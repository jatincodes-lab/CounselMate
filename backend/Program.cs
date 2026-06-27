using EducationCrm.Api.Data;
using EducationCrm.Api.Models;
using EducationCrm.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(DatabaseConnection.GetConnectionString(builder.Configuration));
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? ["http://localhost:5173", "http://127.0.0.1:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.Use(async (httpContext, next) =>
{
    if (HttpMethods.IsOptions(httpContext.Request.Method) ||
        IsPublicEndpoint(httpContext.Request.Path))
    {
        await next();
        return;
    }

    var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(authHeader) ||
        !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        await WriteAuthErrorAsync(httpContext, StatusCodes.Status401Unauthorized, "Authentication token is required.");
        return;
    }

    var token = authHeader["Bearer ".Length..].Trim();
    var tokenClaims = ValidateAccessToken(token, builder.Configuration);
    if (tokenClaims is null)
    {
        await WriteAuthErrorAsync(httpContext, StatusCodes.Status401Unauthorized, "Authentication token is invalid or expired.");
        return;
    }

    var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
    var user = await db.Users
        .AsNoTracking()
        .Where(item => item.Id == tokenClaims.UserId && item.TenantId == tokenClaims.TenantId && item.IsActive && item.Tenant.IsActive)
        .Select(item => new AuthenticatedUser(
            item.Id,
            item.TenantId,
            item.Tenant.Name,
            item.Tenant.Slug,
            item.FullName,
            item.Email,
            item.Role.ToString()
        ))
        .FirstOrDefaultAsync(httpContext.RequestAborted);

    if (user is null)
    {
        await WriteAuthErrorAsync(httpContext, StatusCodes.Status401Unauthorized, "User is inactive or no longer available.");
        return;
    }

    httpContext.Items["CurrentUser"] = user;
    await next();
});

var api = app.MapGroup("/api");

api.MapGet("/health", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);

    return Results.Ok(new
    {
        status = canConnect ? "healthy" : "degraded",
        service = "CounselMate API",
        database = canConnect ? "connected" : "unavailable",
        checkedAt = IndianClock.Now()
    });
});

api.MapPost("/auth/login", async (
    LoginRequest request,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateLoginRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var email = NormalizeEmail(request.Email);
    var matchingUsers = await db.Users
        .Include(item => item.Tenant)
        .Where(item => item.Email == email)
        .ToListAsync(cancellationToken);

    var user = matchingUsers
        .FirstOrDefault(item => item.IsActive && item.Tenant.IsActive && VerifyPassword(request.Password, item.PasswordHash));

    if (user is null)
    {
        var activeUsers = matchingUsers.Where(item => item.IsActive && item.Tenant.IsActive).ToList();
        if (activeUsers.Count > 0)
        {
            foreach (var activeUser in activeUsers)
            {
                activeUser.FailedLoginAttempts += 1;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Json(new { message = "Invalid email or password." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    user.FailedLoginAttempts = 0;
    user.LastLoginAt = IndianClock.Now();
    await db.SaveChangesAsync(cancellationToken);

    var currentUser = new AuthenticatedUser(
        user.Id,
        user.TenantId,
        user.Tenant.Name,
        user.Tenant.Slug,
        user.FullName,
        user.Email,
        user.Role.ToString()
    );
    var expiresAt = DateTimeOffset.UtcNow.AddHours(GetTokenLifetimeHours(configuration));
    var token = CreateAccessToken(currentUser, expiresAt, configuration);

    return Results.Ok(new AuthResponse(token, expiresAt, currentUser));
});

api.MapGet("/auth/me", (HttpContext httpContext) =>
{
    var user = TenantResolver.GetCurrentUser(httpContext);
    return user is null
        ? Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized)
        : Results.Ok(user);
});

api.MapPost("/auth/forgot-password", async (
    ForgotPasswordRequest request,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateForgotPasswordRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var email = NormalizeEmail(request.Email);
    _ = await db.Users
        .AsNoTracking()
        .Where(item => item.Email == email && item.Tenant.IsActive)
        .Select(item => item.Id)
        .FirstOrDefaultAsync(cancellationToken);

    return Results.Ok(new PasswordResetRequestResponse(
        "If this email belongs to an active CounselMate account, ask your institute owner or admin to reset the password. Email reset links can be enabled after mail delivery is configured."));
});

api.MapPost("/auth/change-password", async (
    ChangePasswordRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var validationErrors = ValidateChangePasswordRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var user = await db.Users
        .FirstOrDefaultAsync(item => item.Id == currentUser.UserId && item.TenantId == currentUser.TenantId && item.IsActive && item.Tenant.IsActive, cancellationToken);
    if (user is null)
    {
        return Results.Json(new { message = "User is inactive or no longer available." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["currentPassword"] = ["Current password is incorrect."]
        });
    }

    user.PasswordHash = HashPassword(request.NewPassword);
    user.FailedLoginAttempts = 0;
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new { message = "Password changed successfully." });
});

api.MapGet("/platform/tenants", async (
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    if (!CanManagePlatform(TenantResolver.GetCurrentUser(httpContext)))
    {
        return Results.Json(new { message = "Only platform owners can manage tenants." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var tenants = await db.Tenants
        .AsNoTracking()
        .OrderBy(tenant => tenant.Name)
        .Select(tenant => new TenantListItemResponse(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.IsActive,
            tenant.CreatedAt,
            tenant.Users.Count(user => user.IsActive),
            tenant.Leads.Count()
        ))
        .ToListAsync(cancellationToken);

    return Results.Ok(tenants);
});

api.MapPost("/platform/tenants", async (
    CreateTenantRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    if (!CanManagePlatform(TenantResolver.GetCurrentUser(httpContext)))
    {
        return Results.Json(new { message = "Only platform owners can create tenants." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var validationErrors = ValidateCreateTenantRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var slug = NormalizeSlug(request.Slug);
    var adminEmail = NormalizeEmail(request.AdminEmail);
    var duplicateTenant = await db.Tenants.AnyAsync(tenant => tenant.Slug == slug, cancellationToken);
    if (duplicateTenant)
    {
        return Results.Conflict(new { message = "A tenant already exists with this slug." });
    }

    var duplicateAdmin = await db.Users.AnyAsync(user => user.Email == adminEmail && user.Tenant.Slug == slug, cancellationToken);
    if (duplicateAdmin)
    {
        return Results.Conflict(new { message = "A user already exists with this email for this tenant." });
    }

    var now = IndianClock.Now();
    var tenantId = Guid.NewGuid();
    var branchId = Guid.NewGuid();
    var adminUserId = Guid.NewGuid();

    var tenant = new Tenant
    {
        Id = tenantId,
        Name = NormalizeName(request.Name),
        Slug = slug,
        IsActive = true,
        CreatedAt = now
    };

    var branch = new Branch
    {
        Id = branchId,
        TenantId = tenantId,
        Name = NormalizeName(request.BranchName),
        NormalizedName = NormalizeMasterName(request.BranchName),
        City = NormalizeName(request.City),
        IsActive = true,
        Version = 1,
        CreatedAt = now,
        UpdatedAt = now
    };

    var adminUser = new AppUser
    {
        Id = adminUserId,
        TenantId = tenantId,
        BranchId = branchId,
        FullName = NormalizeName(request.AdminFullName),
        Email = adminEmail,
        PasswordHash = HashPassword(request.AdminPassword),
        Role = UserRole.Admin,
        IsActive = true,
        CreatedAt = now
    };

    db.Tenants.Add(tenant);
    db.Branches.Add(branch);
    db.Users.Add(adminUser);
    AddDefaultTenantSetup(db, tenantId, now);

    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/platform/tenants/{tenant.Slug}", new TenantCreatedResponse(
        tenant.Id,
        tenant.Name,
        tenant.Slug,
        adminUser.Email
    ));
});

api.MapGet("/users", async (
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var users = await db.Users
        .AsNoTracking()
        .Where(user => user.TenantId == currentUser.TenantId)
        .OrderBy(user => user.FullName)
        .Select(user => new UserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Role.ToString(),
            user.BranchId,
            user.Branch == null ? null : user.Branch.Name,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt
        ))
        .ToListAsync(cancellationToken);

    return Results.Ok(users);
});

api.MapPost("/users", async (
    CreateUserRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only admins can create users." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var validationErrors = ValidateCreateUserRequest(request, currentUser);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var email = NormalizeEmail(request.Email);
    var duplicateUser = await db.Users.AnyAsync(
        user => user.TenantId == currentUser!.TenantId && user.Email == email,
        cancellationToken);
    if (duplicateUser)
    {
        return Results.Conflict(new { message = "A user already exists with this email in this tenant." });
    }

    if (request.BranchId is not null)
    {
        var branchExists = await db.Branches.AnyAsync(
            branch => branch.TenantId == currentUser!.TenantId && branch.IsActive && branch.Id == request.BranchId,
            cancellationToken);
        if (!branchExists)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["branchId"] = ["Select a valid active branch."]
            });
        }
    }

    var user = new AppUser
    {
        Id = Guid.NewGuid(),
        TenantId = currentUser!.TenantId,
        BranchId = request.BranchId,
        FullName = NormalizeName(request.FullName),
        Email = email,
        PasswordHash = HashPassword(request.Password),
        Role = Enum.Parse<UserRole>(request.Role),
        IsActive = true,
        CreatedAt = IndianClock.Now()
    };

    db.Users.Add(user);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/users/{user.Id}", await GetUserResponseAsync(db, currentUser.TenantId, user.Id, cancellationToken));
});

api.MapPatch("/users/{id:guid}", async (
    Guid id,
    UpdateUserRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only admins can update users." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var user = await db.Users
        .FirstOrDefaultAsync(item => item.TenantId == currentUser!.TenantId && item.Id == id, cancellationToken);
    if (user is null)
    {
        return Results.NotFound(new { message = "User not found." });
    }

    if (user.Role == UserRole.Owner && currentUser!.Role != nameof(UserRole.Owner))
    {
        return Results.Json(new { message = "Only platform owners can update owner accounts." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var validationErrors = ValidateUpdateUserRequest(request, currentUser, user);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    if (request.BranchId is not null)
    {
        var branchExists = await db.Branches.AnyAsync(
            branch => branch.TenantId == currentUser!.TenantId && branch.IsActive && branch.Id == request.BranchId,
            cancellationToken);
        if (!branchExists)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["branchId"] = ["Select a valid active branch."]
            });
        }
    }

    user.FullName = NormalizeName(request.FullName);
    user.BranchId = request.BranchId;
    user.Role = Enum.Parse<UserRole>(request.Role);
    user.IsActive = request.IsActive;

    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(await GetUserResponseAsync(db, currentUser!.TenantId, user.Id, cancellationToken));
});

api.MapPost("/users/{id:guid}/reset-password", async (
    Guid id,
    ResetUserPasswordRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only admins can reset passwords." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var user = await db.Users
        .FirstOrDefaultAsync(item => item.TenantId == currentUser!.TenantId && item.Id == id, cancellationToken);
    if (user is null)
    {
        return Results.NotFound(new { message = "User not found." });
    }

    if (user.Id == currentUser!.UserId)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["user"] = ["Use Change Password to update your own password."]
        });
    }

    if (user.Role == UserRole.Owner && currentUser.Role != nameof(UserRole.Owner))
    {
        return Results.Json(new { message = "Only platform owners can reset owner passwords." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var validationErrors = ValidateResetUserPasswordRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    user.PasswordHash = HashPassword(request.NewPassword);
    user.FailedLoginAttempts = 0;
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new { message = $"Password reset for {user.FullName}." });
});

var masterDataApi = api.MapGroup("/master-data");

masterDataApi.MapGet("", async (
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(await GetMasterDataResponseAsync(db, currentUser.TenantId, cancellationToken));
});

masterDataApi.MapPost("/branches", async (
    CreateBranchMasterRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can manage master data." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var errors = ValidateBranchMasterRequest(request.Name, request.City);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var name = NormalizeName(request.Name);
    var normalizedName = NormalizeMasterName(request.Name);
    if (await db.Branches.AnyAsync(item => item.TenantId == currentUser!.TenantId && item.NormalizedName == normalizedName, cancellationToken))
    {
        return Results.Conflict(new { message = "A branch with this name already exists." });
    }

    var now = IndianClock.Now();
    var branch = new Branch
    {
        Id = Guid.NewGuid(), TenantId = currentUser!.TenantId, Name = name, NormalizedName = normalizedName,
        City = NormalizeName(request.City), IsActive = true, Version = 1, CreatedAt = now, UpdatedAt = now
    };
    db.Branches.Add(branch);

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception))
    {
        return Results.Conflict(new { message = "A branch with this name already exists." });
    }

    return Results.Created($"/api/master-data/branches/{branch.Id}", new MasterMutationResponse(branch.Id, branch.Name, branch.Version, "Branch created."));
});

masterDataApi.MapPatch("/branches/{id:guid}", async (
    Guid id,
    UpdateBranchMasterRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can manage master data." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var errors = ValidateBranchMasterRequest(request.Name, request.City);
    AddVersionError(errors, request.Version);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var branch = await db.Branches.FirstOrDefaultAsync(item => item.TenantId == currentUser!.TenantId && item.Id == id, cancellationToken);
    if (branch is null)
    {
        return Results.NotFound(new { message = "Branch not found." });
    }
    if (branch.Version != request.Version)
    {
        return Results.Conflict(new { message = "This branch was changed by another user. Refresh and try again." });
    }

    var normalizedName = NormalizeMasterName(request.Name);
    if (await db.Branches.AnyAsync(item => item.TenantId == currentUser!.TenantId && item.Id != id && item.NormalizedName == normalizedName, cancellationToken))
    {
        return Results.Conflict(new { message = "A branch with this name already exists." });
    }

    if (branch.IsActive && !request.IsActive)
    {
        var activeUsers = await db.Users.CountAsync(item => item.TenantId == currentUser!.TenantId && item.BranchId == id && item.IsActive, cancellationToken);
        if (activeUsers > 0)
        {
            return Results.Conflict(new { message = $"Reassign {activeUsers} active user(s) before deactivating this branch." });
        }
    }

    branch.Name = NormalizeName(request.Name);
    branch.NormalizedName = normalizedName;
    branch.City = NormalizeName(request.City);
    branch.IsActive = request.IsActive;
    branch.Version += 1;
    branch.UpdatedAt = IndianClock.Now();

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(new { message = "This branch was changed by another user. Refresh and try again." });
    }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception))
    {
        return Results.Conflict(new { message = "A branch with this name already exists." });
    }

    return Results.Ok(new MasterMutationResponse(branch.Id, branch.Name, branch.Version, "Branch updated."));
});

masterDataApi.MapPost("/courses", async (
    CreateNamedMasterRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can manage master data." }, statusCode: StatusCodes.Status403Forbidden);
    }
    var errors = ValidateNamedMasterRequest(request.Name, 160);
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    var normalizedName = NormalizeMasterName(request.Name);
    if (await db.Courses.AnyAsync(item => item.TenantId == currentUser!.TenantId && item.NormalizedName == normalizedName, cancellationToken))
        return Results.Conflict(new { message = "A course with this name already exists." });

    var course = CreateCourse(currentUser!.TenantId, NormalizeName(request.Name), IndianClock.Now());
    db.Courses.Add(course);
    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception)) { return Results.Conflict(new { message = "A course with this name already exists." }); }
    return Results.Created($"/api/master-data/courses/{course.Id}", new MasterMutationResponse(course.Id, course.Name, course.Version, "Course created."));
});

masterDataApi.MapPatch("/courses/{id:guid}", async (
    Guid id,
    UpdateNamedMasterRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser)) return Results.Json(new { message = "Only owners and admins can manage master data." }, statusCode: StatusCodes.Status403Forbidden);
    var errors = ValidateNamedMasterRequest(request.Name, 160);
    AddVersionError(errors, request.Version);
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    var course = await db.Courses.FirstOrDefaultAsync(item => item.TenantId == currentUser!.TenantId && item.Id == id, cancellationToken);
    if (course is null) return Results.NotFound(new { message = "Course not found." });
    if (course.Version != request.Version) return Results.Conflict(new { message = "This course was changed by another user. Refresh and try again." });
    var normalizedName = NormalizeMasterName(request.Name);
    if (await db.Courses.AnyAsync(item => item.TenantId == currentUser!.TenantId && item.Id != id && item.NormalizedName == normalizedName, cancellationToken))
        return Results.Conflict(new { message = "A course with this name already exists." });
    if (course.IsActive && !request.IsActive && await db.Courses.CountAsync(item => item.TenantId == currentUser!.TenantId && item.IsActive, cancellationToken) <= 1)
        return Results.Conflict(new { message = "At least one active course is required." });

    course.Name = NormalizeName(request.Name); course.NormalizedName = normalizedName; course.IsActive = request.IsActive;
    course.Version += 1; course.UpdatedAt = IndianClock.Now();
    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This course was changed by another user. Refresh and try again." }); }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception)) { return Results.Conflict(new { message = "A course with this name already exists." }); }
    return Results.Ok(new MasterMutationResponse(course.Id, course.Name, course.Version, "Course updated."));
});

masterDataApi.MapPost("/lead-sources", async (
    CreateNamedMasterRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser)) return Results.Json(new { message = "Only owners and admins can manage master data." }, statusCode: StatusCodes.Status403Forbidden);
    var errors = ValidateNamedMasterRequest(request.Name, 120);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var normalizedName = NormalizeMasterName(request.Name);
    if (await db.LeadSources.AnyAsync(item => item.TenantId == currentUser!.TenantId && item.NormalizedName == normalizedName, cancellationToken))
        return Results.Conflict(new { message = "A lead source with this name already exists." });
    var source = CreateLeadSource(currentUser!.TenantId, NormalizeName(request.Name), IndianClock.Now());
    db.LeadSources.Add(source);
    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception)) { return Results.Conflict(new { message = "A lead source with this name already exists." }); }
    return Results.Created($"/api/master-data/lead-sources/{source.Id}", new MasterMutationResponse(source.Id, source.Name, source.Version, "Lead source created."));
});

masterDataApi.MapPatch("/lead-sources/{id:guid}", async (
    Guid id,
    UpdateNamedMasterRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser)) return Results.Json(new { message = "Only owners and admins can manage master data." }, statusCode: StatusCodes.Status403Forbidden);
    var errors = ValidateNamedMasterRequest(request.Name, 120);
    AddVersionError(errors, request.Version);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var source = await db.LeadSources.FirstOrDefaultAsync(item => item.TenantId == currentUser!.TenantId && item.Id == id, cancellationToken);
    if (source is null) return Results.NotFound(new { message = "Lead source not found." });
    if (source.Version != request.Version) return Results.Conflict(new { message = "This lead source was changed by another user. Refresh and try again." });
    var normalizedName = NormalizeMasterName(request.Name);
    if (await db.LeadSources.AnyAsync(item => item.TenantId == currentUser!.TenantId && item.Id != id && item.NormalizedName == normalizedName, cancellationToken))
        return Results.Conflict(new { message = "A lead source with this name already exists." });
    if (source.IsActive && !request.IsActive && await db.LeadSources.CountAsync(item => item.TenantId == currentUser!.TenantId && item.IsActive, cancellationToken) <= 1)
        return Results.Conflict(new { message = "At least one active lead source is required." });

    source.Name = NormalizeName(request.Name); source.NormalizedName = normalizedName; source.IsActive = request.IsActive;
    source.Version += 1; source.UpdatedAt = IndianClock.Now();
    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This lead source was changed by another user. Refresh and try again." }); }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception)) { return Results.Conflict(new { message = "A lead source with this name already exists." }); }
    return Results.Ok(new MasterMutationResponse(source.Id, source.Name, source.Version, "Lead source updated."));
});

masterDataApi.MapPost("/lead-stages", async (
    CreateLeadStageMasterRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser)) return Results.Json(new { message = "Only owners and admins can manage master data." }, statusCode: StatusCodes.Status403Forbidden);
    var errors = ValidateLeadStageMasterRequest(request.Name, request.IsDefaultStage, request.IsWonStage, request.IsLostStage);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var normalizedName = NormalizeMasterName(request.Name);
    if (await db.LeadStages.AnyAsync(item => item.TenantId == currentUser!.TenantId && item.NormalizedName == normalizedName, cancellationToken))
        return Results.Conflict(new { message = "A lead stage with this name already exists." });

    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    LeadStage stage;
    try
    {
        if (request.IsDefaultStage)
        {
            var currentDefaults = await db.LeadStages.Where(item => item.TenantId == currentUser!.TenantId && item.IsDefaultStage).ToListAsync(cancellationToken);
            foreach (var item in currentDefaults) { item.IsDefaultStage = false; item.Version += 1; item.UpdatedAt = IndianClock.Now(); }
            await db.SaveChangesAsync(cancellationToken);
        }
        var nextOrder = (await db.LeadStages.Where(item => item.TenantId == currentUser!.TenantId).MaxAsync(item => (int?)item.SortOrder, cancellationToken) ?? 0) + 10;
        stage = CreateLeadStage(currentUser!.TenantId, NormalizeName(request.Name), nextOrder, IndianClock.Now(), request.IsDefaultStage, request.IsWonStage, request.IsLostStage);
        db.LeadStages.Add(stage);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "A lead stage was changed by another user. Refresh and try again." }); }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception)) { return Results.Conflict(new { message = "A lead stage with this name already exists." }); }
    return Results.Created($"/api/master-data/lead-stages/{stage.Id}", new MasterMutationResponse(stage.Id, stage.Name, stage.Version, "Lead stage created."));
});

masterDataApi.MapPatch("/lead-stages/{id:guid}", async (
    Guid id,
    UpdateLeadStageMasterRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser)) return Results.Json(new { message = "Only owners and admins can manage master data." }, statusCode: StatusCodes.Status403Forbidden);
    var errors = ValidateLeadStageMasterRequest(request.Name, request.IsDefaultStage, request.IsWonStage, request.IsLostStage);
    AddVersionError(errors, request.Version);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var stage = await db.LeadStages.FirstOrDefaultAsync(item => item.TenantId == currentUser!.TenantId && item.Id == id, cancellationToken);
    if (stage is null) return Results.NotFound(new { message = "Lead stage not found." });
    if (stage.Version != request.Version) return Results.Conflict(new { message = "This lead stage was changed by another user. Refresh and try again." });
    var normalizedName = NormalizeMasterName(request.Name);
    if (await db.LeadStages.AnyAsync(item => item.TenantId == currentUser!.TenantId && item.Id != id && item.NormalizedName == normalizedName, cancellationToken))
        return Results.Conflict(new { message = "A lead stage with this name already exists." });
    if (stage.IsActive && !request.IsActive)
    {
        if (stage.IsDefaultStage) return Results.Conflict(new { message = "Select another default stage before deactivating this stage." });
        if (await db.LeadStages.CountAsync(item => item.TenantId == currentUser!.TenantId && item.IsActive, cancellationToken) <= 1)
            return Results.Conflict(new { message = "At least one active lead stage is required." });
        var leadCount = await db.Leads.CountAsync(item => item.TenantId == currentUser!.TenantId && item.LeadStageId == id, cancellationToken);
        if (leadCount > 0) return Results.Conflict(new { message = $"Move {leadCount} lead(s) to another stage before deactivating this stage." });
        if (stage.IsWonStage && await db.LeadStages.CountAsync(item => item.TenantId == currentUser!.TenantId && item.IsActive && item.IsWonStage, cancellationToken) <= 1)
            return Results.Conflict(new { message = "At least one active won stage is required." });
        if (stage.IsLostStage && await db.LeadStages.CountAsync(item => item.TenantId == currentUser!.TenantId && item.IsActive && item.IsLostStage, cancellationToken) <= 1)
            return Results.Conflict(new { message = "At least one active lost stage is required." });
    }
    if (stage.IsDefaultStage && !request.IsDefaultStage)
        return Results.Conflict(new { message = "Set another stage as default instead of removing the current default." });
    if (stage.IsWonStage && !request.IsWonStage && stage.IsActive && await db.LeadStages.CountAsync(item => item.TenantId == currentUser!.TenantId && item.IsActive && item.IsWonStage, cancellationToken) <= 1)
        return Results.Conflict(new { message = "At least one active won stage is required." });
    if (stage.IsLostStage && !request.IsLostStage && stage.IsActive && await db.LeadStages.CountAsync(item => item.TenantId == currentUser!.TenantId && item.IsActive && item.IsLostStage, cancellationToken) <= 1)
        return Results.Conflict(new { message = "At least one active lost stage is required." });

    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    try
    {
        if (request.IsDefaultStage && !stage.IsDefaultStage)
        {
            var previousDefaults = await db.LeadStages.Where(item => item.TenantId == currentUser!.TenantId && item.IsDefaultStage).ToListAsync(cancellationToken);
            foreach (var item in previousDefaults) { item.IsDefaultStage = false; item.Version += 1; item.UpdatedAt = IndianClock.Now(); }
            await db.SaveChangesAsync(cancellationToken);
        }
        stage.Name = NormalizeName(request.Name); stage.NormalizedName = normalizedName; stage.IsActive = request.IsActive;
        stage.IsDefaultStage = request.IsDefaultStage; stage.IsWonStage = request.IsWonStage; stage.IsLostStage = request.IsLostStage;
        stage.Version += 1; stage.UpdatedAt = IndianClock.Now();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This lead stage was changed by another user. Refresh and try again." }); }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception)) { return Results.Conflict(new { message = "A lead stage with this name already exists." }); }
    return Results.Ok(new MasterMutationResponse(stage.Id, stage.Name, stage.Version, "Lead stage updated."));
});

masterDataApi.MapPost("/lead-stages/reorder", async (
    ReorderLeadStagesRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser)) return Results.Json(new { message = "Only owners and admins can manage master data." }, statusCode: StatusCodes.Status403Forbidden);
    if (request.Items is null || request.Items.Count == 0 || request.Items.Any(item => item.Id == Guid.Empty || item.Version < 1) || request.Items.Select(item => item.Id).Distinct().Count() != request.Items.Count)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["items"] = ["Provide every active stage exactly once with its current version."] });

    var stages = await db.LeadStages.Where(item => item.TenantId == currentUser!.TenantId).OrderBy(item => item.SortOrder).ToListAsync(cancellationToken);
    var activeStages = stages.Where(item => item.IsActive).ToList();
    if (activeStages.Count != request.Items.Count || activeStages.Select(item => item.Id).Except(request.Items.Select(item => item.Id)).Any())
        return Results.Conflict(new { message = "The stage list changed. Refresh before reordering." });
    var requestVersions = request.Items.ToDictionary(item => item.Id, item => item.Version);
    if (activeStages.Any(item => requestVersions[item.Id] != item.Version))
        return Results.Conflict(new { message = "A lead stage was changed by another user. Refresh before reordering." });

    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    for (var index = 0; index < stages.Count; index++) stages[index].SortOrder = -10000 - index;
    await db.SaveChangesAsync(cancellationToken);
    var now = IndianClock.Now();
    var orderedIds = request.Items.Select(item => item.Id).ToList();
    for (var index = 0; index < orderedIds.Count; index++)
    {
        var item = stages.Single(stageItem => stageItem.Id == orderedIds[index]);
        item.SortOrder = (index + 1) * 10; item.Version += 1; item.UpdatedAt = now;
    }
    var inactiveStages = stages.Where(item => !item.IsActive).OrderBy(item => item.Name).ToList();
    for (var index = 0; index < inactiveStages.Count; index++)
    {
        inactiveStages[index].SortOrder = (orderedIds.Count + index + 1) * 10;
        inactiveStages[index].Version += 1;
        inactiveStages[index].UpdatedAt = now;
    }
    try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "A lead stage was changed by another user. Refresh before reordering." }); }
    return Results.Ok(new { message = "Lead stages reordered." });
});

api.MapGet("/tenants/current", async (
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    return tenant is null
        ? Results.NotFound(new { message = "Tenant not found." })
        : Results.Ok(tenant);
});

api.MapGet("/dashboard", async (
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var todayStart = IndianClock.TodayStart();
    var totalLeads = await db.Leads.CountAsync(lead => lead.TenantId == tenant.TenantId && lead.ArchivedAt == null, cancellationToken);
    var enrolled = await db.Leads.CountAsync(lead => lead.TenantId == tenant.TenantId && lead.ArchivedAt == null && lead.LeadStage.IsWonStage, cancellationToken);
    var contacted = await db.Leads.CountAsync(lead => lead.TenantId == tenant.TenantId && lead.ArchivedAt == null && lead.LeadStage.SortOrder >= 20, cancellationToken);
    var pendingFollowUps = await db.FollowUps.CountAsync(item => item.TenantId == tenant.TenantId && item.Status == "Scheduled" && item.Lead.ArchivedAt == null, cancellationToken);
    var newLeadsToday = await db.Leads.CountAsync(lead => lead.TenantId == tenant.TenantId && lead.ArchivedAt == null && lead.CreatedAt >= todayStart, cancellationToken);
    var conversionRate = totalLeads == 0 ? 0m : Math.Round((decimal)enrolled / totalLeads * 100m, 1);

    return Results.Ok(new DashboardSummary(
        TotalLeads: totalLeads,
        NewLeadsToday: newLeadsToday,
        Contacted: contacted,
        Enrolled: enrolled,
        PendingFollowUps: pendingFollowUps,
        ConversionRate: conversionRate
    ));
});

api.MapGet("/leads", async (
    string? search,
    Guid? branchId,
    Guid? courseId,
    Guid? sourceId,
    Guid? stageId,
    Guid? assignedUserId,
    string? priority,
    string? archive,
    string? sort,
    int? page,
    int? pageSize,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var pageNumber = Math.Clamp(page ?? 1, 1, 100000);
    var take = Math.Clamp(pageSize ?? 25, 1, 100);
    var normalizedSearch = NormalizeOptionalText(search);
    var normalizedPriority = NormalizeOptionalText(priority);
    var archiveMode = NormalizeOptionalText(archive)?.ToLowerInvariant() ?? "active";

    var query = ApplyLeadAccessScope(db.Leads.AsNoTracking().Where(lead => lead.TenantId == tenant.TenantId), currentUser, accessScope);

    query = archiveMode switch
    {
        "all" => query,
        "archived" => query.Where(lead => lead.ArchivedAt != null),
        _ => query.Where(lead => lead.ArchivedAt == null)
    };

    if (!string.IsNullOrWhiteSpace(normalizedSearch))
    {
        var loweredSearch = normalizedSearch.ToLowerInvariant();
        var phoneSearch = NormalizePhone(normalizedSearch);
        query = query.Where(lead =>
            lead.LeadNumber.ToLower().Contains(loweredSearch) ||
            lead.StudentName.ToLower().Contains(loweredSearch) ||
            lead.Email.ToLower().Contains(loweredSearch) ||
            lead.Phone.Contains(normalizedSearch) ||
            (phoneSearch != "" && lead.NormalizedPhone.Contains(phoneSearch)));
    }

    if (branchId is not null) query = query.Where(lead => lead.BranchId == branchId);
    if (courseId is not null) query = query.Where(lead => lead.CourseId == courseId);
    if (sourceId is not null) query = query.Where(lead => lead.LeadSourceId == sourceId);
    if (stageId is not null) query = query.Where(lead => lead.LeadStageId == stageId);
    if (assignedUserId is not null) query = query.Where(lead => lead.AssignedUserId == assignedUserId);
    if (!string.IsNullOrWhiteSpace(normalizedPriority)) query = query.Where(lead => lead.Priority == normalizedPriority);

    query = (NormalizeOptionalText(sort)?.ToLowerInvariant()) switch
    {
        "oldest" => query.OrderBy(lead => lead.CreatedAt).ThenBy(lead => lead.LeadNumber),
        "name" => query.OrderBy(lead => lead.StudentName).ThenByDescending(lead => lead.CreatedAt),
        "follow-up" => query.OrderBy(lead => lead.NextFollowUpAt == null).ThenBy(lead => lead.NextFollowUpAt).ThenByDescending(lead => lead.CreatedAt),
        "priority" => query.OrderByDescending(lead => lead.Priority == "Urgent").ThenByDescending(lead => lead.Priority == "High").ThenByDescending(lead => lead.Priority == "Medium").ThenByDescending(lead => lead.CreatedAt),
        _ => query.OrderByDescending(lead => lead.CreatedAt).ThenByDescending(lead => lead.LeadNumber)
    };

    var total = await query.CountAsync(cancellationToken);
    var leads = await query
        .Skip((pageNumber - 1) * take)
        .Take(take)
        .AsNoTracking()
        .Select(lead => new LeadResponse(
            lead.LeadNumber,
            lead.StudentName,
            lead.Email,
            lead.Phone,
            lead.LeadSource.Name,
            lead.Course.Name,
            lead.AssignedUser == null ? "Unassigned" : lead.AssignedUser.FullName,
            lead.Status,
            lead.Priority,
            lead.LeadStage.Name,
            lead.Branch == null ? null : lead.Branch.Name,
            lead.AssignedUserId,
            lead.LeadStageId,
            lead.Version,
            lead.CreatedAt,
            lead.UpdatedAt,
            lead.ArchivedAt,
            lead.NextFollowUpAt))
        .ToListAsync(cancellationToken);

    return Results.Ok(new LeadListResponse(leads, pageNumber, take, total));
});

api.MapGet("/leads/options", async (
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var branches = await db.Branches
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.IsActive)
        .OrderBy(item => item.Name)
        .Select(item => new LookupOption(item.Id, item.Name))
        .ToListAsync(cancellationToken);

    var courses = await db.Courses
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.IsActive)
        .OrderBy(item => item.Name)
        .Select(item => new LookupOption(item.Id, item.Name))
        .ToListAsync(cancellationToken);

    var sources = await db.LeadSources
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.IsActive)
        .OrderBy(item => item.Name)
        .Select(item => new LookupOption(item.Id, item.Name))
        .ToListAsync(cancellationToken);

    var stages = await db.LeadStages
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.IsActive)
        .OrderBy(item => item.SortOrder)
        .Select(item => new LookupOption(item.Id, item.Name))
        .ToListAsync(cancellationToken);

    var counselors = await db.Users
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.IsActive && item.Role != UserRole.Accountant && item.Role != UserRole.ReadOnly)
        .OrderBy(item => item.FullName)
        .Select(item => new LookupOption(item.Id, item.FullName))
        .ToListAsync(cancellationToken);

    return Results.Ok(new LeadOptionsResponse(branches, courses, sources, stages, counselors));
});

api.MapPost("/leads", async (
    CreateLeadRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManageLeads(currentUser))
    {
        return Results.Json(new { message = "You do not have permission to create leads." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var validationErrors = ValidateCreateLeadRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var normalizedPhone = NormalizePhone(request.Phone);
    var duplicateLead = await db.Leads
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.NormalizedPhone == normalizedPhone)
        .Select(item => new { item.LeadNumber, item.StudentName })
        .FirstOrDefaultAsync(cancellationToken);

    if (duplicateLead is not null)
    {
        return Results.Conflict(new
        {
            message = $"A lead already exists with this phone number: {duplicateLead.LeadNumber} - {duplicateLead.StudentName}."
        });
    }

    var courseExists = await db.Courses.AnyAsync(
        item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == request.CourseId,
        cancellationToken);
    if (!courseExists)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["courseId"] = ["Select a valid active course."]
        });
    }

    var sourceExists = await db.LeadSources.AnyAsync(
        item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == request.LeadSourceId,
        cancellationToken);
    if (!sourceExists)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["leadSourceId"] = ["Select a valid active lead source."]
        });
    }

    var stageExists = await db.LeadStages.AnyAsync(
        item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == request.LeadStageId,
        cancellationToken);
    if (!stageExists)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["leadStageId"] = ["Select a valid lead stage."]
        });
    }

    if (!CanCreateLeadInBranch(currentUser, accessScope, request.BranchId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["branchId"] = ["You cannot create leads for this branch."]
        });
    }

    if (request.BranchId is not null)
    {
        var branchExists = await db.Branches.AnyAsync(
            item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == request.BranchId,
            cancellationToken);
        if (!branchExists)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["branchId"] = ["Select a valid active branch."]
            });
        }
    }

    if (request.AssignedUserId is not null)
    {
        var assignmentError = await ValidateLeadAssignmentAsync(db, tenant.TenantId, currentUser, accessScope, request.BranchId, request.AssignedUserId.Value, cancellationToken);
        if (assignmentError is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedUserId"] = [assignmentError]
            });
        }
    }

    var now = IndianClock.Now();
    var lead = new Lead
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        BranchId = request.BranchId,
        CourseId = request.CourseId,
        LeadStageId = request.LeadStageId,
        LeadSourceId = request.LeadSourceId,
        AssignedUserId = request.AssignedUserId,
        LeadNumber = await GenerateLeadNumberAsync(db, tenant.TenantId, cancellationToken),
        StudentName = NormalizeName(request.StudentName),
        GuardianName = NormalizeOptionalText(request.GuardianName),
        Email = NormalizeEmail(request.Email),
        Phone = NormalizePhoneDisplay(request.Phone),
        NormalizedPhone = normalizedPhone,
        City = NormalizeOptionalText(request.City),
        Status = NormalizeOptionalText(request.Status) ?? "New Lead",
        Priority = NormalizePriority(request.Priority),
        Version = 1,
        CreatedAt = now,
        UpdatedAt = now,
        NextFollowUpAt = IndianClock.ToIndianTime(request.NextFollowUpAt),
        CreatedByUserId = currentUser?.UserId,
        UpdatedByUserId = currentUser?.UserId
    };

    db.Leads.Add(lead);
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = request.AssignedUserId,
        Type = "LeadCreated",
        Description = $"Lead {lead.LeadNumber} created for {lead.StudentName}.",
        CreatedAt = now
    });

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    {
        return Results.Conflict(new { message = "A lead with this phone number or lead number already exists. Refresh and try again." });
    }

    var response = await db.Leads
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.Id == lead.Id)
        .Select(item => new LeadResponse(
            item.LeadNumber,
            item.StudentName,
            item.Email,
            item.Phone,
            item.LeadSource.Name,
            item.Course.Name,
            item.AssignedUser == null ? "Unassigned" : item.AssignedUser.FullName,
            item.Status,
            item.Priority,
            item.LeadStage.Name,
            item.Branch == null ? null : item.Branch.Name,
            item.AssignedUserId,
            item.LeadStageId,
            item.Version,
            item.CreatedAt,
            item.UpdatedAt,
            item.ArchivedAt,
            item.NextFollowUpAt))
        .FirstAsync(cancellationToken);

    return Results.Created($"/api/leads/{response.Id}", response);
});

api.MapGet("/leads/{id}", async (
    string id,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var canAccessLead = await ApplyLeadAccessScope(
            db.Leads.AsNoTracking().Where(item => item.TenantId == tenant.TenantId && item.LeadNumber == id),
            currentUser,
            accessScope)
        .AnyAsync(cancellationToken);
    if (!canAccessLead)
    {
        return Results.NotFound(new { message = "Lead not found." });
    }

    var lead = await db.Leads
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.LeadNumber == id)
        .Select(item => new LeadDetailResponse(
            item.LeadNumber,
            item.StudentName,
            item.GuardianName,
            item.Email,
            item.Phone,
            item.City,
            item.BranchId,
            item.Branch == null ? null : item.Branch.Name,
            item.CourseId,
            item.Course.Name,
            item.LeadSourceId,
            item.LeadSource.Name,
            item.LeadStageId,
            item.LeadStage.Name,
            item.AssignedUserId,
            item.AssignedUser == null ? "Unassigned" : item.AssignedUser.FullName,
            item.Status,
            item.Priority,
            item.Version,
            item.CreatedAt,
            item.UpdatedAt,
            item.ArchivedAt,
            item.NextFollowUpAt,
            item.FollowUps
                .OrderByDescending(followUp => followUp.DueAt)
                .Select(followUp => new FollowUpResponse(
                    followUp.Id.ToString(),
                    item.LeadNumber,
                    item.StudentName,
                    followUp.Type,
                    followUp.Priority,
                    followUp.Status,
                    followUp.Version,
                    followUp.DueAt,
                    followUp.CreatedAt,
                    followUp.UpdatedAt,
                    followUp.CompletedAt,
                    followUp.CancelledAt,
                    followUp.AssignedUser == null ? "Unassigned" : followUp.AssignedUser.FullName
                ))
                .ToArray(),
            item.Activities
                .OrderByDescending(activity => activity.CreatedAt)
                .Select(activity => new ActivityResponse(
                    activity.Id.ToString(),
                    activity.Type,
                    activity.Description,
                    activity.CreatedByUser == null ? "System" : activity.CreatedByUser.FullName,
                    activity.CreatedAt
                ))
                .ToArray()
        ))
        .FirstOrDefaultAsync(cancellationToken);

    return lead is null
        ? Results.NotFound(new { message = "Lead not found." })
        : Results.Ok(lead);
});

api.MapPatch("/leads/{id}", async (
    string id,
    UpdateLeadRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManageLeads(currentUser))
    {
        return Results.Json(new { message = "You do not have permission to update leads." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var lead = await db.Leads
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null)
    {
        return Results.NotFound(new { message = "Lead not found." });
    }

    if (!CanAccessLead(currentUser, accessScope, lead))
    {
        return Results.NotFound(new { message = "Lead not found." });
    }

    if (lead.ArchivedAt is not null)
    {
        return Results.Conflict(new { message = "Restore this lead before editing it." });
    }

    if (request.Version != lead.Version)
    {
        return Results.Conflict(new { message = "This lead was changed by another user. Refresh and try again." });
    }

    var validationErrors = ValidateUpdateLeadRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var course = await db.Courses
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == request.CourseId, cancellationToken);
    if (course is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["courseId"] = ["Select a valid active course."]
        });
    }

    var source = await db.LeadSources
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == request.LeadSourceId, cancellationToken);
    if (source is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["leadSourceId"] = ["Select a valid active lead source."]
        });
    }

    var stage = await db.LeadStages
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == request.LeadStageId, cancellationToken);
    if (stage is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["leadStageId"] = ["Select a valid lead stage."]
        });
    }

    if (!CanCreateLeadInBranch(currentUser, accessScope, request.BranchId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["branchId"] = ["You cannot move leads to this branch."]
        });
    }

    if (request.BranchId is not null)
    {
        var branchExists = await db.Branches.AnyAsync(
            item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == request.BranchId,
            cancellationToken);
        if (!branchExists)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["branchId"] = ["Select a valid active branch."]
            });
        }
    }

    if (request.AssignedUserId is not null)
    {
        var assignmentError = await ValidateLeadAssignmentAsync(db, tenant.TenantId, currentUser, accessScope, request.BranchId, request.AssignedUserId.Value, cancellationToken);
        if (assignmentError is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedUserId"] = [assignmentError]
            });
        }
    }

    var normalizedPhone = NormalizePhone(request.Phone);
    var duplicateLead = await db.Leads
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.Id != lead.Id && item.NormalizedPhone == normalizedPhone)
        .Select(item => new { item.LeadNumber, item.StudentName })
        .FirstOrDefaultAsync(cancellationToken);
    if (duplicateLead is not null)
    {
        return Results.Conflict(new { message = $"A lead already exists with this phone number: {duplicateLead.LeadNumber} - {duplicateLead.StudentName}." });
    }

    var previousName = lead.StudentName;
    var previousCourseId = lead.CourseId;
    var previousSourceId = lead.LeadSourceId;
    var previousStageId = lead.LeadStageId;
    var previousStatus = lead.Status;
    var previousPriority = lead.Priority;
    var previousBranchId = lead.BranchId;
    var previousAssignedUserId = lead.AssignedUserId;
    var previousFollowUpAt = lead.NextFollowUpAt;

    lead.StudentName = NormalizeName(request.StudentName);
    lead.GuardianName = NormalizeOptionalText(request.GuardianName);
    lead.Email = NormalizeEmail(request.Email);
    lead.Phone = NormalizePhoneDisplay(request.Phone);
    lead.NormalizedPhone = normalizedPhone;
    lead.City = NormalizeOptionalText(request.City);
    lead.BranchId = request.BranchId;
    lead.CourseId = request.CourseId;
    lead.LeadSourceId = request.LeadSourceId;
    lead.LeadStageId = request.LeadStageId;
    lead.AssignedUserId = request.AssignedUserId;
    lead.Status = NormalizeOptionalText(request.Status) ?? lead.Status;
    lead.Priority = NormalizePriority(request.Priority);
    lead.NextFollowUpAt = IndianClock.ToIndianTime(request.NextFollowUpAt);
    lead.UpdatedAt = IndianClock.Now();
    lead.UpdatedByUserId = currentUser?.UserId;
    lead.Version += 1;

    var changes = new List<string>();
    if (!string.Equals(previousName, lead.StudentName, StringComparison.Ordinal))
    {
        changes.Add("profile updated");
    }

    if (previousCourseId != lead.CourseId)
    {
        changes.Add($"course changed to {course.Name}");
    }

    if (previousSourceId != lead.LeadSourceId)
    {
        changes.Add($"source changed to {source.Name}");
    }

    if (previousStageId != lead.LeadStageId)
    {
        changes.Add($"stage changed to {stage.Name}");
    }

    if (!string.Equals(previousStatus, lead.Status, StringComparison.Ordinal))
    {
        changes.Add($"status changed to {lead.Status}");
    }

    if (!string.Equals(previousPriority, lead.Priority, StringComparison.Ordinal))
    {
        changes.Add($"priority changed to {lead.Priority}");
    }

    if (previousAssignedUserId != lead.AssignedUserId)
    {
        changes.Add("counsellor assignment updated");
    }

    if (previousBranchId != lead.BranchId)
    {
        changes.Add("branch updated");
    }

    if (previousFollowUpAt != lead.NextFollowUpAt)
    {
        changes.Add(lead.NextFollowUpAt is null ? "next follow-up cleared" : "next follow-up updated");
    }

    if (changes.Count > 0)
    {
        db.Activities.Add(new EducationCrm.Api.Models.Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            CreatedByUserId = currentUser?.UserId,
            Type = "LeadUpdated",
            Description = $"Lead {lead.LeadNumber} {string.Join(", ", changes)}.",
            CreatedAt = lead.UpdatedAt
        });
    }

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(new { message = "This lead was changed by another user. Refresh and try again." });
    }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception))
    {
        return Results.Conflict(new { message = "A lead with this phone number already exists." });
    }

    return Results.Ok(await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPatch("/leads/{id}/assign", async (
    string id,
    AssignLeadRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManageLeads(currentUser)) return Results.Json(new { message = "You do not have permission to assign leads." }, statusCode: StatusCodes.Status403Forbidden);

    var lead = await db.Leads.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });
    if (lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before assigning it." });
    if (request.Version != lead.Version) return Results.Conflict(new { message = "This lead was changed by another user. Refresh and try again." });

    if (request.AssignedUserId is not null)
    {
        var assignmentError = await ValidateLeadAssignmentAsync(db, tenant.TenantId, currentUser, accessScope, lead.BranchId, request.AssignedUserId.Value, cancellationToken);
        if (assignmentError is not null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["assignedUserId"] = [assignmentError] });
    }

    if (lead.AssignedUserId != request.AssignedUserId)
    {
        lead.AssignedUserId = request.AssignedUserId;
        lead.UpdatedAt = IndianClock.Now();
        lead.UpdatedByUserId = currentUser?.UserId;
        lead.Version += 1;
        db.Activities.Add(new EducationCrm.Api.Models.Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            CreatedByUserId = currentUser?.UserId,
            Type = "LeadAssigned",
            Description = request.AssignedUserId is null ? $"Lead {lead.LeadNumber} unassigned." : $"Lead {lead.LeadNumber} assignment updated.",
            CreatedAt = lead.UpdatedAt
        });
    }

    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This lead was changed by another user. Refresh and try again." }); }
    return Results.Ok(await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPatch("/leads/{id}/stage", async (
    string id,
    UpdateLeadStageRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManageLeads(currentUser)) return Results.Json(new { message = "You do not have permission to update lead stages." }, statusCode: StatusCodes.Status403Forbidden);

    var lead = await db.Leads.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });
    if (lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before moving it." });
    if (request.Version != lead.Version) return Results.Conflict(new { message = "This lead was changed by another user. Refresh and try again." });

    var stage = await db.LeadStages.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == request.LeadStageId, cancellationToken);
    if (stage is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["leadStageId"] = ["Select a valid active lead stage."] });

    if (lead.LeadStageId != request.LeadStageId)
    {
        lead.LeadStageId = request.LeadStageId;
        lead.Status = NormalizeOptionalText(request.Status) ?? stage.Name;
        lead.UpdatedAt = IndianClock.Now();
        lead.UpdatedByUserId = currentUser?.UserId;
        lead.Version += 1;
        db.Activities.Add(new EducationCrm.Api.Models.Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            CreatedByUserId = currentUser?.UserId,
            Type = "StageChanged",
            Description = $"Lead {lead.LeadNumber} moved to {stage.Name}.",
            CreatedAt = lead.UpdatedAt
        });
    }

    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This lead was changed by another user. Refresh and try again." }); }
    return Results.Ok(await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPatch("/leads/{id}/archive", async (
    string id,
    LeadVersionRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManageLeadArchive(currentUser)) return Results.Json(new { message = "Only owners, admins, and branch managers can archive leads." }, statusCode: StatusCodes.Status403Forbidden);

    var lead = await db.Leads.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });
    if (request.Version != lead.Version) return Results.Conflict(new { message = "This lead was changed by another user. Refresh and try again." });
    if (lead.ArchivedAt is null)
    {
        var now = IndianClock.Now();
        lead.ArchivedAt = now;
        lead.ArchivedByUserId = currentUser?.UserId;
        lead.UpdatedAt = now;
        lead.UpdatedByUserId = currentUser?.UserId;
        lead.Version += 1;
        db.Activities.Add(new EducationCrm.Api.Models.Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            CreatedByUserId = currentUser?.UserId,
            Type = "LeadArchived",
            Description = $"Lead {lead.LeadNumber} archived.",
            CreatedAt = now
        });
    }

    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This lead was changed by another user. Refresh and try again." }); }
    return Results.Ok(await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPatch("/leads/{id}/restore", async (
    string id,
    LeadVersionRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManageLeadArchive(currentUser)) return Results.Json(new { message = "Only owners, admins, and branch managers can restore leads." }, statusCode: StatusCodes.Status403Forbidden);

    var lead = await db.Leads.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });
    if (request.Version != lead.Version) return Results.Conflict(new { message = "This lead was changed by another user. Refresh and try again." });
    if (lead.ArchivedAt is not null)
    {
        var now = IndianClock.Now();
        lead.ArchivedAt = null;
        lead.ArchivedByUserId = null;
        lead.UpdatedAt = now;
        lead.UpdatedByUserId = currentUser?.UserId;
        lead.Version += 1;
        db.Activities.Add(new EducationCrm.Api.Models.Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            CreatedByUserId = currentUser?.UserId,
            Type = "LeadRestored",
            Description = $"Lead {lead.LeadNumber} restored.",
            CreatedAt = now
        });
    }

    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This lead was changed by another user. Refresh and try again." }); }
    return Results.Ok(await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPost("/leads/{id}/activities", async (
    string id,
    AddLeadActivityRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManageLeads(currentUser))
    {
        return Results.Json(new { message = "You do not have permission to add lead activity." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var lead = await db.Leads
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.LeadNumber == id)
        .Select(item => new { item.Id, item.LeadNumber, item.AssignedUserId, item.BranchId, item.ArchivedAt })
        .FirstOrDefaultAsync(cancellationToken);
    if (lead is null)
    {
        return Results.NotFound(new { message = "Lead not found." });
    }
    if (!CanAccessLeadValues(currentUser, accessScope, lead.BranchId, lead.AssignedUserId))
    {
        return Results.NotFound(new { message = "Lead not found." });
    }
    if (lead.ArchivedAt is not null)
    {
        return Results.Conflict(new { message = "Restore this lead before adding activity." });
    }

    var validationErrors = ValidateAddLeadActivityRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = currentUser?.UserId,
        Type = NormalizeActivityType(request.Type),
        Description = NormalizeName(request.Description),
        CreatedAt = IndianClock.Now()
    });

    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/leads/{lead.LeadNumber}", await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPost("/leads/{id}/follow-ups", async (
    string id,
    CreateFollowUpRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManageLeads(currentUser))
    {
        return Results.Json(new { message = "You do not have permission to schedule follow-ups." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var lead = await db.Leads
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null)
    {
        return Results.NotFound(new { message = "Lead not found." });
    }
    if (!CanAccessLead(currentUser, accessScope, lead))
    {
        return Results.NotFound(new { message = "Lead not found." });
    }
    if (lead.ArchivedAt is not null)
    {
        return Results.Conflict(new { message = "Restore this lead before scheduling follow-ups." });
    }

    var validationErrors = ValidateCreateFollowUpRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var assignedUserId = request.AssignedUserId ?? lead.AssignedUserId;
    if (assignedUserId is not null)
    {
        var assignmentError = await ValidateLeadAssignmentAsync(db, tenant.TenantId, currentUser, accessScope, lead.BranchId, assignedUserId.Value, cancellationToken);
        if (assignmentError is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedUserId"] = [assignmentError]
            });
        }
    }

    var dueAt = IndianClock.ToIndianTime(request.DueAt);
    var now = IndianClock.Now();
    var followUp = new FollowUp
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        AssignedUserId = assignedUserId,
        Type = NormalizeFollowUpType(request.Type),
        Priority = NormalizePriority(request.Priority),
        Status = "Scheduled",
        Version = 1,
        DueAt = dueAt,
        CreatedAt = now,
        UpdatedAt = now
    };

    lead.NextFollowUpAt = await CalculateNextScheduledFollowUpAsync(db, tenant.TenantId, lead.Id, followUp.Id, followUp.Status, followUp.DueAt, cancellationToken);
    lead.UpdatedAt = now;
    lead.UpdatedByUserId = currentUser?.UserId;
    lead.Version += 1;
    db.FollowUps.Add(followUp);
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = currentUser?.UserId,
        Type = "FollowUpScheduled",
        Description = $"{followUp.Type} follow-up scheduled for lead {lead.LeadNumber}.",
        CreatedAt = now
    });

    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/leads/{lead.LeadNumber}", await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPatch("/leads/{leadId}/follow-ups/{followUpId}/reschedule", async (
    string leadId,
    Guid followUpId,
    RescheduleFollowUpRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManageLeads(currentUser)) return Results.Json(new { message = "You do not have permission to reschedule follow-ups." }, statusCode: StatusCodes.Status403Forbidden);

    var lead = await db.Leads.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == leadId, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });
    if (lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before rescheduling follow-ups." });

    var followUp = await db.FollowUps.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadId == lead.Id && item.Id == followUpId, cancellationToken);
    if (followUp is null) return Results.NotFound(new { message = "Follow-up not found." });
    if (request.Version != followUp.Version) return Results.Conflict(new { message = "This follow-up was changed by another user. Refresh and try again." });
    if (followUp.Status != "Scheduled") return Results.Conflict(new { message = "Only scheduled follow-ups can be rescheduled." });

    var validationErrors = ValidateRescheduleFollowUpRequest(request);
    if (validationErrors.Count > 0) return Results.ValidationProblem(validationErrors);

    var assignedUserId = request.AssignedUserId ?? followUp.AssignedUserId ?? lead.AssignedUserId;
    if (assignedUserId is not null)
    {
        var assignmentError = await ValidateLeadAssignmentAsync(db, tenant.TenantId, currentUser, accessScope, lead.BranchId, assignedUserId.Value, cancellationToken);
        if (assignmentError is not null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["assignedUserId"] = [assignmentError] });
    }

    var dueAt = IndianClock.ToIndianTime(request.DueAt);
    var now = IndianClock.Now();
    followUp.Type = NormalizeFollowUpType(request.Type);
    followUp.Priority = NormalizePriority(request.Priority);
    followUp.AssignedUserId = assignedUserId;
    followUp.DueAt = dueAt;
    followUp.UpdatedAt = now;
    followUp.Version += 1;

    lead.NextFollowUpAt = await CalculateNextScheduledFollowUpAsync(db, tenant.TenantId, lead.Id, followUp.Id, followUp.Status, followUp.DueAt, cancellationToken);
    lead.UpdatedAt = now;
    lead.UpdatedByUserId = currentUser?.UserId;
    lead.Version += 1;
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = currentUser?.UserId,
        Type = "FollowUpRescheduled",
        Description = $"{followUp.Type} follow-up rescheduled for lead {lead.LeadNumber}.",
        CreatedAt = now
    });

    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This follow-up was changed by another user. Refresh and try again." }); }
    return Results.Ok(await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPatch("/leads/{leadId}/follow-ups/{followUpId}/cancel", async (
    string leadId,
    Guid followUpId,
    FollowUpVersionRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManageLeads(currentUser)) return Results.Json(new { message = "You do not have permission to cancel follow-ups." }, statusCode: StatusCodes.Status403Forbidden);

    var lead = await db.Leads.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == leadId, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });
    if (lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before cancelling follow-ups." });

    var followUp = await db.FollowUps.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadId == lead.Id && item.Id == followUpId, cancellationToken);
    if (followUp is null) return Results.NotFound(new { message = "Follow-up not found." });
    if (request.Version != followUp.Version) return Results.Conflict(new { message = "This follow-up was changed by another user. Refresh and try again." });
    if (followUp.Status == "Completed") return Results.Conflict(new { message = "Completed follow-ups cannot be cancelled." });
    if (followUp.Status == "Cancelled") return Results.Conflict(new { message = "This follow-up is already cancelled." });

    var now = IndianClock.Now();
    followUp.Status = "Cancelled";
    followUp.CancelledAt = now;
    followUp.UpdatedAt = now;
    followUp.Version += 1;
    lead.NextFollowUpAt = await CalculateNextScheduledFollowUpAsync(db, tenant.TenantId, lead.Id, followUp.Id, followUp.Status, followUp.DueAt, cancellationToken);
    lead.UpdatedAt = now;
    lead.UpdatedByUserId = currentUser?.UserId;
    lead.Version += 1;
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = currentUser?.UserId,
        Type = "FollowUpCancelled",
        Description = $"{followUp.Type} follow-up cancelled for lead {lead.LeadNumber}.",
        CreatedAt = now
    });

    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This follow-up was changed by another user. Refresh and try again." }); }
    return Results.Ok(await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPost("/leads/{leadId}/follow-ups/{followUpId}/complete", async (
    string leadId,
    Guid followUpId,
    FollowUpVersionRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManageLeads(currentUser))
    {
        return Results.Json(new { message = "You do not have permission to complete follow-ups." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var lead = await db.Leads
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == leadId, cancellationToken);
    if (lead is null)
    {
        return Results.NotFound(new { message = "Lead not found." });
    }
    if (!CanAccessLead(currentUser, accessScope, lead))
    {
        return Results.NotFound(new { message = "Lead not found." });
    }
    if (lead.ArchivedAt is not null)
    {
        return Results.Conflict(new { message = "Restore this lead before completing follow-ups." });
    }

    var followUp = await db.FollowUps
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadId == lead.Id && item.Id == followUpId, cancellationToken);
    if (followUp is null)
    {
        return Results.NotFound(new { message = "Follow-up not found." });
    }

    if (request.Version != followUp.Version)
    {
        return Results.Conflict(new { message = "This follow-up was changed by another user. Refresh and try again." });
    }

    if (followUp.Status == "Cancelled")
    {
        return Results.Conflict(new { message = "Cancelled follow-ups cannot be completed." });
    }

    if (followUp.Status == "Completed")
    {
        return Results.Conflict(new { message = "This follow-up is already completed." });
    }

    if (followUp.Status == "Scheduled")
    {
        var now = IndianClock.Now();
        followUp.Status = "Completed";
        followUp.CompletedAt = now;
        followUp.UpdatedAt = now;
        followUp.Version += 1;
        db.Activities.Add(new EducationCrm.Api.Models.Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            CreatedByUserId = currentUser?.UserId,
            Type = "FollowUpCompleted",
            Description = $"{followUp.Type} follow-up completed for lead {lead.LeadNumber}.",
            CreatedAt = now
        });
    }

    lead.NextFollowUpAt = await CalculateNextScheduledFollowUpAsync(db, tenant.TenantId, lead.Id, followUp.Id, followUp.Status, followUp.DueAt, cancellationToken);
    lead.UpdatedAt = IndianClock.Now();
    lead.UpdatedByUserId = currentUser?.UserId;
    lead.Version += 1;

    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This follow-up was changed by another user. Refresh and try again." }); }
    return Results.Ok(await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapGet("/pipeline", async (
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);

    var stages = await db.LeadStages
        .AsNoTracking()
        .Where(stage => stage.TenantId == tenant.TenantId && stage.IsActive)
        .OrderBy(stage => stage.SortOrder)
        .Select(stage => new { stage.Id, stage.Name })
        .ToListAsync(cancellationToken);

    var leadQuery = ApplyLeadAccessScope(
        db.Leads.AsNoTracking().Where(lead => lead.TenantId == tenant.TenantId && lead.ArchivedAt == null),
        currentUser,
        accessScope);

    var leads = await leadQuery
        .AsNoTracking()
        .Select(lead => new
        {
            lead.LeadStageId,
            Lead = new LeadResponse(
                lead.LeadNumber,
                lead.StudentName,
                lead.Email,
                lead.Phone,
                lead.LeadSource.Name,
                lead.Course.Name,
                lead.AssignedUser == null ? "Unassigned" : lead.AssignedUser.FullName,
                lead.Status,
                lead.Priority,
                lead.LeadStage.Name,
                lead.Branch == null ? null : lead.Branch.Name,
                lead.AssignedUserId,
                lead.LeadStageId,
                lead.Version,
                lead.CreatedAt,
                lead.UpdatedAt,
                lead.ArchivedAt,
                lead.NextFollowUpAt)
        })
        .ToListAsync(cancellationToken);

    var pipeline = stages
        .Select(stage =>
        {
            var stageLeads = leads
                .Where(lead => lead.LeadStageId == stage.Id)
                .Select(lead => lead.Lead)
                .ToArray();

            return new PipelineStageResponse(stage.Name, stageLeads.Length, stageLeads);
        })
        .ToArray();

    return Results.Ok(pipeline);
});

api.MapGet("/follow-ups", async (
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var followUps = await db.FollowUps
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.Lead.ArchivedAt == null)
        .Where(item =>
            accessScope.CanViewAll ||
            (accessScope.BranchId != null && (item.Lead.BranchId == accessScope.BranchId || item.Lead.AssignedUserId == currentUser!.UserId)) ||
            item.Lead.AssignedUserId == currentUser!.UserId)
        .OrderBy(item => item.DueAt)
        .Select(item => new FollowUpResponse(
            item.Id.ToString(),
            item.Lead.LeadNumber,
            item.Lead.StudentName,
            item.Type,
            item.Priority,
            item.Status,
            item.Version,
            item.DueAt,
            item.CreatedAt,
            item.UpdatedAt,
            item.CompletedAt,
            item.CancelledAt,
            item.AssignedUser == null ? "Unassigned" : item.AssignedUser.FullName
        ))
        .ToListAsync(cancellationToken);

    return Results.Ok(followUps);
});

api.MapGet("/reports/conversion", async (
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var totalLeads = await db.Leads.CountAsync(lead => lead.TenantId == tenant.TenantId && lead.ArchivedAt == null, cancellationToken);
    var stageCounts = await db.LeadStages
        .AsNoTracking()
        .Where(stage => stage.TenantId == tenant.TenantId && stage.IsActive)
        .OrderBy(stage => stage.SortOrder)
        .Select(stage => new
        {
            stage.Name,
            Count = stage.Leads.Count(lead => lead.TenantId == tenant.TenantId && lead.ArchivedAt == null)
        })
        .ToListAsync(cancellationToken);

    var funnel = stageCounts
        .Select(stage => new FunnelStep(
            stage.Name,
            stage.Count,
            totalLeads == 0 ? 0m : Math.Round((decimal)stage.Count / totalLeads * 100m, 1)
        ))
        .ToArray();

    return Results.Ok(funnel);
});

app.Run();

static bool IsPublicEndpoint(PathString path)
{
    return path.StartsWithSegments("/api/health") ||
        path.StartsWithSegments("/api/auth/login") ||
        path.StartsWithSegments("/api/auth/forgot-password");
}

static async Task WriteAuthErrorAsync(HttpContext httpContext, int statusCode, string message)
{
    httpContext.Response.StatusCode = statusCode;
    httpContext.Response.ContentType = "application/json";
    await httpContext.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
}

static int GetTokenLifetimeHours(IConfiguration configuration)
{
    return int.TryParse(configuration["Jwt:AccessTokenHours"], out var hours) && hours is >= 1 and <= 24
        ? hours
        : 8;
}

static string GetJwtSecret(IConfiguration configuration)
{
    return configuration["Jwt:Secret"]
        ?? configuration["JWT_SECRET"]
        ?? "local-development-secret-change-before-production-minimum-32-chars";
}

static string CreateAccessToken(AuthenticatedUser user, DateTimeOffset expiresAt, IConfiguration configuration)
{
    var headerJson = JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" });
    var payloadJson = JsonSerializer.Serialize(new AccessTokenPayload(
        Sub: user.UserId.ToString(),
        Tid: user.TenantId.ToString(),
        TenantSlug: user.TenantSlug,
        TenantName: user.TenantName,
        Name: user.FullName,
        Email: user.Email,
        Role: user.Role,
        Exp: expiresAt.ToUnixTimeSeconds()
    ));

    var unsignedToken = $"{Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson))}.{Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson))}";
    var signature = SignToken(unsignedToken, configuration);
    return $"{unsignedToken}.{signature}";
}

static TokenClaims? ValidateAccessToken(string token, IConfiguration configuration)
{
    var parts = token.Split('.');
    if (parts.Length != 3)
    {
        return null;
    }

    var unsignedToken = $"{parts[0]}.{parts[1]}";
    var expectedSignature = SignToken(unsignedToken, configuration);
    if (!CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedSignature),
            Encoding.ASCII.GetBytes(parts[2])))
    {
        return null;
    }

    try
    {
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        var payload = JsonSerializer.Deserialize<AccessTokenPayload>(payloadJson);
        if (payload is null || payload.Exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return null;
        }

        if (!Guid.TryParse(payload.Sub, out var userId) || !Guid.TryParse(payload.Tid, out var tenantId))
        {
            return null;
        }

        return new TokenClaims(userId, tenantId);
    }
    catch
    {
        return null;
    }
}

static string SignToken(string unsignedToken, IConfiguration configuration)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(GetJwtSecret(configuration)));
    return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken)));
}

static string Base64UrlEncode(byte[] bytes)
{
    return Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

static byte[] Base64UrlDecode(string value)
{
    var padded = value.Replace('-', '+').Replace('_', '/');
    padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
    return Convert.FromBase64String(padded);
}

static bool VerifyPassword(string password, string passwordHash)
{
    var parts = passwordHash.Split('.');
    if (parts.Length != 4 ||
        parts[0] != "v1" ||
        !int.TryParse(parts[1], out var iterations) ||
        iterations < 100000)
    {
        return false;
    }

    try
    {
        var salt = Convert.FromBase64String(parts[2]);
        var expectedHash = Convert.FromBase64String(parts[3]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
    catch
    {
        return false;
    }
}

static string HashPassword(string password)
{
    var salt = RandomNumberGenerator.GetBytes(16);
    const int iterations = 100000;
    var hash = Rfc2898DeriveBytes.Pbkdf2(
        Encoding.UTF8.GetBytes(password),
        salt,
        iterations,
        HashAlgorithmName.SHA256,
        32);

    return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
}

static bool CanManageLeads(AuthenticatedUser? user)
{
    return user?.Role is nameof(UserRole.Owner)
        or nameof(UserRole.Admin)
        or nameof(UserRole.BranchManager)
        or nameof(UserRole.Counselor)
        or nameof(UserRole.Telecaller);
}

static bool CanManageLeadArchive(AuthenticatedUser? user)
{
    return user?.Role is nameof(UserRole.Owner)
        or nameof(UserRole.Admin)
        or nameof(UserRole.BranchManager);
}

static async Task<LeadAccessScope> GetLeadAccessScopeAsync(AppDbContext db, AuthenticatedUser? currentUser, CancellationToken cancellationToken)
{
    if (currentUser is null)
    {
        return new LeadAccessScope(false, null);
    }

    if (currentUser.Role is nameof(UserRole.Owner) or nameof(UserRole.Admin) or nameof(UserRole.Accountant) or nameof(UserRole.ReadOnly))
    {
        return new LeadAccessScope(true, null);
    }

    var branchId = await db.Users
        .AsNoTracking()
        .Where(item => item.Id == currentUser.UserId && item.TenantId == currentUser.TenantId && item.IsActive)
        .Select(item => item.BranchId)
        .FirstOrDefaultAsync(cancellationToken);

    return new LeadAccessScope(false, branchId);
}

static IQueryable<Lead> ApplyLeadAccessScope(IQueryable<Lead> query, AuthenticatedUser? currentUser, LeadAccessScope accessScope)
{
    if (currentUser is null)
    {
        return query.Where(_ => false);
    }

    if (accessScope.CanViewAll)
    {
        return query;
    }

    if (currentUser.Role == nameof(UserRole.BranchManager) && accessScope.BranchId is not null)
    {
        var branchId = accessScope.BranchId.Value;
        var userId = currentUser.UserId;
        return query.Where(item => item.BranchId == branchId || item.AssignedUserId == userId);
    }

    var currentUserId = currentUser.UserId;
    return query.Where(item => item.AssignedUserId == currentUserId);
}

static bool CanAccessLead(AuthenticatedUser? currentUser, LeadAccessScope accessScope, Lead lead)
{
    return CanAccessLeadValues(currentUser, accessScope, lead.BranchId, lead.AssignedUserId);
}

static bool CanAccessLeadValues(AuthenticatedUser? currentUser, LeadAccessScope accessScope, Guid? branchId, Guid? assignedUserId)
{
    if (currentUser is null) return false;
    if (accessScope.CanViewAll) return true;
    if (currentUser.Role == nameof(UserRole.BranchManager) && accessScope.BranchId is not null)
    {
        return branchId == accessScope.BranchId || assignedUserId == currentUser.UserId;
    }

    return assignedUserId == currentUser.UserId;
}

static bool CanCreateLeadInBranch(AuthenticatedUser? currentUser, LeadAccessScope accessScope, Guid? branchId)
{
    if (currentUser is null) return false;
    if (accessScope.CanViewAll) return true;
    if (currentUser.Role == nameof(UserRole.BranchManager))
    {
        return accessScope.BranchId is null ? branchId is null : branchId == accessScope.BranchId;
    }

    return branchId is null || branchId == accessScope.BranchId;
}

static async Task<string?> ValidateLeadAssignmentAsync(
    AppDbContext db,
    Guid tenantId,
    AuthenticatedUser? currentUser,
    LeadAccessScope accessScope,
    Guid? leadBranchId,
    Guid assignedUserId,
    CancellationToken cancellationToken)
{
    if (currentUser is null)
    {
        return "Authentication is required.";
    }

    if (currentUser.Role is nameof(UserRole.Counselor) or nameof(UserRole.Telecaller) && assignedUserId != currentUser.UserId)
    {
        return "You can only assign leads to yourself.";
    }

    var assignee = await db.Users
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.Id == assignedUserId && item.IsActive)
        .Select(item => new { item.Id, item.Role, item.BranchId })
        .FirstOrDefaultAsync(cancellationToken);

    if (assignee is null)
    {
        return "Select a valid active counsellor.";
    }

    if (assignee.Role is UserRole.Accountant or UserRole.ReadOnly)
    {
        return "Leads can only be assigned to CRM working roles.";
    }

    if (currentUser.Role == nameof(UserRole.BranchManager) && accessScope.BranchId is not null)
    {
        if (leadBranchId != accessScope.BranchId)
        {
            return "You can only assign leads from your branch.";
        }

        if (assignee.BranchId is not null && assignee.BranchId != accessScope.BranchId)
        {
            return "Select a user from your branch.";
        }
    }

    if (leadBranchId is not null && assignee.BranchId is not null && assignee.BranchId != leadBranchId)
    {
        return "Assigned user must belong to the lead branch.";
    }

    return null;
}

static bool CanManagePlatform(AuthenticatedUser? user)
{
    return user?.Role == nameof(UserRole.Owner);
}

static bool CanManageUsers(AuthenticatedUser? user)
{
    return user?.Role is nameof(UserRole.Owner) or nameof(UserRole.Admin);
}

static async Task<MasterDataResponse> GetMasterDataResponseAsync(AppDbContext db, Guid tenantId, CancellationToken cancellationToken)
{
    var branches = await db.Branches.AsNoTracking()
        .Where(item => item.TenantId == tenantId)
        .OrderByDescending(item => item.IsActive).ThenBy(item => item.Name)
        .Select(item => new BranchMasterResponse(
            item.Id, item.Name, item.City, item.IsActive, item.Version, item.CreatedAt, item.UpdatedAt,
            item.Users.Count(user => user.IsActive), item.Leads.Count))
        .ToListAsync(cancellationToken);

    var courses = await db.Courses.AsNoTracking()
        .Where(item => item.TenantId == tenantId)
        .OrderByDescending(item => item.IsActive).ThenBy(item => item.Name)
        .Select(item => new NamedMasterResponse(
            item.Id, item.Name, item.IsActive, item.Version, item.CreatedAt, item.UpdatedAt, item.Leads.Count))
        .ToListAsync(cancellationToken);

    var sources = await db.LeadSources.AsNoTracking()
        .Where(item => item.TenantId == tenantId)
        .OrderByDescending(item => item.IsActive).ThenBy(item => item.Name)
        .Select(item => new NamedMasterResponse(
            item.Id, item.Name, item.IsActive, item.Version, item.CreatedAt, item.UpdatedAt, item.Leads.Count))
        .ToListAsync(cancellationToken);

    var stages = await db.LeadStages.AsNoTracking()
        .Where(item => item.TenantId == tenantId)
        .OrderBy(item => item.IsActive ? 0 : 1).ThenBy(item => item.SortOrder)
        .Select(item => new LeadStageMasterResponse(
            item.Id, item.Name, item.SortOrder, item.IsActive, item.IsDefaultStage, item.IsWonStage,
            item.IsLostStage, item.Version, item.CreatedAt, item.UpdatedAt, item.Leads.Count))
        .ToListAsync(cancellationToken);

    return new MasterDataResponse(branches, courses, sources, stages);
}

static bool IsAllowedManagedRole(string? role, AuthenticatedUser? currentUser)
{
    if (string.IsNullOrWhiteSpace(role) || !Enum.TryParse<UserRole>(role, ignoreCase: false, out var parsedRole))
    {
        return false;
    }

    if (parsedRole == UserRole.Owner)
    {
        return currentUser?.Role == nameof(UserRole.Owner);
    }

    return parsedRole is UserRole.Admin
        or UserRole.BranchManager
        or UserRole.Counselor
        or UserRole.Telecaller
        or UserRole.Accountant
        or UserRole.ReadOnly;
}

static void AddDefaultTenantSetup(AppDbContext db, Guid tenantId, DateTimeOffset createdAt)
{
    db.LeadStages.AddRange(
        CreateLeadStage(tenantId, "New Inquiry", 10, createdAt, isDefault: true),
        CreateLeadStage(tenantId, "Contacted", 20, createdAt),
        CreateLeadStage(tenantId, "Interested", 30, createdAt),
        CreateLeadStage(tenantId, "Demo Scheduled", 40, createdAt),
        CreateLeadStage(tenantId, "Application Started", 50, createdAt),
        CreateLeadStage(tenantId, "Enrolled", 60, createdAt, isWon: true),
        CreateLeadStage(tenantId, "Dropped", 70, createdAt, isLost: true)
    );

    db.LeadSources.AddRange(
        CreateLeadSource(tenantId, "Website", createdAt),
        CreateLeadSource(tenantId, "Google Ads", createdAt),
        CreateLeadSource(tenantId, "Referral", createdAt),
        CreateLeadSource(tenantId, "Walk-in", createdAt),
        CreateLeadSource(tenantId, "Social Media", createdAt)
    );

    db.Courses.AddRange(
        CreateCourse(tenantId, "General Admission", createdAt),
        CreateCourse(tenantId, "Counselling Session", createdAt)
    );
}

static Course CreateCourse(Guid tenantId, string name, DateTimeOffset timestamp) => new()
{
    Id = Guid.NewGuid(), TenantId = tenantId, Name = name, NormalizedName = NormalizeMasterName(name),
    IsActive = true, Version = 1, CreatedAt = timestamp, UpdatedAt = timestamp
};

static LeadSource CreateLeadSource(Guid tenantId, string name, DateTimeOffset timestamp) => new()
{
    Id = Guid.NewGuid(), TenantId = tenantId, Name = name, NormalizedName = NormalizeMasterName(name),
    IsActive = true, Version = 1, CreatedAt = timestamp, UpdatedAt = timestamp
};

static LeadStage CreateLeadStage(Guid tenantId, string name, int sortOrder, DateTimeOffset timestamp, bool isDefault = false, bool isWon = false, bool isLost = false) => new()
{
    Id = Guid.NewGuid(), TenantId = tenantId, Name = name, NormalizedName = NormalizeMasterName(name),
    SortOrder = sortOrder, IsActive = true, IsDefaultStage = isDefault, IsWonStage = isWon, IsLostStage = isLost,
    Version = 1, CreatedAt = timestamp, UpdatedAt = timestamp
};

static async Task<LeadDetailResponse> GetLeadDetailAsync(AppDbContext db, Guid tenantId, string leadNumber, CancellationToken cancellationToken)
{
    return await db.Leads
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.LeadNumber == leadNumber)
        .Select(item => new LeadDetailResponse(
            item.LeadNumber,
            item.StudentName,
            item.GuardianName,
            item.Email,
            item.Phone,
            item.City,
            item.BranchId,
            item.Branch == null ? null : item.Branch.Name,
            item.CourseId,
            item.Course.Name,
            item.LeadSourceId,
            item.LeadSource.Name,
            item.LeadStageId,
            item.LeadStage.Name,
            item.AssignedUserId,
            item.AssignedUser == null ? "Unassigned" : item.AssignedUser.FullName,
            item.Status,
            item.Priority,
            item.Version,
            item.CreatedAt,
            item.UpdatedAt,
            item.ArchivedAt,
            item.NextFollowUpAt,
            item.FollowUps
                .OrderByDescending(followUp => followUp.DueAt)
                .Select(followUp => new FollowUpResponse(
                    followUp.Id.ToString(),
                    item.LeadNumber,
                    item.StudentName,
                    followUp.Type,
                    followUp.Priority,
                    followUp.Status,
                    followUp.Version,
                    followUp.DueAt,
                    followUp.CreatedAt,
                    followUp.UpdatedAt,
                    followUp.CompletedAt,
                    followUp.CancelledAt,
                    followUp.AssignedUser == null ? "Unassigned" : followUp.AssignedUser.FullName
                ))
                .ToArray(),
            item.Activities
                .OrderByDescending(activity => activity.CreatedAt)
                .Select(activity => new ActivityResponse(
                    activity.Id.ToString(),
                    activity.Type,
                    activity.Description,
                    activity.CreatedByUser == null ? "System" : activity.CreatedByUser.FullName,
                    activity.CreatedAt
                ))
                .ToArray()
        ))
        .FirstAsync(cancellationToken);
}

static async Task<DateTimeOffset?> CalculateNextScheduledFollowUpAsync(
    AppDbContext db,
    Guid tenantId,
    Guid leadId,
    Guid? changedFollowUpId,
    string? changedStatus,
    DateTimeOffset? changedDueAt,
    CancellationToken cancellationToken)
{
    var scheduledDueTimes = await db.FollowUps
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.LeadId == leadId && item.Status == "Scheduled")
        .Where(item => changedFollowUpId == null || item.Id != changedFollowUpId)
        .Select(item => item.DueAt)
        .ToListAsync(cancellationToken);

    if (changedStatus == "Scheduled" && changedDueAt is not null)
    {
        scheduledDueTimes.Add(changedDueAt.Value);
    }

    return scheduledDueTimes.Count == 0 ? null : scheduledDueTimes.Min();
}

static async Task<UserResponse> GetUserResponseAsync(AppDbContext db, Guid tenantId, Guid userId, CancellationToken cancellationToken)
{
    return await db.Users
        .AsNoTracking()
        .Where(user => user.TenantId == tenantId && user.Id == userId)
        .Select(user => new UserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Role.ToString(),
            user.BranchId,
            user.Branch == null ? null : user.Branch.Name,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt
        ))
        .FirstAsync(cancellationToken);
}

static Dictionary<string, string[]> ValidateCreateLeadRequest(CreateLeadRequest request)
{
    var errors = new Dictionary<string, string[]>();

    AddRequiredError(errors, "studentName", request.StudentName, 160);
    AddOptionalLengthError(errors, "guardianName", request.GuardianName, 160);
    AddRequiredError(errors, "email", request.Email, 240);
    AddRequiredError(errors, "phone", request.Phone, 40);
    AddOptionalLengthError(errors, "city", request.City, 120);
    AddOptionalLengthError(errors, "status", request.Status, 80);
    AddOptionalLengthError(errors, "priority", request.Priority, 40);

    if (!errors.ContainsKey("email") && !IsValidEmail(request.Email))
    {
        errors["email"] = ["Enter a valid email address."];
    }

    if (!errors.ContainsKey("phone"))
    {
        var normalizedPhone = NormalizePhone(request.Phone);
        if (normalizedPhone.Length < 10 || normalizedPhone.Length > 15)
        {
            errors["phone"] = ["Enter a valid phone number with 10 to 15 digits."];
        }
    }

    if (request.CourseId == Guid.Empty)
    {
        errors["courseId"] = ["Course is required."];
    }

    if (request.LeadSourceId == Guid.Empty)
    {
        errors["leadSourceId"] = ["Lead source is required."];
    }

    if (request.LeadStageId == Guid.Empty)
    {
        errors["leadStageId"] = ["Lead stage is required."];
    }

    if (request.NextFollowUpAt is not null && IndianClock.ToIndianTime(request.NextFollowUpAt.Value) < IndianClock.Now().AddMinutes(-5))
    {
        errors["nextFollowUpAt"] = ["Next follow-up cannot be in the past."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateLoginRequest(LoginRequest request)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "email", request.Email, 240);
    AddRequiredError(errors, "password", request.Password, 120);

    if (!errors.ContainsKey("email") && !IsValidEmail(request.Email))
    {
        errors["email"] = ["Enter a valid email address."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateForgotPasswordRequest(ForgotPasswordRequest request)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "email", request.Email, 240);

    if (!errors.ContainsKey("email") && !IsValidEmail(request.Email))
    {
        errors["email"] = ["Enter a valid email address."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateChangePasswordRequest(ChangePasswordRequest request)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "currentPassword", request.CurrentPassword, 120);
    AddRequiredError(errors, "newPassword", request.NewPassword, 120);
    AddRequiredError(errors, "confirmPassword", request.ConfirmPassword, 120);

    if (!errors.ContainsKey("newPassword"))
    {
        AddPasswordPolicyErrors(errors, "newPassword", request.NewPassword);
    }

    if (!errors.ContainsKey("newPassword") &&
        !errors.ContainsKey("currentPassword") &&
        string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
    {
        errors["newPassword"] = ["New password must be different from the current password."];
    }

    if (!errors.ContainsKey("confirmPassword") &&
        !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
    {
        errors["confirmPassword"] = ["Password confirmation does not match."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateResetUserPasswordRequest(ResetUserPasswordRequest request)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "newPassword", request.NewPassword, 120);
    AddRequiredError(errors, "confirmPassword", request.ConfirmPassword, 120);

    if (!errors.ContainsKey("newPassword"))
    {
        AddPasswordPolicyErrors(errors, "newPassword", request.NewPassword);
    }

    if (!errors.ContainsKey("confirmPassword") &&
        !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
    {
        errors["confirmPassword"] = ["Password confirmation does not match."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateNamedMasterRequest(string? name, int maxLength)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "name", name, maxLength);
    return errors;
}

static Dictionary<string, string[]> ValidateBranchMasterRequest(string? name, string? city)
{
    var errors = ValidateNamedMasterRequest(name, 160);
    AddRequiredError(errors, "city", city, 120);
    return errors;
}

static Dictionary<string, string[]> ValidateLeadStageMasterRequest(string? name, bool isDefaultStage, bool isWonStage, bool isLostStage)
{
    var errors = ValidateNamedMasterRequest(name, 120);
    if (isWonStage && isLostStage)
    {
        errors["stageType"] = ["A lead stage cannot be both won and lost."];
    }
    if (isDefaultStage && (isWonStage || isLostStage))
    {
        errors["isDefaultStage"] = ["The default stage cannot be a won or lost stage."];
    }
    return errors;
}

static void AddVersionError(Dictionary<string, string[]> errors, int version)
{
    if (version < 1)
    {
        errors["version"] = ["A valid record version is required."];
    }
}

static Dictionary<string, string[]> ValidateCreateTenantRequest(CreateTenantRequest request)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "name", request.Name, 160);
    AddRequiredError(errors, "slug", request.Slug, 80);
    AddRequiredError(errors, "branchName", request.BranchName, 160);
    AddRequiredError(errors, "city", request.City, 120);
    AddRequiredError(errors, "adminFullName", request.AdminFullName, 160);
    AddRequiredError(errors, "adminEmail", request.AdminEmail, 240);
    AddRequiredError(errors, "adminPassword", request.AdminPassword, 120);

    if (!errors.ContainsKey("slug") && !Regex.IsMatch(NormalizeSlug(request.Slug), "^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$"))
    {
        errors["slug"] = ["Use 3 to 80 lowercase letters, numbers, or hyphens. Start and end with a letter or number."];
    }

    if (!errors.ContainsKey("adminEmail") && !IsValidEmail(request.AdminEmail))
    {
        errors["adminEmail"] = ["Enter a valid admin email address."];
    }

    if (!errors.ContainsKey("adminPassword"))
    {
        AddPasswordPolicyErrors(errors, "adminPassword", request.AdminPassword);
    }

    return errors;
}

static Dictionary<string, string[]> ValidateCreateUserRequest(CreateUserRequest request, AuthenticatedUser? currentUser)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "fullName", request.FullName, 160);
    AddRequiredError(errors, "email", request.Email, 240);
    AddRequiredError(errors, "role", request.Role, 40);
    AddRequiredError(errors, "password", request.Password, 120);

    if (!errors.ContainsKey("email") && !IsValidEmail(request.Email))
    {
        errors["email"] = ["Enter a valid email address."];
    }

    if (!errors.ContainsKey("role") && !IsAllowedManagedRole(request.Role, currentUser))
    {
        errors["role"] = ["Select a valid role for your permission level."];
    }

    if (!errors.ContainsKey("password"))
    {
        AddPasswordPolicyErrors(errors, "password", request.Password);
    }

    return errors;
}

static Dictionary<string, string[]> ValidateUpdateUserRequest(UpdateUserRequest request, AuthenticatedUser? currentUser, AppUser targetUser)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "fullName", request.FullName, 160);
    AddRequiredError(errors, "role", request.Role, 40);

    if (!errors.ContainsKey("role") && !IsAllowedManagedRole(request.Role, currentUser))
    {
        errors["role"] = ["Select a valid role for your permission level."];
    }

    if (!string.IsNullOrWhiteSpace(request.Password))
    {
        errors["password"] = ["Use the dedicated reset password action to change user passwords."];
    }

    if (currentUser?.UserId == targetUser.Id)
    {
        if (!request.IsActive)
        {
            errors["isActive"] = ["You cannot deactivate your own account."];
        }

        if (!string.Equals(request.Role, currentUser.Role, StringComparison.Ordinal))
        {
            errors["role"] = ["You cannot change your own role."];
        }
    }

    return errors;
}

static Dictionary<string, string[]> ValidateUpdateLeadRequest(UpdateLeadRequest request)
{
    var errors = new Dictionary<string, string[]>();

    AddRequiredError(errors, "studentName", request.StudentName, 160);
    AddOptionalLengthError(errors, "guardianName", request.GuardianName, 160);
    AddRequiredError(errors, "email", request.Email, 240);
    AddRequiredError(errors, "phone", request.Phone, 40);
    AddOptionalLengthError(errors, "city", request.City, 120);
    AddOptionalLengthError(errors, "status", request.Status, 80);
    AddOptionalLengthError(errors, "priority", request.Priority, 40);

    if (!errors.ContainsKey("email") && !IsValidEmail(request.Email))
    {
        errors["email"] = ["Enter a valid email address."];
    }

    if (!errors.ContainsKey("phone"))
    {
        var normalizedPhone = NormalizePhone(request.Phone);
        if (normalizedPhone.Length < 10 || normalizedPhone.Length > 15)
        {
            errors["phone"] = ["Enter a valid phone number with 10 to 15 digits."];
        }
    }

    if (request.CourseId == Guid.Empty)
    {
        errors["courseId"] = ["Course is required."];
    }

    if (request.LeadSourceId == Guid.Empty)
    {
        errors["leadSourceId"] = ["Lead source is required."];
    }

    if (request.LeadStageId == Guid.Empty)
    {
        errors["leadStageId"] = ["Lead stage is required."];
    }

    if (request.Version < 1)
    {
        errors["version"] = ["Refresh the lead before saving changes."];
    }

    if (request.NextFollowUpAt is not null && IndianClock.ToIndianTime(request.NextFollowUpAt.Value) < IndianClock.Now().AddMinutes(-5))
    {
        errors["nextFollowUpAt"] = ["Next follow-up cannot be in the past."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateAddLeadActivityRequest(AddLeadActivityRequest request)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "description", request.Description, 500);
    AddOptionalLengthError(errors, "type", request.Type, 40);
    return errors;
}

static Dictionary<string, string[]> ValidateCreateFollowUpRequest(CreateFollowUpRequest request)
{
    var errors = new Dictionary<string, string[]>();

    AddOptionalLengthError(errors, "type", request.Type, 40);
    AddOptionalLengthError(errors, "priority", request.Priority, 40);

    if (request.DueAt == default)
    {
        errors["dueAt"] = ["Due date is required."];
    }
    else if (IndianClock.ToIndianTime(request.DueAt) < IndianClock.Now().AddMinutes(-5))
    {
        errors["dueAt"] = ["Follow-up due date cannot be in the past."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateRescheduleFollowUpRequest(RescheduleFollowUpRequest request)
{
    var errors = new Dictionary<string, string[]>();

    AddOptionalLengthError(errors, "type", request.Type, 40);
    AddOptionalLengthError(errors, "priority", request.Priority, 40);

    if (request.Version < 1)
    {
        errors["version"] = ["Refresh the follow-up before saving changes."];
    }

    if (request.DueAt == default)
    {
        errors["dueAt"] = ["Due date is required."];
    }
    else if (IndianClock.ToIndianTime(request.DueAt) < IndianClock.Now().AddMinutes(-5))
    {
        errors["dueAt"] = ["Follow-up due date cannot be in the past."];
    }

    return errors;
}

static void AddRequiredError(Dictionary<string, string[]> errors, string key, string? value, int maxLength)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        errors[key] = ["This field is required."];
        return;
    }

    if (value.Trim().Length > maxLength)
    {
        errors[key] = [$"Maximum length is {maxLength} characters."];
    }
}

static void AddOptionalLengthError(Dictionary<string, string[]> errors, string key, string? value, int maxLength)
{
    if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > maxLength)
    {
        errors[key] = [$"Maximum length is {maxLength} characters."];
    }
}

static void AddPasswordPolicyErrors(Dictionary<string, string[]> errors, string key, string? password)
{
    if (string.IsNullOrEmpty(password))
    {
        errors[key] = ["Password is required."];
        return;
    }

    if (password.Length is < 8 or > 120)
    {
        errors[key] = ["Password must be 8 to 120 characters."];
        return;
    }

    if (password.Any(char.IsWhiteSpace))
    {
        errors[key] = ["Password cannot contain spaces."];
        return;
    }

    if (!password.Any(char.IsUpper) ||
        !password.Any(char.IsLower) ||
        !password.Any(char.IsDigit) ||
        !password.Any(character => !char.IsLetterOrDigit(character)))
    {
        errors[key] = ["Password must include uppercase, lowercase, number, and special character."];
    }
}

static bool IsValidEmail(string email)
{
    return Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
}

static bool IsUniqueViolation(DbUpdateException exception)
{
    return exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

static string NormalizeName(string value)
{
    return Regex.Replace(value.Trim(), @"\s+", " ");
}

static string NormalizeMasterName(string value)
{
    return NormalizeName(value).ToUpperInvariant();
}

static string NormalizeEmail(string value)
{
    return value.Trim().ToLowerInvariant();
}

static string NormalizeSlug(string value)
{
    return Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9-]", "-").Trim('-');
}

static string? NormalizeOptionalText(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? null : Regex.Replace(value.Trim(), @"\s+", " ");
}

static string NormalizePhone(string value)
{
    return Regex.Replace(value, @"\D", "");
}

static string NormalizePhoneDisplay(string value)
{
    return value.Trim();
}

static string NormalizePriority(string? value)
{
    var priority = NormalizeOptionalText(value) ?? "Medium";
    return priority is "Low" or "Medium" or "High" or "Urgent" ? priority : "Medium";
}

static string NormalizeFollowUpType(string? value)
{
    var type = NormalizeOptionalText(value) ?? "Call";
    return type is "Call" or "WhatsApp" or "Email" or "Walk-in" ? type : "Call";
}

static string NormalizeActivityType(string? value)
{
    var type = NormalizeOptionalText(value) ?? "Note";
    return type is "Note" or "Call" or "WhatsApp" or "Email" or "Meeting" ? type : "Note";
}

static async Task<string> GenerateLeadNumberAsync(AppDbContext db, Guid tenantId, CancellationToken cancellationToken)
{
    var latestLeadNumber = await db.Leads
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.LeadNumber.StartsWith("LD-"))
        .OrderByDescending(item => item.CreatedAt)
        .ThenByDescending(item => item.LeadNumber)
        .Select(item => item.LeadNumber)
        .FirstOrDefaultAsync(cancellationToken);

    var nextNumber = 1001;
    if (!string.IsNullOrWhiteSpace(latestLeadNumber) &&
        int.TryParse(latestLeadNumber.Replace("LD-", "", StringComparison.OrdinalIgnoreCase), out var latestNumber))
    {
        nextNumber = latestNumber + 1;
    }

    return $"LD-{nextNumber}";
}

record DashboardSummary(
    int TotalLeads,
    int NewLeadsToday,
    int Contacted,
    int Enrolled,
    int PendingFollowUps,
    decimal ConversionRate);

record LoginRequest(
    string Email,
    string Password);

record ForgotPasswordRequest(
    string Email);

record PasswordResetRequestResponse(
    string Message);

record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);

record ResetUserPasswordRequest(
    string NewPassword,
    string ConfirmPassword);

record AuthResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    AuthenticatedUser User);

record CreateTenantRequest(
    string Name,
    string Slug,
    string BranchName,
    string City,
    string AdminFullName,
    string AdminEmail,
    string AdminPassword);

record TenantListItemResponse(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTimeOffset CreatedAt,
    int ActiveUsers,
    int Leads);

record TenantCreatedResponse(
    Guid Id,
    string Name,
    string Slug,
    string AdminEmail);

record MasterDataResponse(
    IReadOnlyCollection<BranchMasterResponse> Branches,
    IReadOnlyCollection<NamedMasterResponse> Courses,
    IReadOnlyCollection<NamedMasterResponse> Sources,
    IReadOnlyCollection<LeadStageMasterResponse> Stages);

record BranchMasterResponse(
    Guid Id,
    string Name,
    string City,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int ActiveUsers,
    int Leads);

record NamedMasterResponse(
    Guid Id,
    string Name,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Leads);

record LeadStageMasterResponse(
    Guid Id,
    string Name,
    int SortOrder,
    bool IsActive,
    bool IsDefaultStage,
    bool IsWonStage,
    bool IsLostStage,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Leads);

record MasterMutationResponse(Guid Id, string Name, int Version, string Message);
record CreateBranchMasterRequest(string Name, string City);
record UpdateBranchMasterRequest(string Name, string City, bool IsActive, int Version);
record CreateNamedMasterRequest(string Name);
record UpdateNamedMasterRequest(string Name, bool IsActive, int Version);
record CreateLeadStageMasterRequest(string Name, bool IsDefaultStage, bool IsWonStage, bool IsLostStage);
record UpdateLeadStageMasterRequest(string Name, bool IsActive, bool IsDefaultStage, bool IsWonStage, bool IsLostStage, int Version);
record ReorderLeadStageItem(Guid Id, int Version);
record ReorderLeadStagesRequest(IReadOnlyCollection<ReorderLeadStageItem> Items);

record UserResponse(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    Guid? BranchId,
    string? Branch,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

record CreateUserRequest(
    string FullName,
    string Email,
    string Role,
    Guid? BranchId,
    string Password);

record UpdateUserRequest(
    string FullName,
    string Role,
    Guid? BranchId,
    bool IsActive,
    string? Password);

record TokenClaims(Guid UserId, Guid TenantId);

record LeadAccessScope(bool CanViewAll, Guid? BranchId);

record AccessTokenPayload(
    string Sub,
    string Tid,
    string TenantSlug,
    string TenantName,
    string Name,
    string Email,
    string Role,
    long Exp);

record LeadResponse(
    string Id,
    string StudentName,
    string Email,
    string Phone,
    string Source,
    string Course,
    string Counselor,
    string Status,
    string Priority,
    string Stage,
    string? Branch,
    Guid? AssignedUserId,
    Guid LeadStageId,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset? NextFollowUpAt);

record LeadListResponse(
    IReadOnlyCollection<LeadResponse> Items,
    int Page,
    int PageSize,
    int Total);

record LeadDetailResponse(
    string Id,
    string StudentName,
    string? GuardianName,
    string Email,
    string Phone,
    string? City,
    Guid? BranchId,
    string? Branch,
    Guid CourseId,
    string Course,
    Guid LeadSourceId,
    string Source,
    Guid LeadStageId,
    string Stage,
    Guid? AssignedUserId,
    string Counselor,
    string Status,
    string Priority,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset? NextFollowUpAt,
    IReadOnlyCollection<FollowUpResponse> FollowUps,
    IReadOnlyCollection<ActivityResponse> Activities);

record FollowUpResponse(
    string Id,
    string LeadId,
    string StudentName,
    string Type,
    string Priority,
    string Status,
    int Version,
    DateTimeOffset DueAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string AssignedTo);

record ActivityResponse(
    string Id,
    string Type,
    string Description,
    string CreatedBy,
    DateTimeOffset CreatedAt);

record PipelineStageResponse(string Name, int Count, IReadOnlyCollection<LeadResponse> Leads);

record FunnelStep(string Name, int Count, decimal Percentage);

record LookupOption(Guid Id, string Name);

record LeadOptionsResponse(
    IReadOnlyCollection<LookupOption> Branches,
    IReadOnlyCollection<LookupOption> Courses,
    IReadOnlyCollection<LookupOption> Sources,
    IReadOnlyCollection<LookupOption> Stages,
    IReadOnlyCollection<LookupOption> Counselors);

record CreateLeadRequest(
    string StudentName,
    string? GuardianName,
    string Email,
    string Phone,
    string? City,
    Guid CourseId,
    Guid LeadSourceId,
    Guid LeadStageId,
    Guid? BranchId,
    Guid? AssignedUserId,
    string? Status,
    string? Priority,
    DateTimeOffset? NextFollowUpAt);

record UpdateLeadRequest(
    string StudentName,
    string? GuardianName,
    string Email,
    string Phone,
    string? City,
    Guid CourseId,
    Guid LeadSourceId,
    Guid LeadStageId,
    Guid? BranchId,
    Guid? AssignedUserId,
    string? Status,
    string? Priority,
    DateTimeOffset? NextFollowUpAt,
    int Version);

record AssignLeadRequest(Guid? AssignedUserId, int Version);
record UpdateLeadStageRequest(Guid LeadStageId, string? Status, int Version);
record LeadVersionRequest(int Version);

record AddLeadActivityRequest(
    string Description,
    string? Type);

record CreateFollowUpRequest(
    string? Type,
    string? Priority,
    Guid? AssignedUserId,
    DateTimeOffset DueAt);

record RescheduleFollowUpRequest(
    string? Type,
    string? Priority,
    Guid? AssignedUserId,
    DateTimeOffset DueAt,
    int Version);

record FollowUpVersionRequest(int Version);
