using EducationCrm.Api.Data;
using EducationCrm.Api.Models;
using EducationCrm.Api.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(DatabaseConnection.GetConnectionString(builder.Configuration));
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = Math.Max(LeadFileService.MaximumFileBytes, LeadDocumentFileRules.MaximumFileBytes) + 256 * 1024;
    options.ValueLengthLimit = 64 * 1024;
    options.MultipartHeadersLengthLimit = 16 * 1024;
});
builder.Services.AddHttpClient<ILeadDocumentStorage, CloudinaryLeadDocumentStorage>();
builder.Services.AddScoped<ReminderNotificationJob>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

var databaseConnectionString = DatabaseConnection.GetConnectionString(builder.Configuration);
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(
        options => options.UseNpgsqlConnection(databaseConnectionString),
        new PostgreSqlStorageOptions
        {
            SchemaName = "hangfire",
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(15),
            InvisibilityTimeout = TimeSpan.FromMinutes(10),
            DistributedLockTimeout = TimeSpan.FromMinutes(5)
        }));
builder.Services.AddHangfireServer(options =>
{
    options.ServerName = $"counselmate-{Environment.MachineName}";
    options.WorkerCount = Math.Clamp(Environment.ProcessorCount, 1, 4);
    options.Queues = ["notifications", "default"];
    options.ServerTimeout = TimeSpan.FromMinutes(5);
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}
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
            item.Tenant.LogoUrl,
            item.Tenant.BrandColor,
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

var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
recurringJobs.AddOrUpdate<ReminderNotificationJob>(
    "counselmate-reminder-scan",
    job => job.ProcessAsync(CancellationToken.None),
    builder.Configuration["Automation:ReminderCron"] ?? "*/5 * * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

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
        user.Tenant.LogoUrl,
        user.Tenant.BrandColor,
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

api.MapGet("/notifications", async (
    int? page,
    int? pageSize,
    bool? unreadOnly,
    string? type,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var requestedPage = page ?? 1;
    var requestedPageSize = pageSize ?? 20;
    if (requestedPage < 1 || requestedPageSize is < 1 or > 100)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["pagination"] = ["Page must be at least 1 and pageSize must be between 1 and 100."]
        });
    }

    var now = IndianClock.Now();
    var query = db.Notifications
        .AsNoTracking()
        .Where(item => item.TenantId == currentUser.TenantId &&
            item.RecipientUserId == currentUser.UserId &&
            item.DismissedAt == null &&
            (item.ExpiresAt == null || item.ExpiresAt > now));

    if (unreadOnly == true)
    {
        query = query.Where(item => item.ReadAt == null);
    }

    var normalizedType = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
    if (normalizedType is not null)
    {
        query = query.Where(item => item.Type == normalizedType);
    }

    var total = await query.CountAsync(cancellationToken);
    var items = await query
        .OrderByDescending(item => item.CreatedAt)
        .ThenByDescending(item => item.Id)
        .Skip((requestedPage - 1) * requestedPageSize)
        .Take(requestedPageSize)
        .Select(item => new
        {
            id = item.Id,
            item.Type,
            item.Title,
            item.Message,
            item.Severity,
            item.ScheduledFor,
            item.CreatedAt,
            item.ReadAt,
            leadId = item.LeadId,
            followUpId = item.FollowUpId,
            paymentId = item.LeadPaymentId
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        items,
        page = requestedPage,
        pageSize = requestedPageSize,
        total,
        unreadCount = await query.CountAsync(item => item.ReadAt == null, cancellationToken)
    });
});

api.MapGet("/notifications/unread-count", async (
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var now = IndianClock.Now();
    var count = await db.Notifications.AsNoTracking().CountAsync(item =>
        item.TenantId == currentUser.TenantId &&
        item.RecipientUserId == currentUser.UserId &&
        item.ReadAt == null &&
        item.DismissedAt == null &&
        (item.ExpiresAt == null || item.ExpiresAt > now), cancellationToken);
    return Results.Ok(new { count });
});

api.MapPost("/notifications/{notificationId:guid}/read", async (
    Guid notificationId,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var notification = await db.Notifications.FirstOrDefaultAsync(item =>
        item.Id == notificationId &&
        item.TenantId == currentUser.TenantId &&
        item.RecipientUserId == currentUser.UserId &&
        item.DismissedAt == null, cancellationToken);
    if (notification is null)
    {
        return Results.NotFound(new { message = "Notification was not found." });
    }

    notification.ReadAt ??= IndianClock.Now();
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { notification.Id, notification.ReadAt });
});

api.MapPost("/notifications/read-all", async (
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var now = IndianClock.Now();
    var updated = await db.Notifications
        .Where(item => item.TenantId == currentUser.TenantId &&
            item.RecipientUserId == currentUser.UserId &&
            item.ReadAt == null &&
            item.DismissedAt == null)
        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAt, now), cancellationToken);
    return Results.Ok(new { updated, readAt = now });
});

api.MapGet("/notification-preferences", async (
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var preference = await db.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(item =>
        item.TenantId == currentUser.TenantId && item.UserId == currentUser.UserId, cancellationToken);
    return Results.Ok(new
    {
        followUpRemindersEnabled = preference?.FollowUpRemindersEnabled ?? true,
        paymentRemindersEnabled = preference?.PaymentRemindersEnabled ?? true,
        version = preference?.Version ?? 0
    });
});

api.MapPut("/notification-preferences", async (
    NotificationPreferenceRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var preference = await db.NotificationPreferences.FirstOrDefaultAsync(item =>
        item.TenantId == currentUser.TenantId && item.UserId == currentUser.UserId, cancellationToken);
    var now = IndianClock.Now();
    if (preference is null)
    {
        if (request.Version != 0)
        {
            return Results.Conflict(new { message = "Notification preferences changed. Refresh and try again." });
        }

        preference = new NotificationPreference
        {
            Id = Guid.NewGuid(),
            TenantId = currentUser.TenantId,
            UserId = currentUser.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        db.NotificationPreferences.Add(preference);
    }
    else if (request.Version != preference.Version)
    {
        return Results.Conflict(new { message = "Notification preferences changed. Refresh and try again." });
    }

    preference.FollowUpRemindersEnabled = request.FollowUpRemindersEnabled;
    preference.PaymentRemindersEnabled = request.PaymentRemindersEnabled;
    preference.UpdatedAt = now;
    if (request.Version > 0)
    {
        preference.Version += 1;
    }
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(new
    {
        preference.FollowUpRemindersEnabled,
        preference.PaymentRemindersEnabled,
        preference.Version
    });
});

api.MapGet("/automation/status", async (
    HttpContext httpContext,
    AppDbContext db,
    JobStorage jobStorage,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (currentUser.Role is not ("Owner" or "Admin"))
    {
        return Results.Json(new { message = "Only owners and admins can view automation status." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var monitoring = jobStorage.GetMonitoringApi();
    var statistics = monitoring.GetStatistics();
    var since = IndianClock.Now().AddHours(-24);
    var tenantNotifications = await db.Notifications.AsNoTracking().CountAsync(item =>
        item.TenantId == currentUser.TenantId && item.CreatedAt >= since, cancellationToken);
    var tenantFailures = await db.NotificationDeliveryAttempts.AsNoTracking().CountAsync(item =>
        item.TenantId == currentUser.TenantId && item.AttemptedAt >= since && item.Status == "Failed", cancellationToken);

    return Results.Ok(new
    {
        scheduler = "Hangfire",
        recurringJob = "counselmate-reminder-scan",
        cron = builder.Configuration["Automation:ReminderCron"] ?? "*/5 * * * *",
        statistics.Enqueued,
        statistics.Processing,
        statistics.Scheduled,
        statistics.Failed,
        notificationsCreatedLast24Hours = tenantNotifications,
        deliveryFailuresLast24Hours = tenantFailures,
        externalChannelsEnabled = false,
        checkedAt = IndianClock.Now()
    });
});

api.MapPost("/automation/reminders/run", (
    HttpContext httpContext,
    IBackgroundJobClient backgroundJobs) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (currentUser.Role is not ("Owner" or "Admin"))
    {
        return Results.Json(new { message = "Only owners and admins can start reminder scans." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var jobId = backgroundJobs.Enqueue<ReminderNotificationJob>(job => job.ProcessAsync(CancellationToken.None));
    return Results.Accepted(value: new { jobId, queuedAt = IndianClock.Now() });
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
        ContactEmail = adminEmail,
        City = NormalizeName(request.City),
        Country = "India",
        TimeZone = "Asia/Kolkata",
        BrandColor = "#2171D3",
        DefaultBranchId = branchId,
        DefaultAssigneeUserId = adminUserId,
        IsActive = true,
        Version = 1,
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
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can view users." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var users = await db.Users
        .AsNoTracking()
        .Where(user => user.TenantId == currentUser!.TenantId)
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
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can view master data settings." }, statusCode: StatusCodes.Status403Forbidden);
    }

    return Results.Ok(await GetMasterDataResponseAsync(db, currentUser!.TenantId, cancellationToken));
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
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageTenantProfile(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can view the institute profile." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var tenant = await db.Tenants
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == currentUser!.TenantId && item.IsActive, cancellationToken);

    return tenant is null
        ? Results.NotFound(new { message = "Tenant not found." })
        : Results.Ok(ToTenantProfileResponse(tenant));
});

api.MapPatch("/tenants/current", async (
    UpdateTenantProfileRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageTenantProfile(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can update the institute profile." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var errors = ValidateTenantProfileRequest(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var tenant = await db.Tenants
        .FirstOrDefaultAsync(item => item.Id == currentUser!.TenantId && item.IsActive, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }
    if (tenant.Version != request.Version)
    {
        return Results.Conflict(new { message = "The institute profile was changed by another user. Refresh before saving again." });
    }

    Branch? defaultBranch = null;
    if (request.DefaultBranchId is not null)
    {
        defaultBranch = await db.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.DefaultBranchId && item.TenantId == tenant.Id && item.IsActive, cancellationToken);
        if (defaultBranch is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["defaultBranchId"] = ["Select a valid active branch from this institute."]
            });
        }
    }

    AppUser? defaultAssignee = null;
    if (request.DefaultAssigneeUserId is not null)
    {
        defaultAssignee = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.DefaultAssigneeUserId && item.TenantId == tenant.Id && item.IsActive, cancellationToken);
        if (defaultAssignee is null || defaultAssignee.Role is UserRole.Accountant or UserRole.ReadOnly)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["defaultAssigneeUserId"] = ["Select an active CRM user from this institute."]
            });
        }
        if (defaultBranch is not null && defaultAssignee.BranchId is not null && defaultAssignee.BranchId != defaultBranch.Id)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["defaultAssigneeUserId"] = ["The default assignee must belong to the selected default branch."]
            });
        }
    }

    tenant.Name = NormalizeName(request.Name);
    tenant.ContactEmail = NormalizeOptionalText(request.ContactEmail)?.ToLowerInvariant();
    tenant.ContactPhone = NormalizeOptionalText(request.ContactPhone);
    tenant.WebsiteUrl = NormalizeOptionalText(request.WebsiteUrl);
    tenant.AddressLine1 = NormalizeOptionalText(request.AddressLine1);
    tenant.AddressLine2 = NormalizeOptionalText(request.AddressLine2);
    tenant.City = NormalizeOptionalText(request.City);
    tenant.State = NormalizeOptionalText(request.State);
    tenant.PostalCode = NormalizeOptionalText(request.PostalCode);
    tenant.Country = NormalizeName(request.Country);
    tenant.TimeZone = NormalizeName(request.TimeZone);
    tenant.LogoUrl = NormalizeOptionalText(request.LogoUrl);
    tenant.BrandColor = request.BrandColor.Trim().ToUpperInvariant();
    tenant.DefaultBranchId = request.DefaultBranchId;
    tenant.DefaultAssigneeUserId = request.DefaultAssigneeUserId;
    tenant.Version += 1;
    tenant.UpdatedAt = IndianClock.Now();

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(new { message = "The institute profile was changed by another user. Refresh before saving again." });
    }

    return Results.Ok(ToTenantProfileResponse(tenant));
});

api.MapGet("/lead-intelligence/settings", async (
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageTenantProfile(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can view lead intelligence settings." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var now = IndianClock.Now();
    var settings = await GetOrCreateLeadIntelligenceSettingsAsync(db, currentUser!.TenantId, now, cancellationToken);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(await ToLeadIntelligenceSettingsResponseAsync(db, settings, cancellationToken));
});

api.MapPatch("/lead-intelligence/settings", async (
    UpdateLeadIntelligenceSettingsRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageTenantProfile(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can update lead intelligence settings." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var errors = ValidateLeadIntelligenceSettingsRequest(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var now = IndianClock.Now();
    var settings = await GetOrCreateLeadIntelligenceSettingsAsync(db, currentUser!.TenantId, now, cancellationToken);
    if (settings.Version != request.Version)
    {
        return Results.Conflict(new { message = "Lead intelligence settings changed. Refresh before saving again." });
    }

    settings.ScoringEnabled = request.ScoringEnabled;
    settings.DistributionEnabled = request.DistributionEnabled;
    settings.DefaultDistributionStrategy = NormalizeDistributionStrategy(request.DefaultDistributionStrategy);
    settings.PriorityWeight = request.PriorityWeight;
    settings.SourceWeight = request.SourceWeight;
    settings.ResponseWeight = request.ResponseWeight;
    settings.EngagementWeight = request.EngagementWeight;
    settings.FreshnessWeight = request.FreshnessWeight;
    settings.ProfileWeight = request.ProfileWeight;
    settings.HotThreshold = request.HotThreshold;
    settings.WarmThreshold = request.WarmThreshold;
    settings.MaxActiveLeadsPerUser = request.MaxActiveLeadsPerUser;
    settings.Version += 1;
    settings.UpdatedAt = now;

    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "Lead intelligence settings changed. Refresh before saving again." }); }
    return Results.Ok(await ToLeadIntelligenceSettingsResponseAsync(db, settings, cancellationToken));
});

api.MapPost("/lead-intelligence/rules", async (
    SaveLeadDistributionRuleRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageTenantProfile(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can create distribution rules." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var errors = await ValidateLeadDistributionRuleRequestAsync(db, currentUser!.TenantId, request, requireVersion: false, cancellationToken);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var now = IndianClock.Now();
    var rule = new LeadDistributionRule
    {
        Id = Guid.NewGuid(),
        TenantId = currentUser.TenantId,
        Name = NormalizeName(request.Name),
        IsActive = request.IsActive,
        PriorityOrder = request.PriorityOrder,
        Strategy = NormalizeDistributionStrategy(request.Strategy),
        BranchId = request.BranchId,
        CourseId = request.CourseId,
        LeadSourceId = request.LeadSourceId,
        TargetUserIds = string.Join(",", request.TargetUserIds.Distinct()),
        CreatedAt = now,
        UpdatedAt = now
    };
    db.LeadDistributionRules.Add(rule);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/lead-intelligence/rules/{rule.Id}", ToLeadDistributionRuleResponse(rule));
});

api.MapPatch("/lead-intelligence/rules/{id:guid}", async (
    Guid id,
    SaveLeadDistributionRuleRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageTenantProfile(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can update distribution rules." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var errors = await ValidateLeadDistributionRuleRequestAsync(db, currentUser!.TenantId, request, requireVersion: true, cancellationToken);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var rule = await db.LeadDistributionRules.FirstOrDefaultAsync(item => item.TenantId == currentUser.TenantId && item.Id == id, cancellationToken);
    if (rule is null)
    {
        return Results.NotFound(new { message = "Distribution rule not found." });
    }
    if (rule.Version != request.Version)
    {
        return Results.Conflict(new { message = "Distribution rule changed. Refresh before saving again." });
    }

    rule.Name = NormalizeName(request.Name);
    rule.IsActive = request.IsActive;
    rule.PriorityOrder = request.PriorityOrder;
    rule.Strategy = NormalizeDistributionStrategy(request.Strategy);
    rule.BranchId = request.BranchId;
    rule.CourseId = request.CourseId;
    rule.LeadSourceId = request.LeadSourceId;
    rule.TargetUserIds = string.Join(",", request.TargetUserIds.Distinct());
    rule.Version += 1;
    rule.UpdatedAt = IndianClock.Now();

    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "Distribution rule changed. Refresh before saving again." }); }
    return Results.Ok(ToLeadDistributionRuleResponse(rule));
});

api.MapPost("/lead-intelligence/recalculate", async (
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageTenantProfile(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can recalculate lead scores." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var now = IndianClock.Now();
    var settings = await GetOrCreateLeadIntelligenceSettingsAsync(db, currentUser!.TenantId, now, cancellationToken);
    var leads = await db.Leads
        .Where(item => item.TenantId == currentUser.TenantId && item.ArchivedAt == null)
        .ToListAsync(cancellationToken);
    foreach (var lead in leads)
    {
        await ApplyLeadScoringAsync(db, lead, settings, "Manual recalculation", now, cancellationToken);
    }
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(new LeadIntelligenceRecalculateResponse(leads.Count, $"Recalculated {leads.Count} active lead score{(leads.Count == 1 ? string.Empty : "s")}."));
});

api.MapGet("/communication-templates", async (
    string? status,
    string? channel,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication token is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var statusMode = NormalizeOptionalText(status)?.ToLowerInvariant() ?? "active";
    if (statusMode != "active" && !CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can view inactive communication templates." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var normalizedChannel = NormalizeOptionalText(channel);
    var query = db.CommunicationTemplates
        .AsNoTracking()
        .Where(item => item.TenantId == currentUser.TenantId);

    query = statusMode switch
    {
        "all" => query,
        "inactive" => query.Where(item => !item.IsActive),
        _ => query.Where(item => item.IsActive)
    };

    if (!string.IsNullOrWhiteSpace(normalizedChannel))
    {
        var channelFilter = NormalizeTemplateChannel(normalizedChannel);
        query = query.Where(item => item.Channel == channelFilter);
    }

    var templates = await query
        .OrderBy(item => item.Channel)
        .ThenBy(item => item.Category)
        .ThenBy(item => item.Name)
        .Select(item => new CommunicationTemplateResponse(
            item.Id,
            item.Name,
            item.Channel,
            item.Category,
            item.Body,
            item.IsActive,
            item.Version,
            item.CreatedAt,
            item.UpdatedAt))
        .ToListAsync(cancellationToken);

    return Results.Ok(templates);
});

api.MapPost("/communication-templates", async (
    SaveCommunicationTemplateRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can manage communication templates." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var errors = ValidateCommunicationTemplateRequest(request, requireVersion: false);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var normalizedName = NormalizeMasterName(request.Name);
    if (await db.CommunicationTemplates.AnyAsync(item => item.TenantId == currentUser!.TenantId && item.NormalizedName == normalizedName, cancellationToken))
    {
        return Results.Conflict(new { message = "A communication template with this name already exists." });
    }

    var now = IndianClock.Now();
    var template = new CommunicationTemplate
    {
        Id = Guid.NewGuid(),
        TenantId = currentUser!.TenantId,
        CreatedByUserId = currentUser.UserId,
        UpdatedByUserId = currentUser.UserId,
        Name = NormalizeName(request.Name),
        NormalizedName = normalizedName,
        Channel = NormalizeTemplateChannel(request.Channel),
        Category = NormalizeName(request.Category),
        Body = NormalizeTemplateBody(request.Body),
        IsActive = true,
        Version = 1,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.CommunicationTemplates.Add(template);

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception))
    {
        return Results.Conflict(new { message = "A communication template with this name already exists." });
    }

    return Results.Created($"/api/communication-templates/{template.Id}", ToCommunicationTemplateResponse(template));
});

api.MapPatch("/communication-templates/{id:guid}", async (
    Guid id,
    SaveCommunicationTemplateRequest request,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can manage communication templates." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var errors = ValidateCommunicationTemplateRequest(request, requireVersion: true);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var template = await db.CommunicationTemplates.FirstOrDefaultAsync(item => item.TenantId == currentUser!.TenantId && item.Id == id, cancellationToken);
    if (template is null)
    {
        return Results.NotFound(new { message = "Communication template not found." });
    }
    if (template.Version != request.Version)
    {
        return Results.Conflict(new { message = "This communication template was changed by another user. Refresh and try again." });
    }

    var normalizedName = NormalizeMasterName(request.Name);
    if (await db.CommunicationTemplates.AnyAsync(item => item.TenantId == currentUser!.TenantId && item.Id != id && item.NormalizedName == normalizedName, cancellationToken))
    {
        return Results.Conflict(new { message = "A communication template with this name already exists." });
    }

    template.Name = NormalizeName(request.Name);
    template.NormalizedName = normalizedName;
    template.Channel = NormalizeTemplateChannel(request.Channel);
    template.Category = NormalizeName(request.Category);
    template.Body = NormalizeTemplateBody(request.Body);
    template.IsActive = request.IsActive;
    template.Version += 1;
    template.UpdatedAt = IndianClock.Now();
    template.UpdatedByUserId = currentUser!.UserId;

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(new { message = "This communication template was changed by another user. Refresh and try again." });
    }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception))
    {
        return Results.Conflict(new { message = "A communication template with this name already exists." });
    }

    return Results.Ok(ToCommunicationTemplateResponse(template));
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

api.MapGet("/dashboard/advanced", async (
    string? startDate,
    string? endDate,
    Guid? branchId,
    Guid? courseId,
    Guid? assignedUserId,
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

    var range = ResolveReportDateRange(startDate, endDate);
    if (range.Errors.Count > 0)
    {
        return Results.ValidationProblem(range.Errors);
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var dashboard = await BuildAdvancedDashboardAsync(
        db,
        tenant.TenantId,
        currentUser,
        accessScope,
        range.Range!,
        branchId,
        courseId,
        assignedUserId,
        cancellationToken);

    return Results.Ok(dashboard);
});

api.MapGet("/reports", async (
    string? startDate,
    string? endDate,
    Guid? branchId,
    Guid? courseId,
    Guid? sourceId,
    Guid? assignedUserId,
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

    var range = ResolveReportDateRange(startDate, endDate);
    if (range.Errors.Count > 0)
    {
        return Results.ValidationProblem(range.Errors);
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var report = await BuildReportsAsync(
        db,
        tenant.TenantId,
        currentUser,
        accessScope,
        range.Range!,
        branchId,
        courseId,
        sourceId,
        assignedUserId,
        cancellationToken);

    return Results.Ok(report);
});

api.MapGet("/reports/export", async (
    string? startDate,
    string? endDate,
    Guid? branchId,
    Guid? courseId,
    Guid? sourceId,
    Guid? assignedUserId,
    string? format,
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
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can export reports." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var range = ResolveReportDateRange(startDate, endDate);
    if (range.Errors.Count > 0)
    {
        return Results.ValidationProblem(range.Errors);
    }

    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var report = await BuildReportsAsync(
        db,
        tenant.TenantId,
        currentUser,
        accessScope,
        range.Range!,
        branchId,
        courseId,
        sourceId,
        assignedUserId,
        cancellationToken);
    var rows = BuildReportExportRows(report);

    var normalizedFormat = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? "csv" : "xlsx";
    var timestamp = IndianClock.Now().ToString("yyyyMMdd-HHmm");
    if (normalizedFormat == "csv")
    {
        return Results.File(
            ReportFileService.CreateCsv(rows),
            "text/csv; charset=utf-8",
            $"reports-{timestamp}.csv");
    }

    return Results.File(
        ReportFileService.CreateWorkbook(rows),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"reports-{timestamp}.xlsx");
});

api.MapGet("/reports/counsellor-workspace", async (
    string? startDate,
    string? endDate,
    Guid? courseId,
    Guid? sourceId,
    HttpContext httpContext,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Authentication is required." }, statusCode: StatusCodes.Status401Unauthorized);
    }
    if (currentUser.Role is not (nameof(UserRole.Counselor) or nameof(UserRole.Telecaller)))
    {
        return Results.Json(new { message = "This workspace is available only to counsellor roles." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var rangeResult = ResolveReportDateRange(startDate, endDate);
    if (rangeResult.Errors.Count > 0) return Results.ValidationProblem(rangeResult.Errors);
    var range = rangeResult.Range!;
    var now = IndianClock.Now();
    var todayStart = new DateTimeOffset(now.Date, IndianClock.Offset);
    var tomorrowStart = todayStart.AddDays(1);
    var staleCutoff = now.AddDays(-3);
    var stageStuckCutoff = now.AddDays(-7);

    var portfolio = db.Leads.AsNoTracking().Where(lead =>
        lead.TenantId == currentUser.TenantId &&
        lead.AssignedUserId == currentUser.UserId &&
        lead.ArchivedAt == null);
    if (courseId is not null) portfolio = portfolio.Where(lead => lead.CourseId == courseId);
    if (sourceId is not null) portfolio = portfolio.Where(lead => lead.LeadSourceId == sourceId);

    var openPortfolio = portfolio.Where(lead => !lead.LeadStage.IsWonStage && !lead.LeadStage.IsLostStage);
    var followUps = db.FollowUps.AsNoTracking().Where(item =>
        item.TenantId == currentUser.TenantId &&
        item.AssignedUserId == currentUser.UserId &&
        item.Lead.ArchivedAt == null);
    if (courseId is not null) followUps = followUps.Where(item => item.Lead.CourseId == courseId);
    if (sourceId is not null) followUps = followUps.Where(item => item.Lead.LeadSourceId == sourceId);

    var overdueLeadIds = followUps
        .Where(item => item.Status == "Scheduled" && item.DueAt < now)
        .Select(item => item.LeadId)
        .Distinct();
    var dueTodayLeadIds = followUps
        .Where(item => item.Status == "Scheduled" && item.DueAt >= todayStart && item.DueAt < tomorrowStart)
        .Select(item => item.LeadId)
        .Distinct();
    var untouched = openPortfolio.Where(lead =>
        lead.CreatedAt < now.AddHours(-24) &&
        !lead.Activities.Any(activity => activity.Type != "LeadCreated"));
    var stale = openPortfolio.Where(lead =>
        lead.CreatedAt < staleCutoff &&
        !lead.Activities.Any(activity => activity.Type != "LeadCreated" && activity.CreatedAt >= staleCutoff));
    var noNextAction = openPortfolio.Where(lead => lead.NextFollowUpAt == null);
    var highPriorityNoAction = noNextAction.Where(lead => lead.Priority == "High" || lead.Priority == "Urgent");

    async Task<CounsellorAttentionGroup> LoadAttentionAsync(
        string key,
        string title,
        string guidance,
        IQueryable<Lead> query)
    {
        var count = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(lead => lead.Priority == "Urgent" ? 0 : lead.Priority == "High" ? 1 : lead.Priority == "Medium" ? 2 : 3)
            .ThenBy(lead => lead.NextFollowUpAt == null)
            .ThenBy(lead => lead.NextFollowUpAt)
            .ThenBy(lead => lead.StudentName)
            .Select(lead => new
            {
                lead.LeadNumber,
                lead.StudentName,
                Course = lead.Course.Name,
                Stage = lead.LeadStage.Name,
                lead.Priority,
                lead.NextFollowUpAt,
                LastActivityAt = lead.Activities
                    .Where(activity => activity.Type != "LeadCreated")
                    .Select(activity => (DateTimeOffset?)activity.CreatedAt)
                    .Max() ?? lead.CreatedAt
            })
            .Take(8)
            .ToListAsync(cancellationToken);
        var items = rows
            .Select(lead => new CounsellorAttentionLead(
                lead.LeadNumber,
                lead.StudentName,
                lead.Course,
                lead.Stage,
                lead.Priority,
                lead.NextFollowUpAt,
                lead.LastActivityAt))
            .ToList();
        return new CounsellorAttentionGroup(key, title, guidance, count, items);
    }

    var attention = new[]
    {
        await LoadAttentionAsync("overdue", "Overdue follow-ups", "Contact these students first.", openPortfolio.Where(lead => overdueLeadIds.Contains(lead.Id))),
        await LoadAttentionAsync("dueToday", "Due today", "Complete today's planned conversations.", openPortfolio.Where(lead => dueTodayLeadIds.Contains(lead.Id))),
        await LoadAttentionAsync("untouched", "Untouched leads", "No meaningful activity after 24 hours.", untouched),
        await LoadAttentionAsync("highPriorityNoAction", "High priority without next action", "Schedule the next step before these leads go cold.", highPriorityNoAction),
        await LoadAttentionAsync("stale", "Stale leads", "No meaningful activity in the last 3 days.", stale),
        await LoadAttentionAsync("noNextAction", "No next action", "Every open lead should have a clear next step.", noNextAction)
    };

    var pipelineRows = await portfolio
        .GroupBy(lead => new { lead.LeadStageId, lead.LeadStage.Name, lead.LeadStage.SortOrder, lead.LeadStage.IsWonStage, lead.LeadStage.IsLostStage })
        .Select(group => new
        {
            StageId = group.Key.LeadStageId,
            Stage = group.Key.Name,
            group.Key.SortOrder,
            TotalLeads = group.Count(),
            group.Key.IsWonStage,
            group.Key.IsLostStage
        })
        .ToListAsync(cancellationToken);
    var stuckLeadsByStage = await portfolio
        .Where(lead =>
            lead.CreatedAt < stageStuckCutoff &&
            !lead.Activities.Any(activity => activity.Type == "StageChanged" && activity.CreatedAt >= stageStuckCutoff))
        .GroupBy(lead => lead.LeadStageId)
        .Select(group => new { StageId = group.Key, Count = group.Count() })
        .ToDictionaryAsync(item => item.StageId, item => item.Count, cancellationToken);
    var pipeline = pipelineRows
        .Select(item => new CounsellorPipelineInsight(
            item.StageId,
            item.Stage,
            item.SortOrder,
            item.TotalLeads,
            stuckLeadsByStage.GetValueOrDefault(item.StageId),
            item.IsWonStage,
            item.IsLostStage))
        .OrderBy(item => item.SortOrder)
        .ToList();

    var followUpMetrics = await followUps
        .GroupBy(_ => 1)
        .Select(group => new
        {
            Scheduled = group.Count(item => item.DueAt >= range.Start && item.DueAt < range.EndExclusive),
            Completed = group.Count(item =>
                item.DueAt >= range.Start && item.DueAt < range.EndExclusive && item.Status == "Completed"),
            CompletedOnTime = group.Count(item =>
                item.DueAt >= range.Start && item.DueAt < range.EndExclusive &&
                item.Status == "Completed" && item.CompletedAt != null && item.CompletedAt <= item.DueAt),
            CompletedLate = group.Count(item =>
                item.DueAt >= range.Start && item.DueAt < range.EndExclusive &&
                item.Status == "Completed" && item.CompletedAt != null && item.CompletedAt > item.DueAt),
            Cancelled = group.Count(item =>
                item.DueAt >= range.Start && item.DueAt < range.EndExclusive && item.Status == "Cancelled"),
            CurrentlyOverdue = group.Count(item => item.Status == "Scheduled" && item.DueAt < now)
        })
        .SingleOrDefaultAsync(cancellationToken);
    var scheduledCount = followUpMetrics?.Scheduled ?? 0;
    var completedCount = followUpMetrics?.Completed ?? 0;
    var completedOnTime = followUpMetrics?.CompletedOnTime ?? 0;
    var completedLate = followUpMetrics?.CompletedLate ?? 0;
    var cancelledCount = followUpMetrics?.Cancelled ?? 0;
    var currentlyOverdue = followUpMetrics?.CurrentlyOverdue ?? 0;
    var completionRate = scheduledCount == 0 ? 0m : Math.Round((decimal)completedCount / scheduledCount * 100m, 1);

    var periodPortfolio = portfolio.Where(lead => lead.CreatedAt >= range.Start && lead.CreatedAt < range.EndExclusive);
    var newLeads = await periodPortfolio.CountAsync(cancellationToken);
    var wonLeads = await portfolio.CountAsync(lead =>
        lead.LeadStage.IsWonStage &&
        (lead.Activities.Where(activity => activity.Type == "StageChanged").Select(activity => (DateTimeOffset?)activity.CreatedAt).Max() ?? lead.CreatedAt) >= range.Start &&
        (lead.Activities.Where(activity => activity.Type == "StageChanged").Select(activity => (DateTimeOffset?)activity.CreatedAt).Max() ?? lead.CreatedAt) < range.EndExclusive,
        cancellationToken);
    var lostLeads = await portfolio.CountAsync(lead =>
        lead.LeadStage.IsLostStage &&
        (lead.Activities.Where(activity => activity.Type == "StageChanged").Select(activity => (DateTimeOffset?)activity.CreatedAt).Max() ?? lead.CreatedAt) >= range.Start &&
        (lead.Activities.Where(activity => activity.Type == "StageChanged").Select(activity => (DateTimeOffset?)activity.CreatedAt).Max() ?? lead.CreatedAt) < range.EndExclusive,
        cancellationToken);
    var openLeads = await openPortfolio.CountAsync(cancellationToken);
    var conversionRate = newLeads == 0 ? 0m : Math.Round((decimal)wonLeads / newLeads * 100m, 1);

    var courseStageRows = await periodPortfolio
        .GroupBy(lead => new
        {
            lead.CourseId,
            Course = lead.Course.Name,
            lead.LeadStage.IsWonStage,
            lead.LeadStage.IsLostStage
        })
        .Select(group => new
        {
            group.Key.CourseId,
            group.Key.Course,
            group.Key.IsWonStage,
            group.Key.IsLostStage,
            Count = group.Count()
        })
        .ToListAsync(cancellationToken);
    var courseInsights = courseStageRows
        .GroupBy(item => new { item.CourseId, item.Course })
        .Select(group => new CounsellorBreakdownInsight(
            group.Key.CourseId,
            group.Key.Course,
            group.Sum(item => item.Count),
            group.Where(item => item.IsWonStage).Sum(item => item.Count),
            group.Where(item => !item.IsWonStage && !item.IsLostStage).Sum(item => item.Count)))
        .OrderByDescending(item => item.TotalLeads)
        .ThenBy(item => item.Name)
        .Take(8)
        .ToList();
    var sourceStageRows = await periodPortfolio
        .GroupBy(lead => new
        {
            lead.LeadSourceId,
            Source = lead.LeadSource.Name,
            lead.LeadStage.IsWonStage,
            lead.LeadStage.IsLostStage
        })
        .Select(group => new
        {
            group.Key.LeadSourceId,
            group.Key.Source,
            group.Key.IsWonStage,
            group.Key.IsLostStage,
            Count = group.Count()
        })
        .ToListAsync(cancellationToken);
    var sourceInsights = sourceStageRows
        .GroupBy(item => new { item.LeadSourceId, item.Source })
        .Select(group => new CounsellorBreakdownInsight(
            group.Key.LeadSourceId,
            group.Key.Source,
            group.Sum(item => item.Count),
            group.Where(item => item.IsWonStage).Sum(item => item.Count),
            group.Where(item => !item.IsWonStage && !item.IsLostStage).Sum(item => item.Count)))
        .OrderByDescending(item => item.TotalLeads)
        .ThenBy(item => item.Name)
        .Take(8)
        .ToList();

    return Results.Ok(new CounsellorWorkspaceResponse(
        range.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        range.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        now,
        attention,
        pipeline,
        new CounsellorFollowUpInsight(scheduledCount, completedCount, completedOnTime, completedLate, cancelledCount, currentlyOverdue, completionRate),
        new CounsellorOutcomeInsight(newLeads, wonLeads, lostLeads, openLeads, conversionRate),
        courseInsights,
        sourceInsights));
});

api.MapGet("/leads", async (
    string? search,
    Guid? branchId,
    Guid? courseId,
    Guid? sourceId,
    Guid? stageId,
    Guid? assignedUserId,
    string? priority,
    string? temperature,
    int? minScore,
    int? maxScore,
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

    var query = ApplyLeadAccessScope(db.Leads.AsNoTracking().Where(lead => lead.TenantId == tenant.TenantId), currentUser, accessScope);
    query = ApplyLeadFilters(query, search, branchId, courseId, sourceId, stageId, assignedUserId, priority, temperature, minScore, maxScore, archive);
    query = ApplyLeadSort(query, sort);

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
            lead.NextFollowUpAt,
            lead.IntelligenceScore,
            lead.IntelligenceTemperature,
            lead.IntelligenceReason,
            lead.IntelligenceUpdatedAt))
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

    var defaults = await db.Tenants
        .AsNoTracking()
        .Where(item => item.Id == tenant.TenantId)
        .Select(item => new
        {
            DefaultBranchId = item.DefaultBranch != null && item.DefaultBranch.IsActive ? item.DefaultBranchId : null,
            DefaultAssigneeUserId = item.DefaultAssigneeUser != null &&
                item.DefaultAssigneeUser.IsActive &&
                item.DefaultAssigneeUser.Role != UserRole.Accountant &&
                item.DefaultAssigneeUser.Role != UserRole.ReadOnly
                    ? item.DefaultAssigneeUserId
                    : null
        })
        .FirstAsync(cancellationToken);

    return Results.Ok(new LeadOptionsResponse(
        branches, courses, sources, stages, counselors, defaults.DefaultBranchId, defaults.DefaultAssigneeUserId));
});

api.MapGet("/leads/import/template", (
    string? format,
    HttpContext httpContext) =>
{
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can import leads." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var normalizedFormat = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? "csv" : "xlsx";
    if (normalizedFormat == "csv")
    {
        return Results.File(
            LeadFileService.CreateImportTemplateCsv(),
            "text/csv; charset=utf-8",
            "lead-import-template.csv");
    }

    return Results.File(
        LeadFileService.CreateImportTemplateWorkbook(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "lead-import-template.xlsx");
});

api.MapPost("/leads/import/preview", async (
    HttpRequest request,
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
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can import leads." }, statusCode: StatusCodes.Status403Forbidden);
    }

    try
    {
        var importRequest = await ReadLeadImportFormAsync(request, requireFingerprint: false, cancellationToken);
        if (importRequest.Error is not null)
        {
            return Results.BadRequest(new { message = importRequest.Error });
        }

        var mapping = LeadFileService.ResolveMapping(importRequest.Sheet!.Headers, importRequest.Mapping);
        var analysis = await LeadFileService.AnalyzeAsync(db, tenant.TenantId, importRequest.Sheet, mapping, importRequest.DuplicateMode, cancellationToken);
        return Results.Ok(analysis.ToResponse());
    }
    catch (LeadImportException exception)
    {
        return Results.BadRequest(new { message = exception.Message });
    }
    catch (InvalidDataException)
    {
        return Results.BadRequest(new { message = "The uploaded file exceeds the allowed import size." });
    }
});

api.MapPost("/leads/import/commit", async (
    HttpRequest request,
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
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can import leads." }, statusCode: StatusCodes.Status403Forbidden);
    }

    try
    {
        var importRequest = await ReadLeadImportFormAsync(request, requireFingerprint: true, cancellationToken);
        if (importRequest.Error is not null)
        {
            return Results.BadRequest(new { message = importRequest.Error });
        }

        if (!string.Equals(importRequest.Sheet!.Fingerprint, importRequest.Fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "The selected file changed after preview. Preview the file again before importing." });
        }

        var mapping = LeadFileService.ResolveMapping(importRequest.Sheet.Headers, importRequest.Mapping);
        var analysis = await LeadFileService.AnalyzeAsync(db, tenant.TenantId, importRequest.Sheet, mapping, importRequest.DuplicateMode, cancellationToken);
        if (analysis.HasErrors)
        {
            return Results.Json(
                new { message = "Resolve import errors before committing leads.", preview = analysis.ToResponse() },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var response = await CommitLeadImportAsync(db, tenant.TenantId, currentUser, analysis, cancellationToken);
        return Results.Ok(response);
    }
    catch (LeadImportException exception)
    {
        return Results.BadRequest(new { message = exception.Message });
    }
    catch (InvalidDataException)
    {
        return Results.BadRequest(new { message = "The uploaded file exceeds the allowed import size." });
    }
    catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    {
        return Results.Conflict(new { message = "One or more leads were changed by another import. Preview the file again and retry." });
    }
    catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
    {
        return Results.Conflict(new { message = "Another lead import is running. Retry after it finishes." });
    }
});

api.MapGet("/leads/export", async (
    string? search,
    Guid? branchId,
    Guid? courseId,
    Guid? sourceId,
    Guid? stageId,
    Guid? assignedUserId,
    string? priority,
    string? temperature,
    int? minScore,
    int? maxScore,
    string? archive,
    string? sort,
    string? format,
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
    if (!CanManageUsers(currentUser))
    {
        return Results.Json(new { message = "Only owners and admins can export leads." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var query = ApplyLeadAccessScope(db.Leads.AsNoTracking().Where(lead => lead.TenantId == tenant.TenantId), currentUser, accessScope);
    query = ApplyLeadFilters(query, search, branchId, courseId, sourceId, stageId, assignedUserId, priority, temperature, minScore, maxScore, archive);
    query = ApplyLeadSort(query, sort);

    var total = await query.CountAsync(cancellationToken);
    if (total > LeadFileService.MaximumExportRows)
    {
        return Results.Json(
            new { message = $"The export contains {total} rows. Narrow the filters to {LeadFileService.MaximumExportRows} rows or fewer." },
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    var rows = await query
        .Select(lead => new LeadExportRow(
            lead.LeadNumber,
            lead.StudentName,
            lead.GuardianName,
            lead.Email,
            lead.Phone,
            lead.City,
            lead.Course.Name,
            lead.LeadSource.Name,
            lead.LeadStage.Name,
            lead.Status,
            lead.Priority,
            lead.Branch == null ? null : lead.Branch.Name,
            lead.AssignedUser == null ? null : lead.AssignedUser.FullName,
            lead.NextFollowUpAt,
            lead.CreatedAt,
            lead.UpdatedAt,
            lead.ArchivedAt))
        .ToListAsync(cancellationToken);

    var normalizedFormat = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? "csv" : "xlsx";
    var timestamp = IndianClock.Now().ToString("yyyyMMdd-HHmm");
    if (normalizedFormat == "csv")
    {
        return Results.File(
            LeadFileService.CreateCsv(rows),
            "text/csv; charset=utf-8",
            $"leads-{timestamp}.csv");
    }

    return Results.File(
        LeadFileService.CreateWorkbook(rows),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"leads-{timestamp}.xlsx");
});

api.MapGet("/applications", async (
    string? status,
    string? search,
    Guid? courseId,
    Guid? branchId,
    int? page,
    int? pageSize,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var pageNumber = Math.Clamp(page ?? 1, 1, 100000);
    var take = Math.Clamp(pageSize ?? 25, 1, 100);
    var query = db.AdmissionApplications.AsNoTracking().Where(item => item.TenantId == tenant.TenantId);
    if (currentUser is null)
    {
        query = query.Where(_ => false);
    }
    else if (!accessScope.CanViewAll)
    {
        var currentUserId = currentUser.UserId;
        if (currentUser.Role == nameof(UserRole.BranchManager) && accessScope.BranchId is not null)
        {
            var branchScope = accessScope.BranchId.Value;
            query = query.Where(item => item.Lead.BranchId == branchScope || item.Lead.AssignedUserId == currentUserId);
        }
        else
        {
            query = query.Where(item => item.Lead.AssignedUserId == currentUserId);
        }
    }
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
    if (courseId is not null) query = query.Where(item => item.CourseId == courseId);
    if (branchId is not null) query = query.Where(item => item.BranchId == branchId);
    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim();
        query = query.Where(item =>
            EF.Functions.ILike(item.ApplicationNumber, $"%{term}%") ||
            EF.Functions.ILike(item.Lead.StudentName, $"%{term}%") ||
            EF.Functions.ILike(item.Lead.LeadNumber, $"%{term}%"));
    }

    var total = await query.CountAsync(cancellationToken);
    var items = await query
        .OrderByDescending(item => item.UpdatedAt)
        .Skip((pageNumber - 1) * take)
        .Take(take)
        .Select(item => new ApplicationListItemResponse(
            item.ApplicationNumber,
            item.Lead.LeadNumber,
            item.Lead.StudentName,
            item.Course.Name,
            item.Branch == null ? null : item.Branch.Name,
            item.Intake,
            item.Status,
            item.ChecklistItems.Count,
            item.ChecklistItems.Count(check => check.IsCompleted || check.IsWaived),
            item.Version,
            item.UpdatedAt))
        .ToListAsync(cancellationToken);
    return Results.Ok(new ApplicationListResponse(items, pageNumber, take, total));
});

api.MapPost("/leads/{leadNumber}/applications", async (
    string leadNumber,
    CreateAdmissionApplicationRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageLeads(currentUser)) return Results.Json(new { message = "You do not have permission to create applications." }, statusCode: StatusCodes.Status403Forbidden);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var lead = await db.Leads.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == leadNumber, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });
    if (lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before creating an application." });

    var courseId = request.CourseId ?? lead.CourseId;
    var branchId = request.BranchId ?? lead.BranchId;
    if (!await db.Courses.AnyAsync(item => item.TenantId == tenant.TenantId && item.Id == courseId && item.IsActive, cancellationToken))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["courseId"] = ["Select a valid active course."] });
    if (branchId is not null && !await db.Branches.AnyAsync(item => item.TenantId == tenant.TenantId && item.Id == branchId && item.IsActive, cancellationToken))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["branchId"] = ["Select a valid active branch."] });

    var intake = NormalizeOptionalText(request.Intake);
    if (await db.AdmissionApplications.AnyAsync(item => item.TenantId == tenant.TenantId && item.LeadId == lead.Id && item.CourseId == courseId && item.Intake == intake, cancellationToken))
        return Results.Conflict(new { message = "An application already exists for this lead, course, and intake." });

    var now = IndianClock.Now();
    var application = new AdmissionApplication
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CourseId = courseId,
        BranchId = branchId,
        AssignedReviewerUserId = request.AssignedReviewerUserId,
        ApplicationNumber = await GenerateApplicationNumberAsync(db, tenant.TenantId, cancellationToken),
        Intake = intake,
        InternalNotes = NormalizeOptionalText(request.InternalNotes),
        CreatedAt = now,
        UpdatedAt = now,
        CreatedByUserId = currentUser?.UserId,
        UpdatedByUserId = currentUser?.UserId
    };
    db.AdmissionApplications.Add(application);
    AddDefaultAdmissionChecklist(db, tenant.TenantId, application.Id, now);
    db.AdmissionStatusHistories.Add(new AdmissionStatusHistory { Id = Guid.NewGuid(), TenantId = tenant.TenantId, ApplicationId = application.Id, NewStatus = "Draft", Note = "Application created.", ChangedAt = now, ChangedByUserId = currentUser?.UserId });
    db.Activities.Add(new EducationCrm.Api.Models.Activity { Id = Guid.NewGuid(), TenantId = tenant.TenantId, LeadId = lead.Id, CreatedByUserId = currentUser?.UserId, Type = "ApplicationCreated", Description = $"Application {application.ApplicationNumber} created.", CreatedAt = now });
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/applications/{application.ApplicationNumber}", await GetApplicationResponseAsync(db, tenant.TenantId, application.ApplicationNumber, cancellationToken));
});

api.MapGet("/applications/{applicationNumber}", async (
    string applicationNumber,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var application = await db.AdmissionApplications.AsNoTracking().Include(item => item.Lead).FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.ApplicationNumber == applicationNumber, cancellationToken);
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (application is null || !CanAccessLead(currentUser, accessScope, application.Lead)) return Results.NotFound(new { message = "Application not found." });
    return Results.Ok(await GetApplicationResponseAsync(db, tenant.TenantId, applicationNumber, cancellationToken));
});

api.MapPost("/applications/{applicationNumber}/transitions", async (
    string applicationNumber,
    TransitionApplicationRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var application = await db.AdmissionApplications.Include(item => item.Lead).Include(item => item.ChecklistItems).FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.ApplicationNumber == applicationNumber, cancellationToken);
    if (application is null || !CanAccessLead(currentUser, accessScope, application.Lead)) return Results.NotFound(new { message = "Application not found." });
    if (request.Version != application.Version) return Results.Conflict(new { message = "This application changed. Refresh and try again." });
    var targetStatus = NormalizeApplicationStatus(request.Status);
    var policyError = await ValidateApplicationTransitionAsync(db, application, currentUser, targetStatus, cancellationToken);
    if (policyError is not null) return Results.Conflict(new { message = policyError });

    var now = IndianClock.Now();
    var previous = application.Status;
    application.Status = targetStatus;
    application.DecisionReason = NormalizeOptionalText(request.Note) ?? application.DecisionReason;
    application.UpdatedAt = now;
    application.UpdatedByUserId = currentUser?.UserId;
    application.Version += 1;
    if (targetStatus == "Submitted") application.SubmittedAt ??= now;
    if (targetStatus == "UnderReview") application.ReviewedAt ??= now;
    if (targetStatus == "Approved") application.ApprovedAt ??= now;
    if (targetStatus == "Rejected") application.RejectedAt ??= now;
    db.AdmissionStatusHistories.Add(new AdmissionStatusHistory { Id = Guid.NewGuid(), TenantId = tenant.TenantId, ApplicationId = application.Id, PreviousStatus = previous, NewStatus = targetStatus, Note = NormalizeOptionalText(request.Note), ChangedAt = now, ChangedByUserId = currentUser?.UserId });
    db.Activities.Add(new EducationCrm.Api.Models.Activity { Id = Guid.NewGuid(), TenantId = tenant.TenantId, LeadId = application.LeadId, CreatedByUserId = currentUser?.UserId, Type = "ApplicationStatusChanged", Description = $"Application {application.ApplicationNumber} moved from {previous} to {targetStatus}.", CreatedAt = now });
    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This application changed. Refresh and try again." }); }
    return Results.Ok(await GetApplicationResponseAsync(db, tenant.TenantId, application.ApplicationNumber, cancellationToken));
});

api.MapPatch("/applications/{applicationNumber}/checklist/{itemId:guid}", async (
    string applicationNumber,
    Guid itemId,
    UpdateChecklistItemRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanReviewApplications(currentUser)) return Results.Json(new { message = "Only owner, admin, and branch manager roles can update admission checklist decisions." }, statusCode: StatusCodes.Status403Forbidden);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var item = await db.AdmissionChecklistItems.Include(check => check.Application).ThenInclude(app => app.Lead).FirstOrDefaultAsync(check => check.TenantId == tenant.TenantId && check.Id == itemId && check.Application.ApplicationNumber == applicationNumber, cancellationToken);
    if (item is null || !CanAccessLead(currentUser, accessScope, item.Application.Lead)) return Results.NotFound(new { message = "Checklist item not found." });
    if (request.Version != item.Version) return Results.Conflict(new { message = "This checklist item changed. Refresh and try again." });
    if (request.IsCompleted && request.IsWaived) return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["A requirement cannot be both completed and waived."] });
    var now = IndianClock.Now();
    item.IsCompleted = request.IsCompleted;
    item.IsWaived = request.IsWaived;
    item.Notes = NormalizeOptionalText(request.Notes);
    item.UpdatedAt = now;
    item.Version += 1;
    item.CompletedAt = request.IsCompleted || request.IsWaived ? now : null;
    item.CompletedByUserId = request.IsCompleted || request.IsWaived ? currentUser?.UserId : null;
    item.Application.UpdatedAt = now;
    item.Application.UpdatedByUserId = currentUser?.UserId;
    item.Application.Version += 1;
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(await GetApplicationResponseAsync(db, tenant.TenantId, applicationNumber, cancellationToken));
});

api.MapPost("/applications/{applicationNumber}/enroll", async (
    string applicationNumber,
    EnrollApplicationRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanReviewApplications(currentUser)) return Results.Json(new { message = "Only owner, admin, and branch manager roles can enroll approved applications." }, statusCode: StatusCodes.Status403Forbidden);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var application = await db.AdmissionApplications.Include(item => item.Lead).Include(item => item.ChecklistItems).FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.ApplicationNumber == applicationNumber, cancellationToken);
    if (application is null || !CanAccessLead(currentUser, accessScope, application.Lead)) return Results.NotFound(new { message = "Application not found." });
    if (request.Version != application.Version) return Results.Conflict(new { message = "This application changed. Refresh and try again." });
    var readinessError = await ValidateApplicationReadyForApprovalAsync(db, application, cancellationToken);
    if (application.Status != "Approved" || readinessError is not null) return Results.Conflict(new { message = readinessError ?? "Approve this application before enrollment." });
    if (await db.Enrollments.AnyAsync(item => item.ApplicationId == application.Id, cancellationToken)) return Results.Conflict(new { message = "This application is already enrolled." });
    var wonStage = await db.LeadStages.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.IsActive && item.IsWonStage, cancellationToken);
    if (wonStage is null) return Results.Conflict(new { message = "Configure an active won stage before enrolling students." });

    var now = IndianClock.Now();
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    var enrollment = new Enrollment
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        ApplicationId = application.Id,
        LeadId = application.LeadId,
        CourseId = application.CourseId,
        BranchId = application.BranchId,
        EnrollmentNumber = await GenerateEnrollmentNumberAsync(db, tenant.TenantId, cancellationToken),
        StudentName = application.Lead.StudentName,
        Intake = NormalizeOptionalText(request.Intake) ?? application.Intake,
        EnrolledAt = now,
        CreatedAt = now,
        UpdatedAt = now,
        CreatedByUserId = currentUser?.UserId,
        UpdatedByUserId = currentUser?.UserId
    };
    db.Enrollments.Add(enrollment);
    var previousStatus = application.Status;
    application.Status = "Enrolled";
    application.UpdatedAt = now;
    application.UpdatedByUserId = currentUser?.UserId;
    application.Version += 1;
    application.Lead.LeadStageId = wonStage.Id;
    application.Lead.Status = wonStage.Name;
    application.Lead.UpdatedAt = now;
    application.Lead.UpdatedByUserId = currentUser?.UserId;
    application.Lead.Version += 1;
    db.AdmissionStatusHistories.Add(new AdmissionStatusHistory { Id = Guid.NewGuid(), TenantId = tenant.TenantId, ApplicationId = application.Id, PreviousStatus = previousStatus, NewStatus = "Enrolled", Note = NormalizeOptionalText(request.Note), ChangedAt = now, ChangedByUserId = currentUser?.UserId });
    db.Activities.Add(new EducationCrm.Api.Models.Activity { Id = Guid.NewGuid(), TenantId = tenant.TenantId, LeadId = application.LeadId, CreatedByUserId = currentUser?.UserId, Type = "EnrollmentCreated", Description = $"Enrollment {enrollment.EnrollmentNumber} created from application {application.ApplicationNumber}.", CreatedAt = now });
    await db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return Results.Ok(await GetApplicationResponseAsync(db, tenant.TenantId, application.ApplicationNumber, cancellationToken));
});

api.MapGet("/enrollments", async (
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var queryValues = httpContext.Request.Query;
    var pageNumber = int.TryParse(queryValues["page"].FirstOrDefault(), out var parsedPage) ? Math.Max(1, parsedPage) : 1;
    var take = int.TryParse(queryValues["pageSize"].FirstOrDefault(), out var parsedPageSize) ? Math.Clamp(parsedPageSize, 1, 100) : 25;
    var search = NormalizeOptionalText(queryValues["search"].FirstOrDefault());
    var status = NormalizeOptionalText(queryValues["status"].FirstOrDefault());
    var intake = NormalizeOptionalText(queryValues["intake"].FirstOrDefault());
    Guid.TryParse(queryValues["courseId"].FirstOrDefault(), out var courseId);
    Guid.TryParse(queryValues["branchId"].FirstOrDefault(), out var branchId);

    if (search?.Length > 160) return Results.ValidationProblem(new Dictionary<string, string[]> { ["search"] = ["Search must be 160 characters or fewer."] });
    if (intake?.Length > 120) return Results.ValidationProblem(new Dictionary<string, string[]> { ["intake"] = ["Intake must be 120 characters or fewer."] });

    var query = db.Enrollments
        .AsNoTracking()
        .Include(item => item.Lead)
        .Include(item => item.Application)
        .Include(item => item.Course)
        .Include(item => item.Branch)
        .Where(item => item.TenantId == tenant.TenantId);

    if (!accessScope.CanViewAll)
    {
        query = query.Where(item =>
            currentUser != null &&
            (item.Lead.AssignedUserId == currentUser.UserId ||
             (currentUser.Role == nameof(UserRole.BranchManager) && accessScope.BranchId != null && item.Lead.BranchId == accessScope.BranchId)));
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim();
        query = query.Where(item =>
            EF.Functions.ILike(item.StudentName, $"%{term}%") ||
            EF.Functions.ILike(item.EnrollmentNumber, $"%{term}%") ||
            EF.Functions.ILike(item.Lead.LeadNumber, $"%{term}%") ||
            EF.Functions.ILike(item.Application.ApplicationNumber, $"%{term}%"));
    }

    if (!string.IsNullOrWhiteSpace(status))
    {
        var normalizedStatus = NormalizeEnrollmentStatus(status);
        if (normalizedStatus == "Invalid")
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Select a valid enrollment status."] });
        }
        query = query.Where(item => item.Status == normalizedStatus);
    }

    if (!string.IsNullOrWhiteSpace(intake))
    {
        query = query.Where(item => item.Intake != null && EF.Functions.ILike(item.Intake, $"%{intake}%"));
    }

    if (courseId != Guid.Empty) query = query.Where(item => item.CourseId == courseId);
    if (branchId != Guid.Empty) query = query.Where(item => item.BranchId == branchId);

    var total = await query.CountAsync(cancellationToken);
    var rawItems = await query
        .OrderByDescending(item => item.UpdatedAt)
        .ThenByDescending(item => item.EnrolledAt)
        .Skip((pageNumber - 1) * take)
        .Take(take)
        .Select(item => new
        {
            item.Id,
            item.EnrollmentNumber,
            item.StudentName,
            LeadId = item.Lead.LeadNumber,
            ApplicationId = item.Application.ApplicationNumber,
            Course = item.Course.Name,
            Branch = item.Branch == null ? null : item.Branch.Name,
            item.Intake,
            item.Status,
            item.Version,
            item.EnrolledAt,
            item.UpdatedAt,
            LeadInternalId = item.LeadId
        })
        .ToListAsync(cancellationToken);

    var leadIds = rawItems.Select(item => item.LeadInternalId).Distinct().ToArray();
    var paymentRows = await db.LeadPayments
        .AsNoTracking()
        .Include(item => item.Transactions)
        .Where(item => item.TenantId == tenant.TenantId && leadIds.Contains(item.LeadId) && item.CancelledAt == null)
        .ToListAsync(cancellationToken);
    var paymentBalances = paymentRows
        .GroupBy(item => item.LeadId)
        .ToDictionary(
            group => group.Key,
            group => group.Sum(payment => Math.Max(0m, payment.AmountDue - payment.Transactions.Sum(transaction => transaction.Amount))));
    var documentRows = await db.LeadDocuments
        .AsNoTracking()
        .Include(item => item.DocumentType)
        .Where(item => item.TenantId == tenant.TenantId && leadIds.Contains(item.LeadId) && item.DocumentType.IsActive && item.DocumentType.IsRequired)
        .ToListAsync(cancellationToken);
    var requiredDocumentCount = await db.DocumentTypes.CountAsync(item => item.TenantId == tenant.TenantId && item.IsActive && item.IsRequired, cancellationToken);
    var verifiedDocuments = documentRows
        .Where(item => item.Status == "Verified")
        .GroupBy(item => item.LeadId)
        .ToDictionary(group => group.Key, group => group.Select(item => item.DocumentTypeId).Distinct().Count());

    var items = new List<EnrollmentListItemResponse>(rawItems.Count);
    foreach (var item in rawItems)
    {
        paymentBalances.TryGetValue(item.LeadInternalId, out var feeBalance);
        verifiedDocuments.TryGetValue(item.LeadInternalId, out var verifiedRequiredDocuments);
        items.Add(new EnrollmentListItemResponse(
            item.EnrollmentNumber,
            item.StudentName,
            item.LeadId,
            item.ApplicationId,
            item.Course,
            item.Branch,
            item.Intake,
            item.Status,
            feeBalance,
            requiredDocumentCount == 0 || verifiedRequiredDocuments >= requiredDocumentCount,
            item.Version,
            item.EnrolledAt,
            item.UpdatedAt));
    }

    return Results.Ok(new EnrollmentListResponse(items, pageNumber, take, total));
});

api.MapGet("/enrollments/{enrollmentNumber}", async (
    string enrollmentNumber,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var enrollment = await db.Enrollments
        .AsNoTracking()
        .Include(item => item.Lead)
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.EnrollmentNumber == enrollmentNumber, cancellationToken);
    if (enrollment is null || !CanAccessLead(currentUser, accessScope, enrollment.Lead)) return Results.NotFound(new { message = "Enrollment not found." });
    return Results.Ok(await GetEnrollmentDetailResponseAsync(db, tenant.TenantId, enrollment.EnrollmentNumber, cancellationToken));
});

api.MapPatch("/enrollments/{enrollmentNumber}/status", async (
    string enrollmentNumber,
    UpdateEnrollmentStatusRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageEnrollmentStatus(currentUser)) return Results.Json(new { message = "Only owner, admin, and branch manager roles can update enrollment status." }, statusCode: StatusCodes.Status403Forbidden);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var enrollment = await db.Enrollments
        .Include(item => item.Lead)
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.EnrollmentNumber == enrollmentNumber, cancellationToken);
    if (enrollment is null || !CanAccessLead(currentUser, accessScope, enrollment.Lead)) return Results.NotFound(new { message = "Enrollment not found." });
    if (request.Version != enrollment.Version) return Results.Conflict(new { message = "This enrollment changed. Refresh and try again." });
    if (NormalizeOptionalText(request.Note)?.Length > 500) return Results.ValidationProblem(new Dictionary<string, string[]> { ["note"] = ["Status note must be 500 characters or fewer."] });
    var nextStatus = NormalizeEnrollmentStatus(request.Status);
    var transitionError = ValidateEnrollmentStatusTransition(enrollment.Status, nextStatus, currentUser);
    if (transitionError is not null) return Results.Conflict(new { message = transitionError });

    var now = IndianClock.Now();
    var previous = enrollment.Status;
    enrollment.Status = nextStatus;
    enrollment.UpdatedAt = now;
    enrollment.UpdatedByUserId = currentUser?.UserId;
    enrollment.Version += 1;
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = enrollment.LeadId,
        CreatedByUserId = currentUser?.UserId,
        Type = "EnrollmentStatusChanged",
        Description = $"Enrollment {enrollment.EnrollmentNumber} moved from {previous} to {nextStatus}.{(string.IsNullOrWhiteSpace(request.Note) ? "" : $" Note: {NormalizeOptionalText(request.Note)}")}",
        CreatedAt = now
    });
    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This enrollment changed. Refresh and try again." }); }
    return Results.Ok(await GetEnrollmentDetailResponseAsync(db, tenant.TenantId, enrollment.EnrollmentNumber, cancellationToken));
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

    var canUseTenantDefaults = currentUser?.Role is nameof(UserRole.Owner) or nameof(UserRole.Admin);
    var tenantDefaults = canUseTenantDefaults
        ? await db.Tenants
            .AsNoTracking()
            .Where(item => item.Id == tenant.TenantId)
            .Select(item => new
            {
                DefaultBranchId = item.DefaultBranch != null && item.DefaultBranch.IsActive ? item.DefaultBranchId : null,
                DefaultAssigneeUserId = item.DefaultAssigneeUser != null &&
                    item.DefaultAssigneeUser.IsActive &&
                    item.DefaultAssigneeUser.Role != UserRole.Accountant &&
                    item.DefaultAssigneeUser.Role != UserRole.ReadOnly
                        ? item.DefaultAssigneeUserId
                        : null
            })
            .FirstAsync(cancellationToken)
        : null;
    var branchId = request.BranchId ?? tenantDefaults?.DefaultBranchId;
    var assignedUserId = request.AssignedUserId;
    var now = IndianClock.Now();
    var intelligenceSettings = await GetOrCreateLeadIntelligenceSettingsAsync(db, tenant.TenantId, now, cancellationToken);
    LeadDistributionDecision? distributionDecision = null;

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

    if (!CanCreateLeadInBranch(currentUser, accessScope, branchId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["branchId"] = ["You cannot create leads for this branch."]
        });
    }

    if (branchId is not null)
    {
        var branchExists = await db.Branches.AnyAsync(
            item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == branchId,
            cancellationToken);
        if (!branchExists)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["branchId"] = ["Select a valid active branch."]
            });
        }
    }

    if (assignedUserId is null)
    {
        distributionDecision = await ResolveLeadDistributionAsync(db, intelligenceSettings, tenant.TenantId, branchId, request.CourseId, request.LeadSourceId, now, cancellationToken);
        assignedUserId = distributionDecision.AssignedUserId ?? tenantDefaults?.DefaultAssigneeUserId;
    }

    if (assignedUserId is not null)
    {
        var assignmentError = await ValidateLeadAssignmentAsync(db, tenant.TenantId, currentUser, accessScope, branchId, assignedUserId.Value, cancellationToken);
        if (assignmentError is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedUserId"] = [assignmentError]
            });
        }
    }

    var lead = new Lead
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        BranchId = branchId,
        CourseId = request.CourseId,
        LeadStageId = request.LeadStageId,
        LeadSourceId = request.LeadSourceId,
        AssignedUserId = assignedUserId,
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

    await ApplyLeadScoringAsync(db, lead, intelligenceSettings, "Lead created", now, cancellationToken);

    db.Leads.Add(lead);
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = currentUser?.UserId,
        Type = "LeadCreated",
        Description = $"Lead {lead.LeadNumber} created for {lead.StudentName}.",
        CreatedAt = now
    });
    if (distributionDecision?.AssignedUserId is not null)
    {
        db.LeadIntelligenceEvents.Add(new LeadIntelligenceEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            EventType = "Distributed",
            PreviousAssignedUserId = null,
            NewAssignedUserId = distributionDecision.AssignedUserId,
            DistributionRuleId = distributionDecision.RuleId,
            Reason = distributionDecision.Reason,
            CreatedAt = now
        });
    }

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
            item.NextFollowUpAt,
            item.IntelligenceScore,
            item.IntelligenceTemperature,
            item.IntelligenceReason,
            item.IntelligenceUpdatedAt))
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
                    followUp.CompletionOutcome,
                    followUp.CompletionNotes,
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
                .ToArray(),
            db.LeadIntelligenceEvents
                .Where(@event => @event.TenantId == item.TenantId && @event.LeadId == item.Id)
                .OrderByDescending(@event => @event.CreatedAt)
                .Take(20)
                .Select(@event => new LeadIntelligenceEventResponse(
                    @event.Id.ToString(),
                    @event.EventType,
                    @event.PreviousScore,
                    @event.NewScore,
                    @event.PreviousTemperature,
                    @event.NewTemperature,
                    @event.PreviousAssignedUser == null ? null : @event.PreviousAssignedUser.FullName,
                    @event.NewAssignedUser == null ? null : @event.NewAssignedUser.FullName,
                    @event.DistributionRule == null ? null : @event.DistributionRule.Name,
                    @event.Reason,
                    @event.CreatedAt
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

    var intelligenceSettings = await GetOrCreateLeadIntelligenceSettingsAsync(db, tenant.TenantId, lead.UpdatedAt, cancellationToken);
    await ApplyLeadScoringAsync(db, lead, intelligenceSettings, "Lead updated", lead.UpdatedAt, cancellationToken);

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
        var previousAssignedUserId = lead.AssignedUserId;
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
        db.LeadIntelligenceEvents.Add(new LeadIntelligenceEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            EventType = "AssignmentChanged",
            PreviousAssignedUserId = previousAssignedUserId,
            NewAssignedUserId = request.AssignedUserId,
            Reason = currentUser is null ? "Assignment changed by system." : $"Assignment changed manually by {currentUser.FullName}.",
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
        var stageIntelligenceSettings = await GetOrCreateLeadIntelligenceSettingsAsync(db, tenant.TenantId, lead.UpdatedAt, cancellationToken);
        await ApplyLeadScoringAsync(db, lead, stageIntelligenceSettings, "Stage changed", lead.UpdatedAt, cancellationToken);
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

api.MapPost("/leads/bulk-actions", async (
    BulkLeadActionRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (!CanManageLeadArchive(currentUser))
    {
        return Results.Json(new { message = "Only owners, admins, and branch managers can run bulk lead actions." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var action = request.Action?.Trim().ToLowerInvariant();
    var supportedActions = new HashSet<string>(StringComparer.Ordinal) { "assign", "changestage", "archive", "restore" };
    if (string.IsNullOrWhiteSpace(action) || !supportedActions.Contains(action))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["Select a valid bulk action."] });
    }

    if (request.Items is null || request.Items.Count is < 1 or > 100)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["items"] = ["Select between 1 and 100 leads."] });
    }

    var invalidItem = request.Items.Any(item => string.IsNullOrWhiteSpace(item.LeadId) || item.Version < 1);
    var normalizedItems = request.Items
        .Select(item => new BulkLeadActionItem(item.LeadId?.Trim() ?? string.Empty, item.Version))
        .ToList();
    if (invalidItem || normalizedItems.Select(item => item.LeadId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedItems.Count)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["items"] = ["Every selected lead must be unique and include a valid version."] });
    }

    if (action == "changestage" && request.LeadStageId is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["leadStageId"] = ["Select a stage."] });
    }

    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var leadIds = normalizedItems.Select(item => item.LeadId).ToList();
    var leads = await ApplyLeadAccessScope(
            db.Leads.Where(item => item.TenantId == tenant.TenantId && leadIds.Contains(item.LeadNumber)),
            currentUser,
            accessScope)
        .ToListAsync(cancellationToken);

    if (leads.Count != normalizedItems.Count)
    {
        return Results.NotFound(new { message = "One or more selected leads no longer exist or are not accessible. Refresh the list and try again." });
    }

    var requestedVersions = normalizedItems.ToDictionary(item => item.LeadId, item => item.Version, StringComparer.OrdinalIgnoreCase);
    var staleLeadIds = leads
        .Where(lead => requestedVersions[lead.LeadNumber] != lead.Version)
        .Select(lead => lead.LeadNumber)
        .OrderBy(id => id)
        .ToList();
    if (staleLeadIds.Count > 0)
    {
        return Results.Conflict(new
        {
            message = $"{staleLeadIds.Count} selected lead(s) changed after selection. Refresh and try again.",
            leadIds = staleLeadIds
        });
    }

    LeadStage? targetStage = null;
    if (action == "changestage")
    {
        targetStage = await db.LeadStages.AsNoTracking().FirstOrDefaultAsync(
            item => item.TenantId == tenant.TenantId && item.Id == request.LeadStageId && item.IsActive,
            cancellationToken);
        if (targetStage is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["leadStageId"] = ["Select a valid active stage."] });
        }
    }

    AppUser? targetAssignee = null;
    if (action == "assign" && request.AssignedUserId is not null)
    {
        targetAssignee = await db.Users.AsNoTracking().FirstOrDefaultAsync(
            item => item.TenantId == tenant.TenantId && item.Id == request.AssignedUserId && item.IsActive,
            cancellationToken);
        if (targetAssignee is null || targetAssignee.Role is UserRole.Accountant or UserRole.ReadOnly)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["assignedUserId"] = ["Select a valid active CRM user."] });
        }

        if (currentUser!.Role is nameof(UserRole.Counselor) or nameof(UserRole.Telecaller) && targetAssignee.Id != currentUser.UserId)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["assignedUserId"] = ["You can only assign leads to yourself."] });
        }
    }

    if (action is "assign" or "changestage" && leads.Any(lead => lead.ArchivedAt is not null))
    {
        return Results.Conflict(new { message = "Archived leads cannot be assigned or moved. Restore them first." });
    }

    if (action == "assign" && targetAssignee is not null)
    {
        if (currentUser!.Role == nameof(UserRole.BranchManager) && accessScope.BranchId is not null &&
            leads.Any(lead => lead.BranchId != accessScope.BranchId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["assignedUserId"] = ["You can only assign leads from your branch."] });
        }

        if (currentUser!.Role == nameof(UserRole.BranchManager) && accessScope.BranchId is not null &&
            targetAssignee.BranchId is not null && targetAssignee.BranchId != accessScope.BranchId)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["assignedUserId"] = ["Select a user from your branch."] });
        }

        if (leads.Any(lead => lead.BranchId is not null && targetAssignee.BranchId is not null && lead.BranchId != targetAssignee.BranchId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["assignedUserId"] = ["The selected user must belong to each lead's branch."] });
        }
    }

    var now = IndianClock.Now();
    var changed = 0;
    foreach (var lead in leads)
    {
        string? activityType = null;
        string? description = null;

        switch (action)
        {
            case "assign" when lead.AssignedUserId != request.AssignedUserId:
                lead.AssignedUserId = request.AssignedUserId;
                activityType = "LeadAssigned";
                description = request.AssignedUserId is null
                    ? $"Lead {lead.LeadNumber} unassigned through a bulk action."
                    : $"Lead {lead.LeadNumber} assigned to {targetAssignee!.FullName} through a bulk action.";
                break;
            case "changestage" when lead.LeadStageId != request.LeadStageId:
                lead.LeadStageId = targetStage!.Id;
                lead.Status = targetStage.Name;
                activityType = "StageChanged";
                description = $"Lead {lead.LeadNumber} moved to {targetStage.Name} through a bulk action.";
                break;
            case "archive" when lead.ArchivedAt is null:
                lead.ArchivedAt = now;
                lead.ArchivedByUserId = currentUser!.UserId;
                activityType = "LeadArchived";
                description = $"Lead {lead.LeadNumber} archived through a bulk action.";
                break;
            case "restore" when lead.ArchivedAt is not null:
                lead.ArchivedAt = null;
                lead.ArchivedByUserId = null;
                activityType = "LeadRestored";
                description = $"Lead {lead.LeadNumber} restored through a bulk action.";
                break;
        }

        if (activityType is null) continue;
        lead.UpdatedAt = now;
        lead.UpdatedByUserId = currentUser!.UserId;
        lead.Version += 1;
        changed += 1;
        db.Activities.Add(new EducationCrm.Api.Models.Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            CreatedByUserId = currentUser.UserId,
            Type = activityType,
            Description = description!,
            CreatedAt = now
        });
    }

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(new { message = "One or more selected leads changed during the update. Refresh and try again." });
    }

    return Results.Ok(new BulkLeadActionResponse(
        normalizedItems.Count,
        changed,
        normalizedItems.Count - changed,
        $"{changed} lead(s) updated successfully."));
});

api.MapGet("/leads/{id}/documents", async (
    string id,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var lead = await db.Leads
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });

    return Results.Ok(await GetLeadDocumentsResponseAsync(db, tenant.TenantId, lead.Id, cancellationToken));
});

api.MapPost("/leads/{id}/documents", async (
    string id,
    HttpRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    ILeadDocumentStorage storage,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanUploadLeadDocuments(currentUser)) return Results.Json(new { message = "You do not have permission to upload documents." }, statusCode: StatusCodes.Status403Forbidden);
    if (!storage.IsConfigured) return Results.Json(new { message = "Cloudinary document storage is not configured." }, statusCode: StatusCodes.Status503ServiceUnavailable);

    var lead = await db.Leads.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });
    if (lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before uploading documents." });

    var form = await ReadLeadDocumentUploadFormAsync(request, cancellationToken);
    if (form.Errors.Count > 0) return Results.ValidationProblem(form.Errors);

    var documentType = await db.DocumentTypes
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.Id == form.DocumentTypeId, cancellationToken);
    if (documentType is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["documentTypeId"] = ["Select a valid document type."] });
    }

    if (!documentType.IsActive)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["documentTypeId"] = ["This document type is inactive."] });
    }

    var existingDocument = await db.LeadDocuments
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadId == lead.Id && item.DocumentTypeId == form.DocumentTypeId, cancellationToken);

    if (existingDocument is not null && form.Version is null)
    {
        return Results.Conflict(new { message = "This document already exists. Refresh and replace it with the latest version." });
    }

    if (existingDocument is not null && existingDocument.Version != form.Version)
    {
        return Results.Conflict(new { message = "This document was changed by another user. Refresh and try again." });
    }

    if (existingDocument is not null &&
        existingDocument.Status == "Verified" &&
        !CanReviewLeadDocuments(currentUser))
    {
        return Results.Conflict(new { message = "Only owners, admins, and branch managers can replace a verified document." });
    }

    LeadDocumentUploadResult upload;
    try
    {
        upload = await storage.UploadAsync(
            form.File!,
            new LeadDocumentUploadContext(tenant.TenantId, lead.LeadNumber, documentType.Id),
            cancellationToken);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Json(new { message = exception.Message }, statusCode: StatusCodes.Status502BadGateway);
    }

    string? oldPublicId = null;
    string? oldResourceType = null;
    string? oldDeliveryType = null;
    var now = IndianClock.Now();
    if (existingDocument is null)
    {
        existingDocument = new LeadDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            DocumentTypeId = documentType.Id,
            UploadedAt = now,
            UploadedByUserId = currentUser?.UserId
        };
        db.LeadDocuments.Add(existingDocument);
    }
    else
    {
        oldPublicId = existingDocument.CloudinaryPublicId;
        oldResourceType = existingDocument.CloudinaryResourceType;
        oldDeliveryType = existingDocument.CloudinaryDeliveryType;
        existingDocument.Version += 1;
    }

    existingDocument.OriginalFileName = SanitizeFileName(form.File!.FileName);
    existingDocument.ContentType = upload.ContentType;
    existingDocument.FileSizeBytes = upload.Bytes;
    existingDocument.CloudinaryAssetId = upload.AssetId;
    existingDocument.CloudinaryPublicId = upload.PublicId;
    existingDocument.CloudinaryResourceType = upload.ResourceType;
    existingDocument.CloudinaryDeliveryType = upload.DeliveryType;
    existingDocument.CloudinarySecureUrl = upload.SecureUrl;
    existingDocument.Status = "Uploaded";
    existingDocument.Notes = NormalizeOptionalText(form.Notes);
    existingDocument.UpdatedAt = now;
    existingDocument.UpdatedByUserId = currentUser?.UserId;
    existingDocument.ReviewedAt = null;
    existingDocument.ReviewedByUserId = null;

    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = currentUser?.UserId,
        Type = "DocumentUploaded",
        Description = $"{documentType.Name} uploaded for lead {lead.LeadNumber}.",
        CreatedAt = now
    });

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
        await TryDeleteCloudinaryAssetAsync(storage, upload.PublicId, upload.ResourceType, upload.DeliveryType, cancellationToken);
        return Results.Conflict(new { message = "This document was changed by another user. Refresh and try again." });
    }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception))
    {
        await TryDeleteCloudinaryAssetAsync(storage, upload.PublicId, upload.ResourceType, upload.DeliveryType, cancellationToken);
        return Results.Conflict(new { message = "This document already exists. Refresh and try again." });
    }
    catch
    {
        await TryDeleteCloudinaryAssetAsync(storage, upload.PublicId, upload.ResourceType, upload.DeliveryType, cancellationToken);
        throw;
    }

    if (!string.IsNullOrWhiteSpace(oldPublicId) && oldResourceType is not null && oldDeliveryType is not null)
    {
        await TryDeleteCloudinaryAssetAsync(storage, oldPublicId, oldResourceType, oldDeliveryType, cancellationToken);
    }

    return Results.Ok(await GetLeadDocumentsResponseAsync(db, tenant.TenantId, lead.Id, cancellationToken));
});

api.MapPatch("/leads/{id}/documents/{documentId:guid}/verify", async (
    string id,
    Guid documentId,
    ReviewLeadDocumentRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    return await ReviewLeadDocumentAsync(id, documentId, request, "Verified", httpContext, db, configuration, cancellationToken);
});

api.MapPatch("/leads/{id}/documents/{documentId:guid}/reject", async (
    string id,
    Guid documentId,
    ReviewLeadDocumentRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Notes))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["notes"] = ["Add a rejection reason."] });
    }

    return await ReviewLeadDocumentAsync(id, documentId, request, "Rejected", httpContext, db, configuration, cancellationToken);
});

api.MapDelete("/leads/{id}/documents/{documentId:guid}", async (
    string id,
    Guid documentId,
    [FromBody] LeadDocumentVersionRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    ILeadDocumentStorage storage,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanReviewLeadDocuments(currentUser)) return Results.Json(new { message = "Only owners, admins, and branch managers can delete documents." }, statusCode: StatusCodes.Status403Forbidden);

    var document = await db.LeadDocuments
        .Include(item => item.Lead)
        .Include(item => item.DocumentType)
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.Id == documentId && item.Lead.LeadNumber == id, cancellationToken);
    if (document is null || !CanAccessLead(currentUser, accessScope, document.Lead)) return Results.NotFound(new { message = "Document not found." });
    if (document.Lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before deleting documents." });
    if (request.Version != document.Version) return Results.Conflict(new { message = "This document was changed by another user. Refresh and try again." });
    if (document.Status == "Verified") return Results.Conflict(new { message = "Reject or replace the verified document before deleting it." });

    var publicId = document.CloudinaryPublicId;
    var resourceType = document.CloudinaryResourceType;
    var deliveryType = document.CloudinaryDeliveryType;
    var documentTypeName = document.DocumentType.Name;
    var leadId = document.LeadId;
    db.LeadDocuments.Remove(document);
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = leadId,
        CreatedByUserId = currentUser?.UserId,
        Type = "DocumentDeleted",
        Description = $"{documentTypeName} deleted for lead {id}.",
        CreatedAt = IndianClock.Now()
    });

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(new { message = "This document was changed by another user. Refresh and try again." });
    }

    await TryDeleteCloudinaryAssetAsync(storage, publicId, resourceType, deliveryType, cancellationToken);
    return Results.Ok(await GetLeadDocumentsResponseAsync(db, tenant.TenantId, leadId, cancellationToken));
});

api.MapGet("/leads/{id}/documents/{documentId:guid}/download", async (
    string id,
    Guid documentId,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var document = await db.LeadDocuments
        .AsNoTracking()
        .Include(item => item.Lead)
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.Id == documentId && item.Lead.LeadNumber == id, cancellationToken);
    if (document is null || !CanAccessLead(currentUser, accessScope, document.Lead)) return Results.NotFound(new { message = "Document not found." });
    if (string.IsNullOrWhiteSpace(document.CloudinarySecureUrl)) return Results.Conflict(new { message = "This document cannot be downloaded because the storage URL is missing." });

    try
    {
        var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(document.CloudinarySecureUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new { message = "The document file is unavailable in Cloudinary." }, statusCode: StatusCodes.Status502BadGateway);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return Results.File(bytes, document.ContentType, document.OriginalFileName);
    }
    catch (HttpRequestException)
    {
        return Results.Json(new { message = "The document file is unavailable in Cloudinary." }, statusCode: StatusCodes.Status502BadGateway);
    }
});

api.MapGet("/leads/{id}/payments", async (
    string id,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var lead = await db.Leads
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });

    return Results.Ok(await GetLeadPaymentsResponseAsync(db, tenant.TenantId, lead.Id, cancellationToken));
});

api.MapPost("/leads/{id}/payments", async (
    string id,
    SaveLeadPaymentRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManagePayments(currentUser)) return Results.Json(new { message = "Only owners, admins, and accountants can manage payments." }, statusCode: StatusCodes.Status403Forbidden);

    var lead = await db.Leads.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });
    if (lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before adding payments." });

    var errors = ValidateSaveLeadPaymentRequest(request, requireVersion: false);
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    var now = IndianClock.Now();
    var payment = new LeadPayment
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        Title = NormalizeName(request.Title),
        AmountDue = NormalizeMoney(request.AmountDue),
        Currency = NormalizeCurrency(request.Currency),
        DueDate = IndianClock.ToIndianTime(request.DueDate),
        Notes = NormalizeOptionalText(request.Notes),
        CreatedAt = now,
        UpdatedAt = now,
        CreatedByUserId = currentUser?.UserId,
        UpdatedByUserId = currentUser?.UserId
    };
    payment.Status = CalculateLeadPaymentStatus(payment, 0m);
    db.LeadPayments.Add(payment);
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = currentUser?.UserId,
        Type = "PaymentCreated",
        Description = $"Payment item {payment.Title} created for {FormatMoney(payment.AmountDue, payment.Currency)}.",
        CreatedAt = now
    });
    var intelligenceSettings = await GetOrCreateLeadIntelligenceSettingsAsync(db, tenant.TenantId, now, cancellationToken);
    await ApplyLeadScoringAsync(db, lead, intelligenceSettings, "Follow-up scheduled", now, cancellationToken);

    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/leads/{id}/payments/{payment.Id}", await GetLeadPaymentsResponseAsync(db, tenant.TenantId, lead.Id, cancellationToken));
});

api.MapPatch("/leads/{id}/payments/{paymentId:guid}", async (
    string id,
    Guid paymentId,
    SaveLeadPaymentRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManagePayments(currentUser)) return Results.Json(new { message = "Only owners, admins, and accountants can manage payments." }, statusCode: StatusCodes.Status403Forbidden);

    var payment = await db.LeadPayments
        .Include(item => item.Lead)
        .Include(item => item.Transactions)
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.Id == paymentId && item.Lead.LeadNumber == id, cancellationToken);
    if (payment is null || !CanAccessLead(currentUser, accessScope, payment.Lead)) return Results.NotFound(new { message = "Payment not found." });
    if (payment.Lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before editing payments." });
    if (payment.CancelledAt is not null) return Results.Conflict(new { message = "Cancelled payments cannot be edited." });
    if (request.Version != payment.Version) return Results.Conflict(new { message = "This payment was changed by another user. Refresh and try again." });

    var errors = ValidateSaveLeadPaymentRequest(request, requireVersion: true);
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    var nextAmountDue = NormalizeMoney(request.AmountDue);
    var paidTotal = payment.Transactions.Sum(item => item.Amount);
    if (nextAmountDue < paidTotal)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["amountDue"] = ["Amount due cannot be less than the amount already received."] });
    }

    var now = IndianClock.Now();
    payment.Title = NormalizeName(request.Title);
    payment.AmountDue = nextAmountDue;
    payment.Currency = NormalizeCurrency(request.Currency);
    payment.DueDate = IndianClock.ToIndianTime(request.DueDate);
    payment.Notes = NormalizeOptionalText(request.Notes);
    payment.Status = CalculateLeadPaymentStatus(payment, paidTotal);
    payment.UpdatedAt = now;
    payment.UpdatedByUserId = currentUser?.UserId;
    payment.Version += 1;
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = payment.LeadId,
        CreatedByUserId = currentUser?.UserId,
        Type = "PaymentUpdated",
        Description = $"Payment item {payment.Title} updated. Balance: {FormatMoney(payment.AmountDue - paidTotal, payment.Currency)}.",
        CreatedAt = now
    });
    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This payment was changed by another user. Refresh and try again." }); }
    return Results.Ok(await GetLeadPaymentsResponseAsync(db, tenant.TenantId, payment.LeadId, cancellationToken));
});

api.MapPost("/leads/{id}/payments/{paymentId:guid}/transactions", async (
    string id,
    Guid paymentId,
    CreateLeadPaymentTransactionRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManagePayments(currentUser)) return Results.Json(new { message = "Only owners, admins, and accountants can record payments." }, statusCode: StatusCodes.Status403Forbidden);

    var payment = await db.LeadPayments
        .Include(item => item.Lead)
        .Include(item => item.Transactions)
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.Id == paymentId && item.Lead.LeadNumber == id, cancellationToken);
    if (payment is null || !CanAccessLead(currentUser, accessScope, payment.Lead)) return Results.NotFound(new { message = "Payment not found." });
    if (payment.Lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before recording payments." });
    if (payment.CancelledAt is not null) return Results.Conflict(new { message = "Cancelled payments cannot receive transactions." });
    if (request.Version != payment.Version) return Results.Conflict(new { message = "This payment was changed by another user. Refresh and try again." });

    var errors = ValidateLeadPaymentTransactionRequest(request);
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    var amount = NormalizeMoney(request.Amount);
    var paidTotal = payment.Transactions.Sum(item => item.Amount);
    if (paidTotal + amount > payment.AmountDue)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["amount"] = ["Payment amount cannot exceed the remaining balance."] });
    }

    var receiptNumber = NormalizeOptionalText(request.ReceiptNumber) ?? await GenerateReceiptNumberAsync(db, tenant.TenantId, cancellationToken);
    var duplicateReceipt = await db.LeadPaymentTransactions.AnyAsync(
        item => item.TenantId == tenant.TenantId && item.ReceiptNumber == receiptNumber,
        cancellationToken);
    if (duplicateReceipt)
    {
        return Results.Conflict(new { message = "A payment transaction with this receipt number already exists." });
    }

    var now = IndianClock.Now();
    var transaction = new LeadPaymentTransaction
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadPaymentId = payment.Id,
        Amount = amount,
        Method = NormalizePaymentMethod(request.Method),
        ReferenceNumber = NormalizeOptionalText(request.ReferenceNumber),
        ReceiptNumber = receiptNumber,
        PaidAt = IndianClock.ToIndianTime(request.PaidAt) ?? now,
        Notes = NormalizeOptionalText(request.Notes),
        CreatedAt = now,
        CreatedByUserId = currentUser?.UserId
    };
    if (transaction.PaidAt > now.AddMinutes(5))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["paidAt"] = ["Payment date cannot be in the future."] });
    }

    db.LeadPaymentTransactions.Add(transaction);
    var nextPaidTotal = paidTotal + amount;
    payment.Status = CalculateLeadPaymentStatus(payment, nextPaidTotal);
    payment.UpdatedAt = now;
    payment.UpdatedByUserId = currentUser?.UserId;
    payment.Version += 1;
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = payment.LeadId,
        CreatedByUserId = currentUser?.UserId,
        Type = "PaymentReceived",
        Description = $"{FormatMoney(amount, payment.Currency)} received for {payment.Title}. Balance: {FormatMoney(payment.AmountDue - nextPaidTotal, payment.Currency)}.",
        CreatedAt = now
    });
    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This payment was changed by another user. Refresh and try again." }); }
    catch (DbUpdateException exception) when (IsUniqueViolation(exception)) { return Results.Conflict(new { message = "A payment transaction with this receipt number already exists." }); }
    return Results.Ok(await GetLeadPaymentsResponseAsync(db, tenant.TenantId, payment.LeadId, cancellationToken));
});

api.MapPost("/leads/{id}/payments/{paymentId:guid}/cancel", async (
    string id,
    Guid paymentId,
    LeadPaymentVersionRequest request,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanManagePayments(currentUser)) return Results.Json(new { message = "Only owners, admins, and accountants can cancel payments." }, statusCode: StatusCodes.Status403Forbidden);

    var payment = await db.LeadPayments
        .Include(item => item.Lead)
        .Include(item => item.Transactions)
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.Id == paymentId && item.Lead.LeadNumber == id, cancellationToken);
    if (payment is null || !CanAccessLead(currentUser, accessScope, payment.Lead)) return Results.NotFound(new { message = "Payment not found." });
    if (payment.Lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before cancelling payments." });
    if (payment.CancelledAt is not null) return Results.Conflict(new { message = "This payment is already cancelled." });
    if (request.Version != payment.Version) return Results.Conflict(new { message = "This payment was changed by another user. Refresh and try again." });
    if (payment.Transactions.Sum(item => item.Amount) > 0)
    {
        return Results.Conflict(new { message = "Payments with received money cannot be cancelled. Record a refund workflow later instead." });
    }

    var now = IndianClock.Now();
    payment.Status = "Cancelled";
    payment.CancelledAt = now;
    payment.CancelledByUserId = currentUser?.UserId;
    payment.UpdatedAt = now;
    payment.UpdatedByUserId = currentUser?.UserId;
    payment.Version += 1;
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = payment.LeadId,
        CreatedByUserId = currentUser?.UserId,
        Type = "PaymentCancelled",
        Description = $"Payment item {payment.Title} cancelled.",
        CreatedAt = now
    });
    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This payment was changed by another user. Refresh and try again." }); }
    return Results.Ok(await GetLeadPaymentsResponseAsync(db, tenant.TenantId, payment.LeadId, cancellationToken));
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
        return Results.Conflict(new { message = "Restore this lead before adding activity." });
    }

    var validationErrors = ValidateAddLeadActivityRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var now = IndianClock.Now();
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = currentUser?.UserId,
        Type = NormalizeActivityType(request.Type),
        Description = NormalizeName(request.Description),
        CreatedAt = now
    });
    var intelligenceSettings = await GetOrCreateLeadIntelligenceSettingsAsync(db, tenant.TenantId, now, cancellationToken);
    await ApplyLeadScoringAsync(db, lead, intelligenceSettings, "Activity added", now, cancellationToken);

    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/leads/{lead.LeadNumber}", await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPost("/leads/{id}/template-activities", async (
    string id,
    ApplyCommunicationTemplateRequest request,
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
        return Results.Json(new { message = "You do not have permission to apply communication templates." }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (request.TemplateId == Guid.Empty)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["templateId"] = ["Select a communication template."] });
    }

    var lead = await db.Leads
        .Include(item => item.Course)
        .Include(item => item.LeadSource)
        .Include(item => item.LeadStage)
        .Include(item => item.AssignedUser)
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
        return Results.Conflict(new { message = "Restore this lead before applying a communication template." });
    }

    var template = await db.CommunicationTemplates
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.Id == request.TemplateId, cancellationToken);
    if (template is null)
    {
        return Results.NotFound(new { message = "Communication template not found." });
    }
    if (!template.IsActive)
    {
        return Results.Conflict(new { message = "This communication template is inactive." });
    }

    var renderedBody = RenderCommunicationTemplate(template.Body, tenant, lead);
    if (!string.IsNullOrWhiteSpace(request.Note))
    {
        renderedBody = $"{renderedBody}\n\nNote: {NormalizeTemplateBody(request.Note)}";
    }

    var now = IndianClock.Now();
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = currentUser?.UserId,
        Type = NormalizeActivityType(template.Channel),
        Description = $"Template: {template.Name}\n{renderedBody}",
        CreatedAt = now
    });
    var intelligenceSettings = await GetOrCreateLeadIntelligenceSettingsAsync(db, tenant.TenantId, now, cancellationToken);
    await ApplyLeadScoringAsync(db, lead, intelligenceSettings, "Communication template applied", now, cancellationToken);

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
    var rescheduleIntelligenceSettings = await GetOrCreateLeadIntelligenceSettingsAsync(db, tenant.TenantId, now, cancellationToken);
    await ApplyLeadScoringAsync(db, lead, rescheduleIntelligenceSettings, "Follow-up rescheduled", now, cancellationToken);

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
    var cancelIntelligenceSettings = await GetOrCreateLeadIntelligenceSettingsAsync(db, tenant.TenantId, now, cancellationToken);
    await ApplyLeadScoringAsync(db, lead, cancelIntelligenceSettings, "Follow-up cancelled", now, cancellationToken);

    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This follow-up was changed by another user. Refresh and try again." }); }
    return Results.Ok(await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPost("/leads/{leadId}/follow-ups/{followUpId}/complete", async (
    string leadId,
    Guid followUpId,
    CompleteFollowUpRequest request,
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

    var validationErrors = ValidateCompleteFollowUpRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var nextAssignedUserId = followUp.AssignedUserId ?? lead.AssignedUserId;
    if (request.NextFollowUp is not null && nextAssignedUserId is not null)
    {
        var assignmentError = await ValidateLeadAssignmentAsync(db, tenant.TenantId, currentUser, accessScope, lead.BranchId, nextAssignedUserId.Value, cancellationToken);
        if (assignmentError is not null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["nextFollowUp.assignedUserId"] = [assignmentError] });
        }
    }

    var now = IndianClock.Now();
    followUp.Status = "Completed";
    followUp.CompletedAt = now;
    followUp.CompletionOutcome = NormalizeCompletionOutcome(request.Outcome);
    followUp.CompletionNotes = NormalizeOptionalText(request.Notes);
    followUp.UpdatedAt = now;
    followUp.Version += 1;
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = currentUser?.UserId,
        Type = "FollowUpCompleted",
        Description = $"{followUp.Type} follow-up completed with outcome '{followUp.CompletionOutcome}' for lead {lead.LeadNumber}.",
        CreatedAt = now
    });

    var nextScheduledAt = await CalculateNextScheduledFollowUpAsync(db, tenant.TenantId, lead.Id, followUp.Id, "Completed", null, cancellationToken);
    if (request.NextFollowUp is not null)
    {
        var nextDueAt = IndianClock.ToIndianTime(request.NextFollowUp.DueAt);
        var nextFollowUp = new FollowUp
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            AssignedUserId = nextAssignedUserId,
            Type = NormalizeFollowUpType(request.NextFollowUp.Type),
            Priority = NormalizePriority(request.NextFollowUp.Priority),
            Status = "Scheduled",
            Version = 1,
            DueAt = nextDueAt,
            CreatedAt = now,
            UpdatedAt = now
        };
        followUp.CompletionNextFollowUpId = nextFollowUp.Id;
        db.FollowUps.Add(nextFollowUp);
        db.Activities.Add(new EducationCrm.Api.Models.Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            CreatedByUserId = currentUser?.UserId,
            Type = "FollowUpScheduled",
            Description = $"{nextFollowUp.Type} follow-up scheduled after completion for lead {lead.LeadNumber}.",
            CreatedAt = now
        });
        nextScheduledAt = nextScheduledAt is null || nextDueAt < nextScheduledAt ? nextDueAt : nextScheduledAt;
    }

    lead.NextFollowUpAt = nextScheduledAt;
    lead.UpdatedAt = now;
    lead.UpdatedByUserId = currentUser?.UserId;
    lead.Version += 1;
    var intelligenceSettings = await GetOrCreateLeadIntelligenceSettingsAsync(db, tenant.TenantId, now, cancellationToken);
    await ApplyLeadScoringAsync(db, lead, intelligenceSettings, "Follow-up completed", now, cancellationToken);

    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "This follow-up was changed by another user. Refresh and try again." }); }
    return Results.Ok(await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPost("/leads/{leadId}/follow-ups/{followUpId}/undo-completion", async (
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
    if (!CanManageLeads(currentUser)) return Results.Json(new { message = "You do not have permission to undo follow-up completion." }, statusCode: StatusCodes.Status403Forbidden);

    var lead = await db.Leads.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == leadId, cancellationToken);
    if (lead is null || !CanAccessLead(currentUser, accessScope, lead)) return Results.NotFound(new { message = "Lead not found." });
    if (lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before undoing follow-up completion." });

    var followUp = await db.FollowUps.FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadId == lead.Id && item.Id == followUpId, cancellationToken);
    if (followUp is null) return Results.NotFound(new { message = "Follow-up not found." });
    if (request.Version != followUp.Version) return Results.Conflict(new { message = "This follow-up was changed by another user. Refresh and try again." });
    if (followUp.Status != "Completed" || followUp.CompletedAt is null) return Results.Conflict(new { message = "Only completed follow-ups can be restored." });
    if (followUp.CompletionNextFollowUpId is not null) return Results.Conflict(new { message = "Completion cannot be undone because a next follow-up was created with it." });

    var now = IndianClock.Now();
    if (now - followUp.CompletedAt.Value > TimeSpan.FromMinutes(2))
    {
        return Results.Conflict(new { message = "The two-minute undo window has expired." });
    }

    followUp.Status = "Scheduled";
    followUp.CompletedAt = null;
    followUp.CompletionOutcome = null;
    followUp.CompletionNotes = null;
    followUp.CompletionNextFollowUpId = null;
    followUp.UpdatedAt = now;
    followUp.Version += 1;
    lead.NextFollowUpAt = await CalculateNextScheduledFollowUpAsync(db, tenant.TenantId, lead.Id, followUp.Id, "Scheduled", followUp.DueAt, cancellationToken);
    lead.UpdatedAt = now;
    lead.UpdatedByUserId = currentUser?.UserId;
    lead.Version += 1;
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = currentUser?.UserId,
        Type = "FollowUpCompletionUndone",
        Description = $"Completion of {followUp.Type} follow-up was undone for lead {lead.LeadNumber}.",
        CreatedAt = now
    });

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
                lead.NextFollowUpAt,
                lead.IntelligenceScore,
                lead.IntelligenceTemperature,
                lead.IntelligenceReason,
                lead.IntelligenceUpdatedAt)
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

api.MapGet("/follow-ups/analytics", async (
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    DateTimeOffset? from,
    DateTimeOffset? to,
    Guid? branchId,
    Guid? assignedUserId,
    CancellationToken cancellationToken) =>
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null)
    {
        return Results.NotFound(new { message = "Tenant not found." });
    }

    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    if (currentUser is null)
    {
        return Results.Json(new { message = "Sign in again to view follow-up analytics." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var now = IndianClock.Now();
    var range = NormalizeFollowUpAnalyticsRange(from, to, now);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    var query = db.FollowUps
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId &&
            item.Lead.ArchivedAt == null &&
            item.DueAt >= range.From &&
            item.DueAt <= range.To);

    if (accessScope.CanViewAll)
    {
        if (branchId is not null)
        {
            query = query.Where(item => item.Lead.BranchId == branchId);
        }
    }
    else if (currentUser.Role == nameof(UserRole.BranchManager) && accessScope.BranchId is not null)
    {
        var scopedBranchId = accessScope.BranchId.Value;
        var currentUserId = currentUser.UserId;
        query = query.Where(item => item.Lead.BranchId == scopedBranchId || item.Lead.AssignedUserId == currentUserId);
    }
    else
    {
        var currentUserId = currentUser.UserId;
        query = query.Where(item => item.Lead.AssignedUserId == currentUserId);
    }

    if (assignedUserId is not null)
    {
        query = query.Where(item => (item.AssignedUserId ?? item.Lead.AssignedUserId) == assignedUserId);
    }

    var followUps = await query
        .Select(item => new
        {
            item.Id,
            item.LeadId,
            item.Type,
            item.Priority,
            item.Status,
            item.DueAt,
            item.CompletedAt,
            item.CompletionOutcome,
            StudentName = item.Lead.StudentName,
            LeadNumber = item.Lead.LeadNumber,
            AssignedTo = item.AssignedUser == null
                ? item.Lead.AssignedUser == null ? "Unassigned" : item.Lead.AssignedUser.FullName
                : item.AssignedUser.FullName,
            IsWonLead = item.Lead.LeadStage.IsWonStage
        })
        .ToListAsync(cancellationToken);

    var scheduled = followUps.Count;
    var completed = followUps.Count(item => string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase));
    var cancelled = followUps.Count(item => string.Equals(item.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
    var overdue = followUps.Count(item => string.Equals(item.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) && item.DueAt < now);
    var open = followUps.Count(item => string.Equals(item.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) && item.DueAt >= now);
    var actionable = Math.Max(0, scheduled - cancelled);
    var completedWithTime = followUps
        .Where(item => string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase) && item.CompletedAt is not null)
        .ToArray();
    var completedOnTime = completedWithTime.Count(item => item.CompletedAt!.Value <= item.DueAt);
    var completedLate = completedWithTime.Count(item => item.CompletedAt!.Value > item.DueAt);
    var averageDelayMinutes = completedWithTime.Length == 0
        ? 0
        : (int)Math.Round(completedWithTime.Average(item => Math.Max(0, (item.CompletedAt!.Value - item.DueAt).TotalMinutes)));
    var convertedLeadIds = followUps
        .Where(item => string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase) && item.IsWonLead)
        .Select(item => item.LeadId)
        .Distinct()
        .Count();

    var response = new FollowUpAnalyticsResponse(
        range.From,
        range.To,
        now,
        new FollowUpAnalyticsSummary(
            scheduled,
            completed,
            cancelled,
            open,
            overdue,
            completedOnTime,
            completedLate,
            averageDelayMinutes,
            Rate(completed, actionable),
            Rate(completedOnTime, completedWithTime.Length),
            convertedLeadIds),
        followUps
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Status) ? "Unknown" : item.Status)
            .Select(group => new FollowUpAnalyticsBreakdown(group.Key, group.Count(), Rate(group.Count(), scheduled)))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label)
            .ToArray(),
        followUps
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Type) ? "Unknown" : item.Type)
            .Select(group => new FollowUpAnalyticsBreakdown(group.Key, group.Count(), Rate(group.Count(), scheduled)))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label)
            .ToArray(),
        followUps
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Priority) ? "Unknown" : item.Priority)
            .Select(group => new FollowUpAnalyticsBreakdown(group.Key, group.Count(), Rate(group.Count(), scheduled)))
            .OrderBy(item => FollowUpPriorityRank(item.Label))
            .ThenBy(item => item.Label)
            .ToArray(),
        followUps
            .GroupBy(item => string.IsNullOrWhiteSpace(item.AssignedTo) ? "Unassigned" : item.AssignedTo)
            .Select(group =>
            {
                var rows = group.ToArray();
                var rowCompleted = rows.Count(item => string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase));
                var rowCancelled = rows.Count(item => string.Equals(item.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
                var rowOverdue = rows.Count(item => string.Equals(item.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) && item.DueAt < now);
                var rowOnTime = rows.Count(item => string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase) && item.CompletedAt is not null && item.CompletedAt.Value <= item.DueAt);
                return new FollowUpCounsellorAnalyticsRow(
                    group.Key,
                    rows.Length,
                    rowCompleted,
                    rowOverdue,
                    rowOnTime,
                    Rate(rowCompleted, Math.Max(0, rows.Length - rowCancelled)),
                    Rate(rowOnTime, rowCompleted));
            })
            .OrderByDescending(item => item.Overdue)
            .ThenByDescending(item => item.Scheduled)
            .ThenBy(item => item.Name)
            .Take(12)
            .ToArray(),
        BuildFollowUpTrend(followUps.Select(item => new FollowUpTrendSource(item.Status, item.DueAt, item.CompletedAt)), range.From, range.To),
        followUps
            .Where(item => string.Equals(item.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) && item.DueAt < now)
            .OrderBy(item => item.DueAt)
            .Take(10)
            .Select(item => new FollowUpOverdueRiskRow(
                item.Id.ToString(),
                item.LeadNumber,
                item.StudentName,
                item.Type,
                item.Priority,
                item.AssignedTo,
                item.DueAt,
                (int)Math.Max(0, Math.Floor((now - item.DueAt).TotalHours))))
            .ToArray());

    return Results.Ok(response);
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
            item.CompletionOutcome,
            item.CompletionNotes,
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

static bool CanUploadLeadDocuments(AuthenticatedUser? user)
{
    return user?.Role is nameof(UserRole.Owner)
        or nameof(UserRole.Admin)
        or nameof(UserRole.BranchManager)
        or nameof(UserRole.Counselor)
        or nameof(UserRole.Telecaller);
}

static bool CanReviewLeadDocuments(AuthenticatedUser? user)
{
    return user?.Role is nameof(UserRole.Owner)
        or nameof(UserRole.Admin)
        or nameof(UserRole.BranchManager);
}

static bool CanManagePayments(AuthenticatedUser? user)
{
    return user?.Role is nameof(UserRole.Owner)
        or nameof(UserRole.Admin)
        or nameof(UserRole.Accountant);
}

static async Task<LeadPaymentsResponse> GetLeadPaymentsResponseAsync(AppDbContext db, Guid tenantId, Guid leadId, CancellationToken cancellationToken)
{
    var payments = await db.LeadPayments
        .AsNoTracking()
        .Include(item => item.CreatedByUser)
        .Include(item => item.UpdatedByUser)
        .Include(item => item.Transactions)
            .ThenInclude(item => item.CreatedByUser)
        .Where(item => item.TenantId == tenantId && item.LeadId == leadId)
        .OrderBy(item => item.DueDate ?? item.CreatedAt)
        .ThenBy(item => item.CreatedAt)
        .ToListAsync(cancellationToken);

    var rows = payments.Select(payment =>
    {
        var transactions = payment.Transactions
            .OrderByDescending(item => item.PaidAt)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new LeadPaymentTransactionResponse(
                item.Id,
                item.Amount,
                item.Method,
                item.ReferenceNumber,
                item.ReceiptNumber,
                item.PaidAt,
                item.Notes,
                item.CreatedAt,
                item.CreatedByUser == null ? "System" : item.CreatedByUser.FullName))
            .ToArray();
        var paid = transactions.Sum(item => item.Amount);
        var status = CalculateLeadPaymentStatus(payment, paid);
        return new LeadPaymentResponse(
            payment.Id,
            payment.Title,
            payment.AmountDue,
            paid,
            Math.Max(0m, payment.AmountDue - paid),
            payment.Currency,
            payment.DueDate,
            status,
            payment.Notes,
            payment.Version,
            payment.CreatedAt,
            payment.UpdatedAt,
            payment.CancelledAt,
            payment.CreatedByUser == null ? "System" : payment.CreatedByUser.FullName,
            payment.UpdatedByUser == null ? "System" : payment.UpdatedByUser.FullName,
            transactions);
    }).ToArray();

    return new LeadPaymentsResponse(rows);
}

static string CalculateLeadPaymentStatus(LeadPayment payment, decimal paidTotal)
{
    if (payment.CancelledAt is not null || payment.Status == "Cancelled") return "Cancelled";
    if (paidTotal >= payment.AmountDue) return "Paid";
    if (payment.DueDate is not null && payment.DueDate.Value.Date < IndianClock.Now().Date) return "Overdue";
    if (paidTotal > 0) return "Partially Paid";
    return "Pending";
}

static Dictionary<string, string[]> ValidateSaveLeadPaymentRequest(SaveLeadPaymentRequest request, bool requireVersion)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "title", request.Title, 160);
    AddOptionalLengthError(errors, "notes", request.Notes, 500);

    if (request.AmountDue <= 0)
    {
        errors["amountDue"] = ["Amount due must be greater than zero."];
    }
    else if (NormalizeMoney(request.AmountDue) != request.AmountDue)
    {
        errors["amountDue"] = ["Amount due can include at most two decimal places."];
    }
    else if (request.AmountDue > 9999999999.99m)
    {
        errors["amountDue"] = ["Amount due is too large."];
    }

    if (NormalizeCurrency(request.Currency) != "INR")
    {
        errors["currency"] = ["Only INR is supported for now."];
    }

    if (request.DueDate is not null && IndianClock.ToIndianTime(request.DueDate.Value) < IndianClock.Now().AddYears(-10))
    {
        errors["dueDate"] = ["Due date is too far in the past."];
    }

    if (requireVersion && request.Version <= 0)
    {
        errors["version"] = ["Payment version is required."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateLeadPaymentTransactionRequest(CreateLeadPaymentTransactionRequest request)
{
    var errors = new Dictionary<string, string[]>();
    AddOptionalLengthError(errors, "referenceNumber", request.ReferenceNumber, 120);
    AddOptionalLengthError(errors, "receiptNumber", request.ReceiptNumber, 120);
    AddOptionalLengthError(errors, "notes", request.Notes, 500);

    if (request.Amount <= 0)
    {
        errors["amount"] = ["Payment amount must be greater than zero."];
    }
    else if (NormalizeMoney(request.Amount) != request.Amount)
    {
        errors["amount"] = ["Payment amount can include at most two decimal places."];
    }
    else if (request.Amount > 9999999999.99m)
    {
        errors["amount"] = ["Payment amount is too large."];
    }

    if (!AllowedPaymentMethods().Contains(NormalizePaymentMethod(request.Method)))
    {
        errors["method"] = [$"Payment method must be one of: {string.Join(", ", AllowedPaymentMethods())}."];
    }

    if (request.Version <= 0)
    {
        errors["version"] = ["Payment version is required."];
    }

    if (request.PaidAt is not null && IndianClock.ToIndianTime(request.PaidAt.Value) > IndianClock.Now().AddMinutes(5))
    {
        errors["paidAt"] = ["Payment date cannot be in the future."];
    }

    return errors;
}

static decimal NormalizeMoney(decimal value)
{
    return Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

static string NormalizeCurrency(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? "INR" : value.Trim().ToUpperInvariant();
}

static string NormalizePaymentMethod(string? value)
{
    var normalized = NormalizeOptionalText(value) ?? "UPI";
    return AllowedPaymentMethods().FirstOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)) ?? normalized;
}

static IReadOnlyCollection<string> AllowedPaymentMethods() => ["Cash", "UPI", "Bank Transfer", "Card", "Cheque", "Other"];

static string FormatMoney(decimal amount, string currency)
{
    return $"{NormalizeCurrency(currency)} {NormalizeMoney(amount).ToString("0.00", CultureInfo.InvariantCulture)}";
}

static async Task<string> GenerateReceiptNumberAsync(AppDbContext db, Guid tenantId, CancellationToken cancellationToken)
{
    for (var attempt = 0; attempt < 5; attempt++)
    {
        var receipt = $"RCPT-{IndianClock.Now():yyyyMMdd}-{RandomNumberGenerator.GetInt32(100000, 999999)}";
        var exists = await db.LeadPaymentTransactions.AnyAsync(item => item.TenantId == tenantId && item.ReceiptNumber == receipt, cancellationToken);
        if (!exists) return receipt;
    }

    return $"RCPT-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
}

static async Task<IResult> ReviewLeadDocumentAsync(
    string leadNumber,
    Guid documentId,
    ReviewLeadDocumentRequest request,
    string status,
    HttpContext httpContext,
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken)
{
    var tenant = await TenantResolver.ResolveAsync(httpContext, db, configuration, cancellationToken);
    if (tenant is null) return Results.NotFound(new { message = "Tenant not found." });
    var currentUser = TenantResolver.GetCurrentUser(httpContext);
    var accessScope = await GetLeadAccessScopeAsync(db, currentUser, cancellationToken);
    if (!CanReviewLeadDocuments(currentUser)) return Results.Json(new { message = "Only owners, admins, and branch managers can review documents." }, statusCode: StatusCodes.Status403Forbidden);

    var document = await db.LeadDocuments
        .Include(item => item.Lead)
        .Include(item => item.DocumentType)
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.Id == documentId && item.Lead.LeadNumber == leadNumber, cancellationToken);
    if (document is null || !CanAccessLead(currentUser, accessScope, document.Lead)) return Results.NotFound(new { message = "Document not found." });
    if (document.Lead.ArchivedAt is not null) return Results.Conflict(new { message = "Restore this lead before reviewing documents." });
    if (request.Version != document.Version) return Results.Conflict(new { message = "This document was changed by another user. Refresh and try again." });

    var normalizedNotes = NormalizeOptionalText(request.Notes);
    var now = IndianClock.Now();
    document.Status = status;
    document.Notes = normalizedNotes;
    document.ReviewedAt = now;
    document.ReviewedByUserId = currentUser?.UserId;
    document.UpdatedAt = now;
    document.UpdatedByUserId = currentUser?.UserId;
    document.Version += 1;

    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = document.LeadId,
        CreatedByUserId = currentUser?.UserId,
        Type = status == "Verified" ? "DocumentVerified" : "DocumentRejected",
        Description = status == "Verified"
            ? $"{document.DocumentType.Name} verified for lead {leadNumber}."
            : $"{document.DocumentType.Name} rejected for lead {leadNumber}.",
        CreatedAt = now
    });

    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(new { message = "This document was changed by another user. Refresh and try again." });
    }

    return Results.Ok(await GetLeadDocumentsResponseAsync(db, tenant.TenantId, document.LeadId, cancellationToken));
}

static async Task<LeadDocumentUploadForm> ReadLeadDocumentUploadFormAsync(HttpRequest request, CancellationToken cancellationToken)
{
    var errors = new Dictionary<string, string[]>();
    if (!request.HasFormContentType)
    {
        errors["file"] = ["Upload the document as multipart form data."];
        return new LeadDocumentUploadForm(Guid.Empty, null, null, null, errors);
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file");
    if (!Guid.TryParse(form["documentTypeId"].FirstOrDefault(), out var documentTypeId) || documentTypeId == Guid.Empty)
    {
        errors["documentTypeId"] = ["Select a document type."];
    }

    int? version = null;
    var rawVersion = form["version"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(rawVersion))
    {
        if (int.TryParse(rawVersion, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedVersion) && parsedVersion > 0)
        {
            version = parsedVersion;
        }
        else
        {
            errors["version"] = ["Document version is invalid."];
        }
    }

    var notes = NormalizeOptionalText(form["notes"].FirstOrDefault());
    if (notes?.Length > 500)
    {
        errors["notes"] = ["Notes must be 500 characters or fewer."];
    }

    if (file is null)
    {
        errors["file"] = ["Select a document file."];
    }
    else
    {
        await AddDocumentFileValidationErrorsAsync(errors, file, cancellationToken);
    }

    return new LeadDocumentUploadForm(documentTypeId, version, notes, file, errors);
}

static async Task AddDocumentFileValidationErrorsAsync(Dictionary<string, string[]> errors, IFormFile file, CancellationToken cancellationToken)
{
    if (file.Length <= 0)
    {
        errors["file"] = ["The document file is empty."];
        return;
    }

    if (file.Length > LeadDocumentFileRules.MaximumFileBytes)
    {
        errors["file"] = [$"The document file must be {LeadDocumentFileRules.MaximumFileBytes / 1024 / 1024} MB or smaller."];
        return;
    }

    var fileName = SanitizeFileName(file.FileName);
    if (string.IsNullOrWhiteSpace(fileName))
    {
        errors["file"] = ["The document filename is invalid."];
        return;
    }

    var extension = Path.GetExtension(fileName);
    if (!LeadDocumentFileRules.AllowedContentTypesByExtension.TryGetValue(extension, out var allowedContentTypes))
    {
        errors["file"] = [$"Allowed document formats are: {string.Join(", ", LeadDocumentFileRules.AllowedExtensions.Select(item => item.TrimStart('.')))}."];
        return;
    }

    var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
    if (!allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
    {
        errors["file"] = ["The document content type does not match the selected file format."];
        return;
    }

    if (extension is ".pdf" or ".jpg" or ".jpeg" or ".png")
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (!HasExpectedDocumentSignature(extension, header.AsSpan(0, read)))
        {
            errors["file"] = ["The document file contents do not match the selected file format."];
        }
    }
}

static bool HasExpectedDocumentSignature(string extension, ReadOnlySpan<byte> header)
{
    return extension switch
    {
        ".pdf" => header.Length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
        ".jpg" or ".jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
        ".png" => header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
        _ => true
    };
}

static async Task<LeadDocumentsResponse> GetLeadDocumentsResponseAsync(AppDbContext db, Guid tenantId, Guid leadId, CancellationToken cancellationToken)
{
    var documentTypes = await db.DocumentTypes
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.IsActive)
        .OrderBy(item => item.SortOrder)
        .ThenBy(item => item.Name)
        .ToListAsync(cancellationToken);

    var documents = await db.LeadDocuments
        .AsNoTracking()
        .Include(item => item.DocumentType)
        .Include(item => item.UploadedByUser)
        .Include(item => item.ReviewedByUser)
        .Where(item => item.TenantId == tenantId && item.LeadId == leadId)
        .ToListAsync(cancellationToken);

    var types = documentTypes
        .Concat(documents.Select(item => item.DocumentType).Where(item => documentTypes.All(type => type.Id != item.Id)))
        .OrderBy(item => item.SortOrder)
        .ThenBy(item => item.Name)
        .ToList();

    var documentsByType = documents.ToDictionary(item => item.DocumentTypeId);
    var rows = types.Select(type =>
    {
        documentsByType.TryGetValue(type.Id, out var document);
        return new LeadDocumentChecklistItemResponse(
            type.Id,
            type.Name,
            type.IsRequired,
            type.IsActive,
            type.SortOrder,
            document?.Id,
            document?.Status ?? "Pending",
            document?.OriginalFileName,
            document?.ContentType,
            document?.FileSizeBytes ?? 0,
            document?.Notes,
            document?.Version,
            document?.UploadedAt,
            document?.UpdatedAt,
            document?.ReviewedAt,
            document?.UploadedByUser?.FullName,
            document?.ReviewedByUser?.FullName,
            document is not null && !string.IsNullOrWhiteSpace(document.CloudinarySecureUrl));
    }).ToArray();

    return new LeadDocumentsResponse(rows);
}

static async Task TryDeleteCloudinaryAssetAsync(ILeadDocumentStorage storage, string publicId, string resourceType, string deliveryType, CancellationToken cancellationToken)
{
    try
    {
        await storage.DeleteAsync(publicId, resourceType, deliveryType, cancellationToken);
    }
    catch
    {
        // Metadata is already consistent; failed remote cleanup can be retried manually from Cloudinary.
    }
}

static string SanitizeFileName(string? fileName)
{
    var normalized = Path.GetFileName(fileName ?? string.Empty).Trim();
    normalized = Regex.Replace(normalized, @"[\x00-\x1F<>:""/\\|?*]+", "-");
    normalized = Regex.Replace(normalized, @"\s+", " ");
    return normalized.Length > 240 ? normalized[^240..] : normalized;
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

static FollowUpAnalyticsRange NormalizeFollowUpAnalyticsRange(DateTimeOffset? from, DateTimeOffset? to, DateTimeOffset now)
{
    var defaultFrom = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
    var start = from is null
        ? defaultFrom
        : IndianClock.ToIndianTime(from.Value).Date;
    var end = to is null
        ? now
        : IndianClock.ToIndianTime(to.Value).Date.AddDays(1).AddTicks(-1);

    if (start > end)
    {
        (start, end) = (end.Date, start.Date.AddDays(1).AddTicks(-1));
    }

    var maxStart = end.AddDays(-366);
    if (start < maxStart)
    {
        start = maxStart;
    }

    return new FollowUpAnalyticsRange(start, end);
}

static decimal Rate(int numerator, int denominator)
{
    return denominator <= 0 ? 0m : Math.Round((decimal)numerator / denominator * 100m, 1);
}

static int FollowUpPriorityRank(string priority)
{
    return priority switch
    {
        "Urgent" => 0,
        "High" => 1,
        "Medium" => 2,
        "Low" => 3,
        _ => 4
    };
}

static IReadOnlyCollection<FollowUpTrendPoint> BuildFollowUpTrend(
    IEnumerable<FollowUpTrendSource> followUps,
    DateTimeOffset from,
    DateTimeOffset to)
{
    var rows = followUps.ToArray();
    var dayCount = Math.Min(367, Math.Max(1, (int)Math.Ceiling((to.Date - from.Date).TotalDays) + 1));
    var points = new List<FollowUpTrendPoint>(dayCount);

    for (var index = 0; index < dayCount; index++)
    {
        var date = from.Date.AddDays(index);
        var scheduled = rows.Count(item => item.DueAt.Date == date);
        var completed = rows.Count(item =>
            string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
            item.CompletedAt is not null &&
            item.CompletedAt.Value.Date == date);
        var overdue = rows.Count(item =>
            string.Equals(item.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) &&
            item.DueAt.Date == date &&
            item.DueAt < IndianClock.Now());

        points.Add(new FollowUpTrendPoint(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), scheduled, completed, overdue));
    }

    return points;
}

static IQueryable<Lead> ApplyLeadFilters(
    IQueryable<Lead> query,
    string? search,
    Guid? branchId,
    Guid? courseId,
    Guid? sourceId,
    Guid? stageId,
    Guid? assignedUserId,
    string? priority,
    string? temperature,
    int? minScore,
    int? maxScore,
    string? archive)
{
    var includeArchived = string.Equals(archive, "archived", StringComparison.OrdinalIgnoreCase);
    var onlyActive = string.IsNullOrWhiteSpace(archive) || string.Equals(archive, "active", StringComparison.OrdinalIgnoreCase);
    if (onlyActive)
    {
        query = query.Where(lead => lead.ArchivedAt == null);
    }
    else if (includeArchived)
    {
        query = query.Where(lead => lead.ArchivedAt != null);
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
        var normalizedSearch = search.Trim();
        var normalizedPhoneSearch = NormalizePhone(normalizedSearch);
        query = query.Where(lead =>
            EF.Functions.ILike(lead.StudentName, $"%{normalizedSearch}%") ||
            EF.Functions.ILike(lead.Email, $"%{normalizedSearch}%") ||
            EF.Functions.ILike(lead.Phone, $"%{normalizedSearch}%") ||
            EF.Functions.ILike(lead.LeadNumber, $"%{normalizedSearch}%") ||
            (normalizedPhoneSearch.Length > 0 && lead.NormalizedPhone.Contains(normalizedPhoneSearch)));
    }

    if (branchId is not null) query = query.Where(lead => lead.BranchId == branchId);
    if (courseId is not null) query = query.Where(lead => lead.CourseId == courseId);
    if (sourceId is not null) query = query.Where(lead => lead.LeadSourceId == sourceId);
    if (stageId is not null) query = query.Where(lead => lead.LeadStageId == stageId);
    if (assignedUserId is not null) query = query.Where(lead => lead.AssignedUserId == assignedUserId);
    if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(lead => lead.Priority == priority);
    if (!string.IsNullOrWhiteSpace(temperature)) query = query.Where(lead => lead.IntelligenceTemperature == temperature);
    if (minScore is not null) query = query.Where(lead => lead.IntelligenceScore >= Math.Clamp(minScore.Value, 0, 100));
    if (maxScore is not null) query = query.Where(lead => lead.IntelligenceScore <= Math.Clamp(maxScore.Value, 0, 100));

    return query;
}

static IOrderedQueryable<Lead> ApplyLeadSort(IQueryable<Lead> query, string? sort)
{
    return sort switch
    {
        "oldest" => query.OrderBy(lead => lead.CreatedAt).ThenBy(lead => lead.LeadNumber),
        "name" => query.OrderBy(lead => lead.StudentName).ThenByDescending(lead => lead.CreatedAt),
        "priority" => query
            .OrderByDescending(lead => lead.Priority == "Urgent")
            .ThenByDescending(lead => lead.Priority == "High")
            .ThenByDescending(lead => lead.Priority == "Medium")
            .ThenByDescending(lead => lead.CreatedAt),
        "followUp" => query
            .OrderBy(lead => lead.NextFollowUpAt == null)
            .ThenBy(lead => lead.NextFollowUpAt)
            .ThenByDescending(lead => lead.CreatedAt),
        "scoreHigh" => query
            .OrderByDescending(lead => lead.IntelligenceScore)
            .ThenByDescending(lead => lead.CreatedAt),
        "scoreLow" => query
            .OrderBy(lead => lead.IntelligenceScore)
            .ThenByDescending(lead => lead.CreatedAt),
        _ => query.OrderByDescending(lead => lead.CreatedAt).ThenByDescending(lead => lead.LeadNumber)
    };
}

static async Task<LeadIntelligenceSettings> GetOrCreateLeadIntelligenceSettingsAsync(
    AppDbContext db,
    Guid tenantId,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    var settings = await db.LeadIntelligenceSettings.FirstOrDefaultAsync(item => item.TenantId == tenantId, cancellationToken);
    if (settings is not null)
    {
        return settings;
    }

    settings = new LeadIntelligenceSettings
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.LeadIntelligenceSettings.Add(settings);
    return settings;
}

static async Task ApplyLeadScoringAsync(
    AppDbContext db,
    Lead lead,
    LeadIntelligenceSettings settings,
    string reason,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    if (!settings.ScoringEnabled)
    {
        return;
    }

    var previousScore = lead.IntelligenceScore;
    var previousTemperature = lead.IntelligenceTemperature;
    var calculation = await CalculateLeadScoreAsync(db, lead, settings, now, cancellationToken);
    lead.IntelligenceScore = calculation.Score;
    lead.IntelligenceTemperature = calculation.Temperature;
    lead.IntelligenceReason = calculation.Reason;
    lead.IntelligenceUpdatedAt = now;

    if (previousScore != calculation.Score || !string.Equals(previousTemperature, calculation.Temperature, StringComparison.Ordinal))
    {
        db.LeadIntelligenceEvents.Add(new LeadIntelligenceEvent
        {
            Id = Guid.NewGuid(),
            TenantId = lead.TenantId,
            LeadId = lead.Id,
            EventType = "ScoreChanged",
            PreviousScore = previousScore,
            NewScore = calculation.Score,
            PreviousTemperature = previousTemperature,
            NewTemperature = calculation.Temperature,
            Reason = $"{reason}: {calculation.Reason}",
            CreatedAt = now
        });
    }
}

static async Task<LeadScoreCalculation> CalculateLeadScoreAsync(
    AppDbContext db,
    Lead lead,
    LeadIntelligenceSettings settings,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    var sourceName = await db.LeadSources
        .AsNoTracking()
        .Where(item => item.TenantId == lead.TenantId && item.Id == lead.LeadSourceId)
        .Select(item => item.Name)
        .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
    var stage = await db.LeadStages
        .AsNoTracking()
        .Where(item => item.TenantId == lead.TenantId && item.Id == lead.LeadStageId)
        .Select(item => new { item.IsWonStage, item.IsLostStage, item.SortOrder })
        .FirstOrDefaultAsync(cancellationToken);
    var activityCount = await db.Activities.CountAsync(item => item.TenantId == lead.TenantId && item.LeadId == lead.Id, cancellationToken);
    var completedFollowUps = await db.FollowUps.CountAsync(item => item.TenantId == lead.TenantId && item.LeadId == lead.Id && item.Status == "Completed", cancellationToken);
    var overdueFollowUps = await db.FollowUps.CountAsync(item => item.TenantId == lead.TenantId && item.LeadId == lead.Id && item.Status == "Scheduled" && item.DueAt < now, cancellationToken);

    var priorityScore = lead.Priority switch
    {
        "Urgent" => 100,
        "High" => 80,
        "Medium" => 55,
        "Low" => 30,
        _ => 40
    };
    var sourceScore = sourceName.Contains("referral", StringComparison.OrdinalIgnoreCase) ? 90 :
        sourceName.Contains("walk", StringComparison.OrdinalIgnoreCase) ? 80 :
        sourceName.Contains("website", StringComparison.OrdinalIgnoreCase) ? 70 :
        sourceName.Contains("google", StringComparison.OrdinalIgnoreCase) ? 65 :
        sourceName.Contains("facebook", StringComparison.OrdinalIgnoreCase) ? 55 : 50;
    var responseScore = lead.NextFollowUpAt is null ? 35 :
        lead.NextFollowUpAt <= now.AddHours(24) ? 85 :
        lead.NextFollowUpAt <= now.AddDays(3) ? 65 : 45;
    var engagementScore = Math.Clamp(activityCount * 12 + completedFollowUps * 18 - overdueFollowUps * 20, 0, 100);
    var ageDays = Math.Max(0, (now - lead.CreatedAt).TotalDays);
    var freshnessScore = ageDays switch
    {
        <= 1 => 100,
        <= 3 => 80,
        <= 7 => 60,
        <= 14 => 40,
        _ => 25
    };
    var profileScore = 35;
    if (!string.IsNullOrWhiteSpace(lead.Email)) profileScore += 20;
    if (!string.IsNullOrWhiteSpace(lead.Phone)) profileScore += 20;
    if (!string.IsNullOrWhiteSpace(lead.City)) profileScore += 15;
    if (!string.IsNullOrWhiteSpace(lead.GuardianName)) profileScore += 10;
    profileScore = Math.Clamp(profileScore, 0, 100);

    if (stage?.IsWonStage == true)
    {
        priorityScore = Math.Max(priorityScore, 90);
        engagementScore = Math.Max(engagementScore, 90);
    }
    else if (stage?.IsLostStage == true)
    {
        priorityScore = Math.Min(priorityScore, 15);
        engagementScore = Math.Min(engagementScore, 15);
    }

    var weightedTotal =
        priorityScore * settings.PriorityWeight +
        sourceScore * settings.SourceWeight +
        responseScore * settings.ResponseWeight +
        engagementScore * settings.EngagementWeight +
        freshnessScore * settings.FreshnessWeight +
        profileScore * settings.ProfileWeight;
    var weightTotal = Math.Max(1, settings.PriorityWeight + settings.SourceWeight + settings.ResponseWeight + settings.EngagementWeight + settings.FreshnessWeight + settings.ProfileWeight);
    var score = Math.Clamp((int)Math.Round((decimal)weightedTotal / weightTotal), 0, 100);
    var temperature = score >= settings.HotThreshold ? "Hot" : score >= settings.WarmThreshold ? "Warm" : "Cold";
    var explanation = $"priority {priorityScore}, source {sourceScore}, response {responseScore}, engagement {engagementScore}, freshness {freshnessScore}, profile {profileScore}";
    return new LeadScoreCalculation(score, temperature, explanation);
}

static async Task<LeadDistributionDecision> ResolveLeadDistributionAsync(
    AppDbContext db,
    LeadIntelligenceSettings settings,
    Guid tenantId,
    Guid? branchId,
    Guid courseId,
    Guid sourceId,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    if (!settings.DistributionEnabled)
    {
        return LeadDistributionDecision.None("Distribution is disabled.");
    }

    var rules = await db.LeadDistributionRules
        .Where(item => item.TenantId == tenantId && item.IsActive)
        .OrderBy(item => item.PriorityOrder)
        .ThenBy(item => item.Name)
        .ToListAsync(cancellationToken);
    var matchingRule = rules.FirstOrDefault(rule =>
        (rule.BranchId == null || rule.BranchId == branchId) &&
        (rule.CourseId == null || rule.CourseId == courseId) &&
        (rule.LeadSourceId == null || rule.LeadSourceId == sourceId));

    if (matchingRule is not null)
    {
        var targetUserIds = ParseDistributionTargetUsers(matchingRule.TargetUserIds);
        var selected = await SelectDistributionUserAsync(db, tenantId, branchId, targetUserIds, matchingRule.Strategy, matchingRule.LastAssignedUserId, settings.MaxActiveLeadsPerUser, cancellationToken);
        if (selected is not null)
        {
            matchingRule.LastAssignedUserId = selected.Value;
            matchingRule.UpdatedAt = now;
            matchingRule.Version += 1;
            return LeadDistributionDecision.Assigned(selected.Value, matchingRule.Id, $"{matchingRule.Strategy} rule '{matchingRule.Name}' matched.");
        }
    }

    if (!string.Equals(settings.DefaultDistributionStrategy, "Disabled", StringComparison.OrdinalIgnoreCase))
    {
        var selected = await SelectDistributionUserAsync(db, tenantId, branchId, [], settings.DefaultDistributionStrategy, null, settings.MaxActiveLeadsPerUser, cancellationToken);
        if (selected is not null)
        {
            return LeadDistributionDecision.Assigned(selected.Value, null, $"{settings.DefaultDistributionStrategy} fallback selected an eligible user.");
        }
    }

    return LeadDistributionDecision.None("No eligible distribution user was found.");
}

static async Task<Guid?> SelectDistributionUserAsync(
    AppDbContext db,
    Guid tenantId,
    Guid? branchId,
    IReadOnlyCollection<Guid> targetUserIds,
    string strategy,
    Guid? lastAssignedUserId,
    int maxActiveLeadsPerUser,
    CancellationToken cancellationToken)
{
    var eligibleRoles = new[] { UserRole.Owner, UserRole.Admin, UserRole.BranchManager, UserRole.Counselor, UserRole.Telecaller };
    var users = await db.Users
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.IsActive && eligibleRoles.Contains(item.Role))
        .Where(item => targetUserIds.Count == 0 || targetUserIds.Contains(item.Id))
        .Where(item => branchId == null || item.BranchId == null || item.BranchId == branchId)
        .OrderBy(item => item.FullName)
        .Select(item => new { item.Id, item.FullName })
        .ToListAsync(cancellationToken);
    if (users.Count == 0)
    {
        return null;
    }

    var activeLoad = await db.Leads
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.ArchivedAt == null && item.AssignedUserId != null)
        .Where(item => users.Select(user => user.Id).Contains(item.AssignedUserId!.Value))
        .GroupBy(item => item.AssignedUserId!.Value)
        .Select(group => new { UserId = group.Key, Count = group.Count() })
        .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);
    var availableUsers = users
        .Where(user => !activeLoad.TryGetValue(user.Id, out var load) || load < maxActiveLeadsPerUser)
        .ToList();
    if (availableUsers.Count == 0)
    {
        return null;
    }

    if (string.Equals(strategy, "Workload", StringComparison.OrdinalIgnoreCase))
    {
        return availableUsers
            .OrderBy(user => activeLoad.TryGetValue(user.Id, out var load) ? load : 0)
            .ThenBy(user => user.FullName)
            .First().Id;
    }

    if (lastAssignedUserId is null)
    {
        return availableUsers.First().Id;
    }

    var lastIndex = availableUsers.FindIndex(user => user.Id == lastAssignedUserId);
    return availableUsers[(lastIndex + 1 + availableUsers.Count) % availableUsers.Count].Id;
}

static IReadOnlyCollection<Guid> ParseDistributionTargetUsers(string targetUserIds)
{
    if (string.IsNullOrWhiteSpace(targetUserIds))
    {
        return [];
    }

    return targetUserIds
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(item => Guid.TryParse(item, out var id) ? id : Guid.Empty)
        .Where(item => item != Guid.Empty)
        .Distinct()
        .ToArray();
}

static ReportDateRangeResult ResolveReportDateRange(string? startDate, string? endDate)
{
    var errors = new Dictionary<string, string[]>();
    var today = DateOnly.FromDateTime(IndianClock.Now().Date);
    var parsedEnd = today;
    var parsedStart = today.AddDays(-29);

    if (!string.IsNullOrWhiteSpace(startDate) &&
        !DateOnly.TryParseExact(startDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedStart))
    {
        errors["startDate"] = ["Use a valid start date in YYYY-MM-DD format."];
    }

    if (!string.IsNullOrWhiteSpace(endDate) &&
        !DateOnly.TryParseExact(endDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedEnd))
    {
        errors["endDate"] = ["Use a valid end date in YYYY-MM-DD format."];
    }

    if (errors.Count == 0 && parsedStart > parsedEnd)
    {
        errors["dateRange"] = ["Start date must be on or before end date."];
    }
    if (errors.Count == 0 && parsedEnd.DayNumber - parsedStart.DayNumber > 366)
    {
        errors["dateRange"] = ["Reports can cover at most 367 days at a time."];
    }
    if (errors.Count == 0 &&
        (parsedStart == DateOnly.MinValue || parsedEnd == DateOnly.MaxValue))
    {
        errors["dateRange"] = ["The requested date range is outside the supported timestamp range."];
    }

    if (errors.Count > 0)
    {
        return new ReportDateRangeResult(null, errors);
    }

    var start = new DateTimeOffset(parsedStart.ToDateTime(TimeOnly.MinValue), IndianClock.Offset);
    var endExclusive = new DateTimeOffset(parsedEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), IndianClock.Offset);
    return new ReportDateRangeResult(new ReportDateRange(parsedStart, parsedEnd, start, endExclusive), errors);
}

static async Task<AdvancedDashboardResponse> BuildAdvancedDashboardAsync(
    AppDbContext db,
    Guid tenantId,
    AuthenticatedUser? currentUser,
    LeadAccessScope accessScope,
    ReportDateRange range,
    Guid? branchId,
    Guid? courseId,
    Guid? assignedUserId,
    CancellationToken cancellationToken)
{
    var now = IndianClock.Now();
    var scopedLeads = ApplyLeadAccessScope(
        db.Leads.AsNoTracking().Where(lead => lead.TenantId == tenantId && lead.ArchivedAt == null),
        currentUser,
        accessScope);

    if (branchId is not null) scopedLeads = scopedLeads.Where(lead => lead.BranchId == branchId);
    if (courseId is not null) scopedLeads = scopedLeads.Where(lead => lead.CourseId == courseId);
    if (assignedUserId is not null) scopedLeads = scopedLeads.Where(lead => lead.AssignedUserId == assignedUserId);

    var scopedLeadIds = scopedLeads.Select(lead => lead.Id);
    var periodLeadsQuery = scopedLeads.Where(lead => lead.CreatedAt >= range.Start && lead.CreatedAt < range.EndExclusive);
    var periodLeads = await periodLeadsQuery
        .Select(lead => new AdvancedLeadAnalyticsRow(
            lead.Id,
            lead.BranchId,
            lead.Branch == null ? "No branch" : lead.Branch.Name,
            lead.CourseId,
            lead.Course.Name,
            lead.AssignedUserId,
            lead.AssignedUser == null ? "Unassigned" : lead.AssignedUser.FullName,
            lead.CreatedAt,
            lead.LeadStage.IsWonStage,
            lead.LeadStage.IsLostStage))
        .ToListAsync(cancellationToken);

    var applicationQuery = db.AdmissionApplications.AsNoTracking()
        .Where(item =>
            item.TenantId == tenantId &&
            scopedLeadIds.Contains(item.LeadId) &&
            item.CreatedAt >= range.Start &&
            item.CreatedAt < range.EndExclusive);
    if (branchId is not null) applicationQuery = applicationQuery.Where(item => item.BranchId == branchId);
    if (courseId is not null) applicationQuery = applicationQuery.Where(item => item.CourseId == courseId);
    if (assignedUserId is not null) applicationQuery = applicationQuery.Where(item => item.Lead.AssignedUserId == assignedUserId);

    var applications = await applicationQuery
        .Select(item => new AdvancedActivityAnalyticsRow(
            item.LeadId,
            item.BranchId,
            item.Branch == null ? "No branch" : item.Branch.Name,
            item.CourseId,
            item.Course.Name,
            item.Lead.AssignedUserId,
            item.Lead.AssignedUser == null ? "Unassigned" : item.Lead.AssignedUser.FullName,
            item.CreatedAt,
            item.Status))
        .ToListAsync(cancellationToken);

    var enrollmentQuery = db.Enrollments.AsNoTracking()
        .Where(item =>
            item.TenantId == tenantId &&
            scopedLeadIds.Contains(item.LeadId) &&
            item.EnrolledAt >= range.Start &&
            item.EnrolledAt < range.EndExclusive);
    if (branchId is not null) enrollmentQuery = enrollmentQuery.Where(item => item.BranchId == branchId);
    if (courseId is not null) enrollmentQuery = enrollmentQuery.Where(item => item.CourseId == courseId);
    if (assignedUserId is not null) enrollmentQuery = enrollmentQuery.Where(item => item.Lead.AssignedUserId == assignedUserId);

    var enrollments = await enrollmentQuery
        .Select(item => new AdvancedActivityAnalyticsRow(
            item.LeadId,
            item.BranchId,
            item.Branch == null ? "No branch" : item.Branch.Name,
            item.CourseId,
            item.Course.Name,
            item.Lead.AssignedUserId,
            item.Lead.AssignedUser == null ? "Unassigned" : item.Lead.AssignedUser.FullName,
            item.EnrolledAt,
            item.Status))
        .ToListAsync(cancellationToken);

    var activePayments = db.LeadPayments.AsNoTracking()
        .Where(item =>
            item.TenantId == tenantId &&
            item.CancelledAt == null &&
            item.Status != "Cancelled" &&
            scopedLeadIds.Contains(item.LeadId));

    var periodPaymentItems = await activePayments
        .Where(item => item.CreatedAt >= range.Start && item.CreatedAt < range.EndExclusive)
        .Select(item => new AdvancedPaymentItemAnalyticsRow(
            item.Id,
            item.LeadId,
            item.Lead.BranchId,
            item.Lead.Branch == null ? "No branch" : item.Lead.Branch.Name,
            item.Lead.CourseId,
            item.Lead.Course.Name,
            item.Lead.AssignedUserId,
            item.Lead.AssignedUser == null ? "Unassigned" : item.Lead.AssignedUser.FullName,
            item.CreatedAt,
            item.AmountDue,
            item.Transactions.Sum(transaction => (decimal?)transaction.Amount) ?? 0m))
        .ToListAsync(cancellationToken);

    var periodTransactions = await db.LeadPaymentTransactions.AsNoTracking()
        .Where(item =>
            item.TenantId == tenantId &&
            item.PaidAt >= range.Start &&
            item.PaidAt < range.EndExclusive &&
            item.LeadPayment.CancelledAt == null &&
            item.LeadPayment.Status != "Cancelled" &&
            scopedLeadIds.Contains(item.LeadPayment.LeadId))
        .Select(item => new AdvancedPaymentTransactionAnalyticsRow(
            item.LeadPayment.LeadId,
            item.LeadPayment.Lead.BranchId,
            item.LeadPayment.Lead.Branch == null ? "No branch" : item.LeadPayment.Lead.Branch.Name,
            item.LeadPayment.Lead.CourseId,
            item.LeadPayment.Lead.Course.Name,
            item.LeadPayment.Lead.AssignedUserId,
            item.LeadPayment.Lead.AssignedUser == null ? "Unassigned" : item.LeadPayment.Lead.AssignedUser.FullName,
            item.PaidAt,
            item.Amount))
        .ToListAsync(cancellationToken);

    var expectedRevenue = periodPaymentItems.Sum(item => item.AmountDue);
    var collectedRevenue = periodTransactions.Sum(item => item.Amount);
    var pendingBalance = periodPaymentItems.Sum(item => Math.Max(0m, item.AmountDue - item.PaidAllTime));
    var totalLeads = periodLeads.Count;
    var enrolledLeads = enrollments.Select(item => item.LeadId).Distinct().Count();
    var approvedApplications = applications.Count(item => string.Equals(item.Status, "Approved", StringComparison.OrdinalIgnoreCase));

    var allActivePaymentBalances = await activePayments
        .Select(item => new
        {
            item.LeadId,
            Balance = item.AmountDue - (item.Transactions.Sum(transaction => (decimal?)transaction.Amount) ?? 0m),
            item.DueDate
        })
        .ToListAsync(cancellationToken);
    var clearPaymentLeadCount = allActivePaymentBalances
        .Where(item => item.Balance <= 0)
        .Select(item => item.LeadId)
        .Distinct()
        .Count();

    var overdueFollowUps = await db.FollowUps.AsNoTracking().CountAsync(
        item =>
            item.TenantId == tenantId &&
            item.Status == "Scheduled" &&
            item.DueAt < now &&
            scopedLeadIds.Contains(item.LeadId),
        cancellationToken);
    var overduePaymentCount = allActivePaymentBalances.Count(item => item.Balance > 0 && item.DueDate != null && item.DueDate < now);
    var approvedNotEnrolled = await db.AdmissionApplications.AsNoTracking().CountAsync(
        item =>
            item.TenantId == tenantId &&
            item.Status == "Approved" &&
            item.Enrollment == null &&
            scopedLeadIds.Contains(item.LeadId),
        cancellationToken);
    var leadsWithoutNextAction = await scopedLeads.CountAsync(
        lead => lead.NextFollowUpAt == null && !lead.LeadStage.IsWonStage && !lead.LeadStage.IsLostStage,
        cancellationToken);

    var alerts = new[]
    {
        new AdvancedDashboardAlert("overdueFollowUps", "Overdue follow-ups", overdueFollowUps, overdueFollowUps > 0 ? "danger" : "success", "Scheduled conversations that already crossed their due time."),
        new AdvancedDashboardAlert("overduePayments", "Payment balance due", overduePaymentCount, overduePaymentCount > 0 ? "warning" : "success", "Active fee items with unpaid balance and a past due date."),
        new AdvancedDashboardAlert("approvedNotEnrolled", "Approved not enrolled", approvedNotEnrolled, approvedNotEnrolled > 0 ? "warning" : "success", "Approved applications that still need enrollment completion."),
        new AdvancedDashboardAlert("noNextAction", "Leads without next action", leadsWithoutNextAction, leadsWithoutNextAction > 0 ? "neutral" : "success", "Open leads that do not have a planned follow-up.")
    };

    var funnel = BuildAdvancedFunnel(totalLeads, applications.Select(item => item.LeadId).Distinct().Count(), approvedApplications, enrolledLeads, clearPaymentLeadCount);
    var trend = BuildAdvancedRevenueTrend(range, periodLeads, applications, enrollments, periodPaymentItems, periodTransactions);
    var courseRows = BuildAdvancedPerformanceRows("course", periodLeads, applications, enrollments, periodPaymentItems, periodTransactions);
    var branchRows = BuildAdvancedPerformanceRows("branch", periodLeads, applications, enrollments, periodPaymentItems, periodTransactions);
    var counselorRows = BuildAdvancedPerformanceRows("counselor", periodLeads, applications, enrollments, periodPaymentItems, periodTransactions);

    return new AdvancedDashboardResponse(
        IndianClock.Now(),
        range.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        range.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        new ReportAccessResponse(accessScope.CanViewAll ? "All accessible tenant leads" : "Scoped to your assigned leads and branch", currentUser?.Role ?? string.Empty),
        new AdvancedDashboardSummaryResponse(
            totalLeads,
            applications.Select(item => item.LeadId).Distinct().Count(),
            approvedApplications,
            enrolledLeads,
            expectedRevenue,
            collectedRevenue,
            pendingBalance,
            CalculateMoneyRate(collectedRevenue, expectedRevenue),
            CalculateRate(enrolledLeads, Math.Max(totalLeads, 1)),
            overdueFollowUps),
        trend,
        funnel,
        courseRows,
        branchRows,
        counselorRows,
        alerts);
}

static IReadOnlyCollection<AdvancedDashboardFunnelStep> BuildAdvancedFunnel(int leads, int applications, int approved, int enrolled, int paymentsCleared)
{
    var baseline = Math.Max(leads, 1);
    return
    [
        new AdvancedDashboardFunnelStep("leads", "Leads", leads, CalculateRate(leads, baseline)),
        new AdvancedDashboardFunnelStep("applications", "Applications", applications, CalculateRate(applications, baseline)),
        new AdvancedDashboardFunnelStep("approved", "Approved", approved, CalculateRate(approved, baseline)),
        new AdvancedDashboardFunnelStep("enrolled", "Enrolled", enrolled, CalculateRate(enrolled, baseline)),
        new AdvancedDashboardFunnelStep("paymentsCleared", "Payments Cleared", paymentsCleared, CalculateRate(paymentsCleared, baseline))
    ];
}

static IReadOnlyCollection<AdvancedDashboardTrendPoint> BuildAdvancedRevenueTrend(
    ReportDateRange range,
    IReadOnlyCollection<AdvancedLeadAnalyticsRow> leads,
    IReadOnlyCollection<AdvancedActivityAnalyticsRow> applications,
    IReadOnlyCollection<AdvancedActivityAnalyticsRow> enrollments,
    IReadOnlyCollection<AdvancedPaymentItemAnalyticsRow> paymentItems,
    IReadOnlyCollection<AdvancedPaymentTransactionAnalyticsRow> transactions)
{
    var days = (range.EndDate.ToDateTime(TimeOnly.MinValue) - range.StartDate.ToDateTime(TimeOnly.MinValue)).Days + 1;
    var monthly = days > 45;
    var points = new List<AdvancedDashboardTrendPoint>();

    if (monthly)
    {
        var cursor = new DateOnly(range.StartDate.Year, range.StartDate.Month, 1);
        var endMonth = new DateOnly(range.EndDate.Year, range.EndDate.Month, 1);
        while (cursor <= endMonth)
        {
            var bucketStart = cursor;
            var bucketEnd = cursor.AddMonths(1);
            points.Add(BuildAdvancedTrendPoint(
                cursor.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                bucketStart,
                bucketEnd,
                leads,
                applications,
                enrollments,
                paymentItems,
                transactions));
            cursor = cursor.AddMonths(1);
        }
        return points;
    }

    for (var cursor = range.StartDate; cursor <= range.EndDate; cursor = cursor.AddDays(1))
    {
        points.Add(BuildAdvancedTrendPoint(
            cursor.ToString("dd MMM", CultureInfo.InvariantCulture),
            cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            cursor,
            cursor.AddDays(1),
            leads,
            applications,
            enrollments,
            paymentItems,
            transactions));
    }

    return points;
}

static AdvancedDashboardTrendPoint BuildAdvancedTrendPoint(
    string label,
    string date,
    DateOnly start,
    DateOnly endExclusive,
    IReadOnlyCollection<AdvancedLeadAnalyticsRow> leads,
    IReadOnlyCollection<AdvancedActivityAnalyticsRow> applications,
    IReadOnlyCollection<AdvancedActivityAnalyticsRow> enrollments,
    IReadOnlyCollection<AdvancedPaymentItemAnalyticsRow> paymentItems,
    IReadOnlyCollection<AdvancedPaymentTransactionAnalyticsRow> transactions)
{
    static DateOnly ToLocalDate(DateTimeOffset value) => DateOnly.FromDateTime(value.ToOffset(IndianClock.Offset).DateTime);
    var bucketPayments = paymentItems.Where(item => ToLocalDate(item.CreatedAt) >= start && ToLocalDate(item.CreatedAt) < endExclusive).ToArray();
    return new AdvancedDashboardTrendPoint(
        label,
        date,
        bucketPayments.Sum(item => item.AmountDue),
        transactions.Where(item => ToLocalDate(item.PaidAt) >= start && ToLocalDate(item.PaidAt) < endExclusive).Sum(item => item.Amount),
        bucketPayments.Sum(item => Math.Max(0m, item.AmountDue - item.PaidAllTime)),
        leads.Count(item => ToLocalDate(item.CreatedAt) >= start && ToLocalDate(item.CreatedAt) < endExclusive),
        applications.Count(item => ToLocalDate(item.OccurredAt) >= start && ToLocalDate(item.OccurredAt) < endExclusive),
        enrollments.Count(item => ToLocalDate(item.OccurredAt) >= start && ToLocalDate(item.OccurredAt) < endExclusive));
}

static IReadOnlyCollection<AdvancedDashboardPerformanceRow> BuildAdvancedPerformanceRows(
    string dimension,
    IReadOnlyCollection<AdvancedLeadAnalyticsRow> leads,
    IReadOnlyCollection<AdvancedActivityAnalyticsRow> applications,
    IReadOnlyCollection<AdvancedActivityAnalyticsRow> enrollments,
    IReadOnlyCollection<AdvancedPaymentItemAnalyticsRow> paymentItems,
    IReadOnlyCollection<AdvancedPaymentTransactionAnalyticsRow> transactions)
{
    var rows = new Dictionary<string, AdvancedDashboardPerformanceAccumulator>(StringComparer.OrdinalIgnoreCase);

    AdvancedDashboardPerformanceAccumulator Get(Guid? id, string name)
    {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? "Unassigned" : name;
        var key = id?.ToString("N", CultureInfo.InvariantCulture) ?? $"none:{normalizedName}";
        if (!rows.TryGetValue(key, out var row))
        {
            row = new AdvancedDashboardPerformanceAccumulator(id, normalizedName);
            rows[key] = row;
        }
        return row;
    }

    (Guid? Id, string Name) LeadKey(AdvancedLeadAnalyticsRow row) => dimension switch
    {
        "branch" => (row.BranchId, row.Branch),
        "counselor" => (row.AssignedUserId, row.Counselor),
        _ => (row.CourseId, row.Course)
    };
    (Guid? Id, string Name) ActivityKey(AdvancedActivityAnalyticsRow row) => dimension switch
    {
        "branch" => (row.BranchId, row.Branch),
        "counselor" => (row.AssignedUserId, row.Counselor),
        _ => (row.CourseId, row.Course)
    };
    (Guid? Id, string Name) PaymentItemKey(AdvancedPaymentItemAnalyticsRow row) => dimension switch
    {
        "branch" => (row.BranchId, row.Branch),
        "counselor" => (row.AssignedUserId, row.Counselor),
        _ => (row.CourseId, row.Course)
    };
    (Guid? Id, string Name) TransactionKey(AdvancedPaymentTransactionAnalyticsRow row) => dimension switch
    {
        "branch" => (row.BranchId, row.Branch),
        "counselor" => (row.AssignedUserId, row.Counselor),
        _ => (row.CourseId, row.Course)
    };

    foreach (var lead in leads)
    {
        var key = LeadKey(lead);
        var row = Get(key.Id, key.Name);
        row.Leads += 1;
    }
    foreach (var application in applications)
    {
        var key = ActivityKey(application);
        Get(key.Id, key.Name).Applications += 1;
    }
    foreach (var enrollment in enrollments)
    {
        var key = ActivityKey(enrollment);
        Get(key.Id, key.Name).Enrollments += 1;
    }
    foreach (var payment in paymentItems)
    {
        var key = PaymentItemKey(payment);
        var row = Get(key.Id, key.Name);
        row.ExpectedRevenue += payment.AmountDue;
        row.PendingBalance += Math.Max(0m, payment.AmountDue - payment.PaidAllTime);
    }
    foreach (var transaction in transactions)
    {
        var key = TransactionKey(transaction);
        Get(key.Id, key.Name).CollectedRevenue += transaction.Amount;
    }

    return rows.Values
        .Select(item => new AdvancedDashboardPerformanceRow(
            item.Id,
            item.Name,
            item.Leads,
            item.Applications,
            item.Enrollments,
            item.ExpectedRevenue,
            item.CollectedRevenue,
            item.PendingBalance,
            CalculateRate(item.Enrollments, Math.Max(item.Leads, 1)),
            CalculateMoneyRate(item.CollectedRevenue, item.ExpectedRevenue)))
        .OrderByDescending(item => item.CollectedRevenue)
        .ThenByDescending(item => item.Enrollments)
        .ThenBy(item => item.Name)
        .Take(8)
        .ToArray();
}

static async Task<ReportsResponse> BuildReportsAsync(
    AppDbContext db,
    Guid tenantId,
    AuthenticatedUser? currentUser,
    LeadAccessScope accessScope,
    ReportDateRange range,
    Guid? branchId,
    Guid? courseId,
    Guid? sourceId,
    Guid? assignedUserId,
    CancellationToken cancellationToken)
{
    var scopedLeads = ApplyLeadAccessScope(
        db.Leads.AsNoTracking().Where(lead => lead.TenantId == tenantId && lead.ArchivedAt == null),
        currentUser,
        accessScope);

    if (branchId is not null) scopedLeads = scopedLeads.Where(lead => lead.BranchId == branchId);
    if (courseId is not null) scopedLeads = scopedLeads.Where(lead => lead.CourseId == courseId);
    if (sourceId is not null) scopedLeads = scopedLeads.Where(lead => lead.LeadSourceId == sourceId);
    if (assignedUserId is not null) scopedLeads = scopedLeads.Where(lead => lead.AssignedUserId == assignedUserId);

    var periodLeads = scopedLeads.Where(lead => lead.CreatedAt >= range.Start && lead.CreatedAt < range.EndExclusive);
    var total = await periodLeads.CountAsync(cancellationToken);
    var won = await periodLeads.CountAsync(lead => lead.LeadStage.IsWonStage, cancellationToken);
    var lost = await periodLeads.CountAsync(lead => lead.LeadStage.IsLostStage, cancellationToken);
    var open = Math.Max(0, total - won - lost);
    var contacted = await periodLeads.CountAsync(lead => lead.LeadStage.SortOrder >= 20, cancellationToken);

    var scopedLeadIds = scopedLeads.Select(lead => lead.Id);
    var followUps = db.FollowUps.AsNoTracking()
        .Where(item => item.TenantId == tenantId && scopedLeadIds.Contains(item.LeadId));
    var scheduledFollowUps = await followUps.CountAsync(
        item => item.Status == "Scheduled" && item.DueAt >= range.Start && item.DueAt < range.EndExclusive,
        cancellationToken);
    var completedFollowUps = await followUps.CountAsync(
        item => item.Status == "Completed" && item.CompletedAt != null && item.CompletedAt >= range.Start && item.CompletedAt < range.EndExclusive,
        cancellationToken);
    var overdueFollowUps = await followUps.CountAsync(
        item => item.Status == "Scheduled" && item.DueAt < IndianClock.Now(),
        cancellationToken);

    var sourceRows = await periodLeads
        .GroupBy(lead => new { lead.LeadSourceId, lead.LeadSource.Name })
        .Select(group => new
        {
            Id = group.Key.LeadSourceId,
            Name = group.Key.Name,
            Total = group.Count(),
            Won = group.Count(lead => lead.LeadStage.IsWonStage),
            Lost = group.Count(lead => lead.LeadStage.IsLostStage)
        })
        .OrderByDescending(item => item.Total)
        .ThenBy(item => item.Name)
        .ToListAsync(cancellationToken);

    var stageRows = await periodLeads
        .GroupBy(lead => new
        {
            lead.LeadStageId,
            lead.LeadStage.Name,
            lead.LeadStage.SortOrder,
            lead.LeadStage.IsWonStage,
            lead.LeadStage.IsLostStage
        })
        .Select(group => new
        {
            Id = group.Key.LeadStageId,
            group.Key.Name,
            group.Key.SortOrder,
            group.Key.IsWonStage,
            group.Key.IsLostStage,
            Total = group.Count()
        })
        .OrderBy(item => item.SortOrder)
        .ToListAsync(cancellationToken);

    var counselorLeadRows = await periodLeads
        .GroupBy(lead => new
        {
            lead.AssignedUserId,
            Name = lead.AssignedUser == null ? "Unassigned" : lead.AssignedUser.FullName
        })
        .Select(group => new
        {
            group.Key.AssignedUserId,
            group.Key.Name,
            Total = group.Count(),
            Won = group.Count(lead => lead.LeadStage.IsWonStage),
            Lost = group.Count(lead => lead.LeadStage.IsLostStage)
        })
        .ToListAsync(cancellationToken);

    var counselorFollowUpRows = await followUps
        .Where(item =>
            (item.DueAt >= range.Start && item.DueAt < range.EndExclusive) ||
            (item.CompletedAt != null && item.CompletedAt >= range.Start && item.CompletedAt < range.EndExclusive) ||
            (item.Status == "Scheduled" && item.DueAt < IndianClock.Now()))
        .GroupBy(item => new
        {
            item.AssignedUserId,
            Name = item.AssignedUser == null ? "Unassigned" : item.AssignedUser.FullName
        })
        .Select(group => new
        {
            group.Key.AssignedUserId,
            group.Key.Name,
            Scheduled = group.Count(item => item.Status == "Scheduled" && item.DueAt >= range.Start && item.DueAt < range.EndExclusive),
            Completed = group.Count(item => item.Status == "Completed" && item.CompletedAt != null && item.CompletedAt >= range.Start && item.CompletedAt < range.EndExclusive),
            Overdue = group.Count(item => item.Status == "Scheduled" && item.DueAt < IndianClock.Now())
        })
        .ToListAsync(cancellationToken);

    var counselorKeys = counselorLeadRows
        .Select(item => item.AssignedUserId)
        .Concat(counselorFollowUpRows.Select(item => item.AssignedUserId))
        .Distinct()
        .ToArray();
    var counselorRows = counselorKeys
        .Select(userId =>
        {
            var leadRow = counselorLeadRows.FirstOrDefault(item => item.AssignedUserId == userId);
            var followUpRow = counselorFollowUpRows.FirstOrDefault(item => item.AssignedUserId == userId);
            var rowTotal = leadRow?.Total ?? 0;
            var rowWon = leadRow?.Won ?? 0;
            var rowLost = leadRow?.Lost ?? 0;
            return new CounselorReportRow(
                userId,
                leadRow?.Name ?? followUpRow?.Name ?? "Unassigned",
                rowTotal,
                rowWon,
                rowLost,
                Math.Max(0, rowTotal - rowWon - rowLost),
                followUpRow?.Scheduled ?? 0,
                followUpRow?.Completed ?? 0,
                followUpRow?.Overdue ?? 0,
                CalculateRate(rowWon, rowTotal));
        })
        .OrderByDescending(item => item.TotalLeads)
        .ThenBy(item => item.Counselor)
        .ToArray();

    var sourceReportRows = sourceRows
        .Select(item => new SourceReportRow(
            item.Id,
            item.Name,
            item.Total,
            item.Won,
            item.Lost,
            Math.Max(0, item.Total - item.Won - item.Lost),
            CalculateRate(item.Won, item.Total)))
        .ToArray();

    var stageReportRows = stageRows
        .Select(item => new StageReportRow(
            item.Id,
            item.Name,
            item.SortOrder,
            item.Total,
            CalculateRate(item.Total, total),
            item.IsWonStage,
            item.IsLostStage))
        .ToArray();

    return new ReportsResponse(
        IndianClock.Now(),
        range.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        range.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        new ReportAccessResponse(accessScope.CanViewAll ? "All accessible tenant leads" : "Scoped to your assigned leads and branch", currentUser?.Role ?? string.Empty),
        new ReportSummaryResponse(total, contacted, won, lost, open, scheduledFollowUps, completedFollowUps, overdueFollowUps, CalculateRate(won, total)),
        sourceReportRows,
        counselorRows,
        stageReportRows);
}

static IReadOnlyCollection<ReportExportRow> BuildReportExportRows(ReportsResponse report)
{
    var rows = new List<ReportExportRow>();
    rows.AddRange(report.Sources.Select(item => new ReportExportRow("Lead Source", item.Source, item.TotalLeads, item.WonLeads, item.LostLeads, item.OpenLeads, 0, 0, 0, item.ConversionRate)));
    rows.AddRange(report.Counselors.Select(item => new ReportExportRow("Counsellor", item.Counselor, item.TotalLeads, item.WonLeads, item.LostLeads, item.OpenLeads, item.ScheduledFollowUps, item.CompletedFollowUps, item.OverdueFollowUps, item.ConversionRate)));
    rows.AddRange(report.Stages.Select(item => new ReportExportRow("Pipeline Stage", item.Stage, item.TotalLeads, item.IsWonStage ? item.TotalLeads : 0, item.IsLostStage ? item.TotalLeads : 0, item.IsWonStage || item.IsLostStage ? 0 : item.TotalLeads, 0, 0, 0, item.Percentage)));
    return rows;
}

static decimal CalculateRate(int numerator, int denominator)
{
    return denominator <= 0 ? 0m : Math.Round((decimal)numerator / denominator * 100m, 1);
}

static decimal CalculateMoneyRate(decimal numerator, decimal denominator)
{
    return denominator <= 0m ? 0m : Math.Round(numerator / denominator * 100m, 1);
}

static async Task<LeadImportFormRequest> ReadLeadImportFormAsync(HttpRequest request, bool requireFingerprint, CancellationToken cancellationToken)
{
    if (!request.HasFormContentType)
    {
        return new LeadImportFormRequest(null, null, "skip", null, "Upload the file as multipart form data.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
    {
        return new LeadImportFormRequest(null, null, "skip", null, "Select a CSV or XLSX lead file.");
    }

    var mappingResult = ParseLeadImportMapping(form["mapping"].FirstOrDefault());
    if (mappingResult.Error is not null)
    {
        return new LeadImportFormRequest(null, null, "skip", null, mappingResult.Error);
    }

    var duplicateMode = string.Equals(form["duplicateMode"].FirstOrDefault(), "update", StringComparison.OrdinalIgnoreCase)
        ? "update"
        : "skip";
    var fingerprint = form["fingerprint"].FirstOrDefault()?.Trim();
    if (requireFingerprint && string.IsNullOrWhiteSpace(fingerprint))
    {
        return new LeadImportFormRequest(null, null, duplicateMode, null, "Preview the file before committing the import.");
    }

    var sheet = await LeadFileService.ReadAsync(file, cancellationToken);
    return new LeadImportFormRequest(sheet, mappingResult.Mapping, duplicateMode, fingerprint, null);
}

static LeadImportMappingResult ParseLeadImportMapping(string? mappingJson)
{
    if (string.IsNullOrWhiteSpace(mappingJson))
    {
        return new LeadImportMappingResult(null, null);
    }

    try
    {
        var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(mappingJson);
        return new LeadImportMappingResult(mapping, null);
    }
    catch (JsonException)
    {
        return new LeadImportMappingResult(null, "The column mapping is not valid JSON.");
    }
}

static async Task<LeadImportCommitResponse> CommitLeadImportAsync(
    AppDbContext db,
    Guid tenantId,
    AuthenticatedUser? currentUser,
    LeadImportAnalysis analysis,
    CancellationToken cancellationToken)
{
    var preparedRows = analysis.PreparedRows.ToArray();
    var skipped = analysis.Rows.Count(row => row.Action == "skip");
    if (preparedRows.Length == 0)
    {
        return new LeadImportCommitResponse(0, 0, skipped, analysis.Sheet.Rows.Count, "No leads were imported.");
    }

    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var now = IndianClock.Now();
    var updateIds = preparedRows
        .Where(row => row.Action == "update" && row.ExistingLeadId is not null)
        .Select(row => row.ExistingLeadId!.Value)
        .Distinct()
        .ToArray();
    var existingLeads = updateIds.Length == 0
        ? new Dictionary<Guid, Lead>()
        : await db.Leads
            .Where(item => item.TenantId == tenantId && updateIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

    var nextLeadNumber = await GetNextLeadNumberSeedAsync(db, tenantId, cancellationToken);
    var intelligenceSettings = await GetOrCreateLeadIntelligenceSettingsAsync(db, tenantId, now, cancellationToken);
    var created = 0;
    var updated = 0;

    foreach (var row in preparedRows)
    {
        if (row.Action == "update")
        {
            if (row.ExistingLeadId is null || !existingLeads.TryGetValue(row.ExistingLeadId.Value, out var lead))
            {
                throw new LeadImportException($"Lead for row {row.RowNumber} was not found during import. Preview the file again.");
            }

            lead.BranchId = row.BranchId;
            lead.CourseId = row.CourseId;
            lead.LeadSourceId = row.SourceId;
            lead.LeadStageId = row.StageId;
            lead.AssignedUserId = row.AssignedUserId;
            lead.StudentName = row.StudentName;
            lead.GuardianName = row.GuardianName;
            lead.Email = row.Email;
            lead.Phone = row.Phone;
            lead.NormalizedPhone = row.NormalizedPhone;
            lead.City = row.City;
            lead.Status = row.Status;
            lead.Priority = row.Priority;
            lead.NextFollowUpAt = row.NextFollowUpAt;
            lead.UpdatedAt = now;
            lead.UpdatedByUserId = currentUser?.UserId;
            lead.Version += 1;

            db.Activities.Add(new EducationCrm.Api.Models.Activity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LeadId = lead.Id,
                CreatedByUserId = currentUser?.UserId,
                Type = "LeadUpdated",
                Description = $"Lead {lead.LeadNumber} updated from import row {row.RowNumber}.",
                CreatedAt = now
            });
            await ApplyLeadScoringAsync(db, lead, intelligenceSettings, $"Lead import row {row.RowNumber} updated", now, cancellationToken);
            updated++;
            continue;
        }

        var leadNumber = $"LD-{nextLeadNumber++}";
        var newLead = new Lead
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = row.BranchId,
            CourseId = row.CourseId,
            LeadSourceId = row.SourceId,
            LeadStageId = row.StageId,
            AssignedUserId = row.AssignedUserId,
            LeadNumber = leadNumber,
            StudentName = row.StudentName,
            GuardianName = row.GuardianName,
            Email = row.Email,
            Phone = row.Phone,
            NormalizedPhone = row.NormalizedPhone,
            City = row.City,
            Status = row.Status,
            Priority = row.Priority,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
            NextFollowUpAt = row.NextFollowUpAt,
            CreatedByUserId = currentUser?.UserId,
            UpdatedByUserId = currentUser?.UserId
        };

        db.Leads.Add(newLead);
        db.Activities.Add(new EducationCrm.Api.Models.Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LeadId = newLead.Id,
            CreatedByUserId = currentUser?.UserId,
            Type = "LeadCreated",
            Description = $"Lead {leadNumber} imported for {row.StudentName}.",
            CreatedAt = now
        });
        await ApplyLeadScoringAsync(db, newLead, intelligenceSettings, $"Lead import row {row.RowNumber} created", now, cancellationToken);
        created++;
    }

    await db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    var message = $"Imported {created} new lead{(created == 1 ? string.Empty : "s")}, updated {updated}, skipped {skipped}.";
    return new LeadImportCommitResponse(created, updated, skipped, analysis.Sheet.Rows.Count, message);
}

static async Task<int> GetNextLeadNumberSeedAsync(AppDbContext db, Guid tenantId, CancellationToken cancellationToken)
{
    var latestLeadNumber = await db.Leads
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.LeadNumber.StartsWith("LD-"))
        .OrderByDescending(item => item.CreatedAt)
        .ThenByDescending(item => item.LeadNumber)
        .Select(item => item.LeadNumber)
        .FirstOrDefaultAsync(cancellationToken);

    if (!string.IsNullOrWhiteSpace(latestLeadNumber) &&
        int.TryParse(latestLeadNumber.Replace("LD-", "", StringComparison.OrdinalIgnoreCase), out var latestNumber))
    {
        return latestNumber + 1;
    }

    return 1001;
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

static bool CanManageTenantProfile(AuthenticatedUser? user)
{
    return user?.Role is nameof(UserRole.Owner) or nameof(UserRole.Admin);
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

    db.CommunicationTemplates.AddRange(CreateDefaultCommunicationTemplates(tenantId, createdAt));
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

static IReadOnlyCollection<CommunicationTemplate> CreateDefaultCommunicationTemplates(Guid tenantId, DateTimeOffset timestamp) =>
[
    CreateCommunicationTemplate(tenantId, "Initial inquiry WhatsApp", "WhatsApp", "Initial Follow-up", "Hi {{studentName}}, thank you for your interest in {{course}} at {{tenantName}}. Our counsellor will help you with the next steps. Reply here or call us for any questions.", timestamp),
    CreateCommunicationTemplate(tenantId, "Demo reminder", "WhatsApp", "Demo Reminder", "Hi {{studentName}}, this is a reminder for your {{course}} demo. Please keep your questions ready. Your counsellor: {{counsellor}}.", timestamp),
    CreateCommunicationTemplate(tenantId, "Application follow-up email", "Email", "Application Follow-up", "Dear {{studentName}}, we are following up on your {{course}} admission application. Current stage: {{stage}}. Please share any pending details so we can proceed.", timestamp),
    CreateCommunicationTemplate(tenantId, "Document reminder", "WhatsApp", "Document Reminder", "Hi {{studentName}}, please share the pending documents for your {{course}} admission process. Lead ID: {{leadNumber}}.", timestamp)
];

static CommunicationTemplate CreateCommunicationTemplate(Guid tenantId, string name, string channel, string category, string body, DateTimeOffset timestamp) => new()
{
    Id = Guid.NewGuid(),
    TenantId = tenantId,
    Name = name,
    NormalizedName = NormalizeMasterName(name),
    Channel = NormalizeTemplateChannel(channel),
    Category = NormalizeName(category),
    Body = NormalizeTemplateBody(body),
    IsActive = true,
    Version = 1,
    CreatedAt = timestamp,
    UpdatedAt = timestamp
};

static CommunicationTemplateResponse ToCommunicationTemplateResponse(CommunicationTemplate template) => new(
    template.Id,
    template.Name,
    template.Channel,
    template.Category,
    template.Body,
    template.IsActive,
    template.Version,
    template.CreatedAt,
    template.UpdatedAt);

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
                    followUp.CompletionOutcome,
                    followUp.CompletionNotes,
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
                .ToArray(),
            db.LeadIntelligenceEvents
                .Where(@event => @event.TenantId == item.TenantId && @event.LeadId == item.Id)
                .OrderByDescending(@event => @event.CreatedAt)
                .Take(20)
                .Select(@event => new LeadIntelligenceEventResponse(
                    @event.Id.ToString(),
                    @event.EventType,
                    @event.PreviousScore,
                    @event.NewScore,
                    @event.PreviousTemperature,
                    @event.NewTemperature,
                    @event.PreviousAssignedUser == null ? null : @event.PreviousAssignedUser.FullName,
                    @event.NewAssignedUser == null ? null : @event.NewAssignedUser.FullName,
                    @event.DistributionRule == null ? null : @event.DistributionRule.Name,
                    @event.Reason,
                    @event.CreatedAt
                ))
                .ToArray()
        ))
        .FirstAsync(cancellationToken);
}

static async Task<ApplicationDetailResponse> GetApplicationResponseAsync(AppDbContext db, Guid tenantId, string applicationNumber, CancellationToken cancellationToken)
{
    var application = await db.AdmissionApplications
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.ApplicationNumber == applicationNumber)
        .Select(item => new ApplicationDetailResponse(
            item.ApplicationNumber,
            item.Lead.LeadNumber,
            item.Lead.StudentName,
            item.Lead.ArchivedAt,
            item.CourseId,
            item.Course.Name,
            item.BranchId,
            item.Branch == null ? null : item.Branch.Name,
            item.Intake,
            item.Status,
            item.InternalNotes,
            item.DecisionReason,
            item.AssignedReviewerUserId,
            item.AssignedReviewerUser == null ? null : item.AssignedReviewerUser.FullName,
            item.Version,
            item.CreatedAt,
            item.UpdatedAt,
            item.SubmittedAt,
            item.ReviewedAt,
            item.ApprovedAt,
            item.RejectedAt,
            item.ChecklistItems
                .OrderBy(check => check.SortOrder)
                .Select(check => new ApplicationChecklistItemResponse(
                    check.Id,
                    check.Name,
                    check.Category,
                    check.IsRequired,
                    check.IsCompleted,
                    check.IsWaived,
                    check.Notes,
                    check.Version,
                    check.CompletedAt,
                    check.CompletedByUser == null ? null : check.CompletedByUser.FullName))
                .ToArray(),
            item.StatusHistory
                .OrderByDescending(history => history.ChangedAt)
                .Select(history => new ApplicationStatusHistoryResponse(
                    history.PreviousStatus,
                    history.NewStatus,
                    history.Note,
                    history.ChangedAt,
                    history.ChangedByUser == null ? "System" : history.ChangedByUser.FullName))
                .ToArray(),
            Array.Empty<LeadDocumentChecklistItemResponse>(),
            Array.Empty<LeadPaymentResponse>(),
            new ApplicationPaymentSummaryResponse(0m, 0m, 0m, "INR", true, false),
            item.Enrollment == null ? null : new EnrollmentResponse(
                item.Enrollment.EnrollmentNumber,
                item.Enrollment.Status,
                item.Enrollment.Intake,
                item.Enrollment.EnrolledAt,
                item.Enrollment.Version)))
        .FirstAsync(cancellationToken);

    var leadInternalId = await db.AdmissionApplications
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.ApplicationNumber == applicationNumber)
        .Select(item => item.LeadId)
        .FirstAsync(cancellationToken);
    var documentChecklist = await GetLeadDocumentsResponseAsync(db, tenantId, leadInternalId, cancellationToken);
    var paymentLedger = await GetLeadPaymentsResponseAsync(db, tenantId, leadInternalId, cancellationToken);
    var activePayments = paymentLedger.Items.Where(item => item.Status != "Cancelled").ToArray();
    var totalDue = activePayments.Sum(item => item.AmountDue);
    var totalPaid = activePayments.Sum(item => item.AmountPaid);
    var unpaidBalance = activePayments.Sum(item => item.Balance);
    var currency = activePayments.Select(item => item.Currency).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? "INR";
    var receiptDocumentReady = documentChecklist.Items.Any(item =>
        item.Name.Contains("Payment Receipt", StringComparison.OrdinalIgnoreCase) &&
        item.DocumentId is not null &&
        item.Status == "Verified");
    var requiredDocumentTypes = await db.DocumentTypes.CountAsync(item => item.TenantId == tenantId && item.IsRequired && item.IsActive, cancellationToken);
    var verifiedRequiredDocuments = await db.LeadDocuments.CountAsync(item =>
        item.TenantId == tenantId &&
        item.LeadId == leadInternalId &&
        item.DocumentType.IsRequired &&
        item.DocumentType.IsActive &&
        item.Status == "Verified",
        cancellationToken);
    return application with
    {
        Documents = documentChecklist.Items,
        Payments = paymentLedger.Items,
        PaymentSummary = new ApplicationPaymentSummaryResponse(
            totalDue,
            totalPaid,
            unpaidBalance,
            currency,
            unpaidBalance <= 0,
            receiptDocumentReady),
        Readiness = new ApplicationReadinessResponse(
            application.ArchivedAt is null,
            requiredDocumentTypes == 0 || verifiedRequiredDocuments >= requiredDocumentTypes,
            unpaidBalance <= 0,
            application.Checklist.Count(check => check.IsRequired && !check.IsCompleted && !check.IsWaived),
            requiredDocumentTypes,
            verifiedRequiredDocuments,
            unpaidBalance)
    };
}

static async Task<EnrollmentDetailResponse> GetEnrollmentDetailResponseAsync(AppDbContext db, Guid tenantId, string enrollmentNumber, CancellationToken cancellationToken)
{
    var enrollment = await db.Enrollments
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.EnrollmentNumber == enrollmentNumber)
        .Select(item => new
        {
            item.EnrollmentNumber,
            item.StudentName,
            item.Intake,
            item.Status,
            item.Version,
            item.EnrolledAt,
            item.CreatedAt,
            item.UpdatedAt,
            LeadInternalId = item.LeadId,
            LeadId = item.Lead.LeadNumber,
            item.Lead.GuardianName,
            item.Lead.Phone,
            item.Lead.Email,
            item.Lead.City,
            item.Lead.ArchivedAt,
            ApplicationId = item.Application.ApplicationNumber,
            ApplicationStatus = item.Application.Status,
            Course = item.Course.Name,
            Branch = item.Branch == null ? null : item.Branch.Name,
            CreatedBy = item.CreatedByUser == null ? "System" : item.CreatedByUser.FullName,
            UpdatedBy = item.UpdatedByUser == null ? "System" : item.UpdatedByUser.FullName,
            ChecklistTotal = item.Application.ChecklistItems.Count,
            ChecklistDone = item.Application.ChecklistItems.Count(check => check.IsCompleted || check.IsWaived)
        })
        .FirstAsync(cancellationToken);

    var documents = await GetLeadDocumentsResponseAsync(db, tenantId, enrollment.LeadInternalId, cancellationToken);
    var payments = await GetLeadPaymentsResponseAsync(db, tenantId, enrollment.LeadInternalId, cancellationToken);
    var activePayments = payments.Items.Where(item => item.Status != "Cancelled").ToArray();
    var requiredDocuments = documents.Items.Count(item => item.IsRequired && item.IsActive);
    var verifiedRequiredDocuments = documents.Items.Count(item => item.IsRequired && item.IsActive && item.Status == "Verified");
    var totalDue = activePayments.Sum(item => item.AmountDue);
    var totalPaid = activePayments.Sum(item => item.AmountPaid);
    var balance = activePayments.Sum(item => item.Balance);
    var currency = activePayments.Select(item => item.Currency).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? "INR";
    var recentActivities = await db.Activities
        .AsNoTracking()
        .Include(item => item.CreatedByUser)
        .Where(item => item.TenantId == tenantId && item.LeadId == enrollment.LeadInternalId)
        .OrderByDescending(item => item.CreatedAt)
        .Take(12)
        .Select(item => new ActivityResponse(
            item.Id.ToString(),
            item.Type,
            item.Description,
            item.CreatedByUser == null ? "System" : item.CreatedByUser.FullName,
            item.CreatedAt))
        .ToArrayAsync(cancellationToken);

    return new EnrollmentDetailResponse(
        enrollment.EnrollmentNumber,
        enrollment.StudentName,
        enrollment.GuardianName,
        enrollment.Phone,
        enrollment.Email,
        enrollment.City,
        enrollment.ArchivedAt,
        enrollment.LeadId,
        enrollment.ApplicationId,
        enrollment.Course,
        enrollment.Branch,
        enrollment.Intake,
        enrollment.Status,
        enrollment.ApplicationStatus,
        enrollment.ChecklistDone,
        enrollment.ChecklistTotal,
        requiredDocuments,
        verifiedRequiredDocuments,
        totalDue,
        totalPaid,
        balance,
        currency,
        enrollment.EnrolledAt,
        enrollment.CreatedAt,
        enrollment.UpdatedAt,
        enrollment.CreatedBy,
        enrollment.UpdatedBy,
        enrollment.Version,
        recentActivities);
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

static Dictionary<string, string[]> ValidateCommunicationTemplateRequest(SaveCommunicationTemplateRequest request, bool requireVersion)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "name", request.Name, 160);
    AddRequiredError(errors, "channel", request.Channel, 40);
    AddRequiredError(errors, "category", request.Category, 80);
    AddRequiredError(errors, "body", request.Body, 2000);
    if (requireVersion)
    {
        AddVersionError(errors, request.Version);
    }

    if (!errors.ContainsKey("channel") && !AllowedTemplateChannels().Contains(NormalizeTemplateChannel(request.Channel)))
    {
        errors["channel"] = [$"Channel must be one of: {string.Join(", ", AllowedTemplateChannels())}."];
    }

    if (!errors.ContainsKey("body"))
    {
        var unknownPlaceholders = ExtractTemplatePlaceholders(request.Body)
            .Where(item => !AllowedTemplatePlaceholders().Contains(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unknownPlaceholders.Length > 0)
        {
            errors["body"] = [$"Unsupported placeholder(s): {string.Join(", ", unknownPlaceholders)}."];
        }
    }

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

static Dictionary<string, string[]> ValidateTenantProfileRequest(UpdateTenantProfileRequest request)
{
    var errors = new Dictionary<string, string[]>();
    AddRequiredError(errors, "name", request.Name, 160);
    AddOptionalLengthError(errors, "contactEmail", request.ContactEmail, 240);
    AddOptionalLengthError(errors, "contactPhone", request.ContactPhone, 40);
    AddOptionalLengthError(errors, "websiteUrl", request.WebsiteUrl, 500);
    AddOptionalLengthError(errors, "addressLine1", request.AddressLine1, 200);
    AddOptionalLengthError(errors, "addressLine2", request.AddressLine2, 200);
    AddOptionalLengthError(errors, "city", request.City, 120);
    AddOptionalLengthError(errors, "state", request.State, 120);
    AddOptionalLengthError(errors, "postalCode", request.PostalCode, 20);
    AddRequiredError(errors, "country", request.Country, 80);
    AddRequiredError(errors, "timeZone", request.TimeZone, 100);
    AddOptionalLengthError(errors, "logoUrl", request.LogoUrl, 500);
    AddRequiredError(errors, "brandColor", request.BrandColor, 7);
    AddVersionError(errors, request.Version);

    if (!string.IsNullOrWhiteSpace(request.ContactEmail) &&
        !errors.ContainsKey("contactEmail") &&
        !IsValidEmail(request.ContactEmail))
    {
        errors["contactEmail"] = ["Enter a valid contact email address."];
    }

    if (!string.IsNullOrWhiteSpace(request.ContactPhone) && !errors.ContainsKey("contactPhone"))
    {
        var phone = request.ContactPhone.Trim();
        var digits = NormalizePhone(phone);
        if (!Regex.IsMatch(phone, @"^[0-9+()\-\s.]+$") || digits.Length is < 7 or > 15)
        {
            errors["contactPhone"] = ["Enter a valid phone number containing 7 to 15 digits."];
        }
    }

    ValidateOptionalHttpUrl(errors, "websiteUrl", request.WebsiteUrl, "website URL");
    ValidateOptionalHttpUrl(errors, "logoUrl", request.LogoUrl, "logo URL");

    if (!errors.ContainsKey("postalCode") &&
        !string.IsNullOrWhiteSpace(request.PostalCode) &&
        !Regex.IsMatch(request.PostalCode.Trim(), @"^[A-Za-z0-9][A-Za-z0-9\- ]{1,18}[A-Za-z0-9]$"))
    {
        errors["postalCode"] = ["Enter a valid postal code using letters, numbers, spaces, or hyphens."];
    }

    if (!errors.ContainsKey("timeZone") && !SupportedTimeZones().Contains(request.TimeZone.Trim(), StringComparer.Ordinal))
    {
        errors["timeZone"] = ["Select a supported timezone."];
    }

    if (!errors.ContainsKey("brandColor") && !Regex.IsMatch(request.BrandColor.Trim(), @"^#[0-9A-Fa-f]{6}$"))
    {
        errors["brandColor"] = ["Enter a six-digit hex color such as #2171D3."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateLeadIntelligenceSettingsRequest(UpdateLeadIntelligenceSettingsRequest request)
{
    var errors = new Dictionary<string, string[]>();
    var strategy = NormalizeDistributionStrategy(request.DefaultDistributionStrategy);
    if (!IsSupportedDistributionStrategy(strategy) && strategy != "Disabled")
    {
        errors["defaultDistributionStrategy"] = ["Choose RoundRobin, Workload, DefaultAssignee, or Disabled."];
    }
    if (request.PriorityWeight < 0 || request.SourceWeight < 0 || request.ResponseWeight < 0 || request.EngagementWeight < 0 || request.FreshnessWeight < 0 || request.ProfileWeight < 0)
    {
        errors["weights"] = ["Scoring weights cannot be negative."];
    }
    if (request.PriorityWeight + request.SourceWeight + request.ResponseWeight + request.EngagementWeight + request.FreshnessWeight + request.ProfileWeight <= 0)
    {
        errors["weights"] = ["At least one scoring weight must be greater than zero."];
    }
    if (request.WarmThreshold < 0 || request.HotThreshold > 100 || request.WarmThreshold >= request.HotThreshold)
    {
        errors["thresholds"] = ["Warm threshold must be lower than hot threshold, and both must be between 0 and 100."];
    }
    if (request.MaxActiveLeadsPerUser is < 1 or > 10000)
    {
        errors["maxActiveLeadsPerUser"] = ["Max active leads per user must be between 1 and 10000."];
    }
    if (request.Version < 1)
    {
        errors["version"] = ["Version is required."];
    }
    return errors;
}

static async Task<Dictionary<string, string[]>> ValidateLeadDistributionRuleRequestAsync(
    AppDbContext db,
    Guid tenantId,
    SaveLeadDistributionRuleRequest request,
    bool requireVersion,
    CancellationToken cancellationToken)
{
    var errors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160)
    {
        errors["name"] = ["Rule name is required and must be 160 characters or fewer."];
    }
    var strategy = NormalizeDistributionStrategy(request.Strategy);
    if (!IsSupportedDistributionStrategy(strategy))
    {
        errors["strategy"] = ["Choose RoundRobin, Workload, or DefaultAssignee."];
    }
    if (request.PriorityOrder is < 1 or > 10000)
    {
        errors["priorityOrder"] = ["Priority order must be between 1 and 10000."];
    }
    if (requireVersion && request.Version < 1)
    {
        errors["version"] = ["Version is required."];
    }
    if (request.TargetUserIds.Count > 50)
    {
        errors["targetUserIds"] = ["A rule can target at most 50 users."];
    }

    if (request.BranchId is not null && !await db.Branches.AnyAsync(item => item.TenantId == tenantId && item.Id == request.BranchId && item.IsActive, cancellationToken))
    {
        errors["branchId"] = ["Select a valid active branch."];
    }
    if (request.CourseId is not null && !await db.Courses.AnyAsync(item => item.TenantId == tenantId && item.Id == request.CourseId && item.IsActive, cancellationToken))
    {
        errors["courseId"] = ["Select a valid active course."];
    }
    if (request.LeadSourceId is not null && !await db.LeadSources.AnyAsync(item => item.TenantId == tenantId && item.Id == request.LeadSourceId && item.IsActive, cancellationToken))
    {
        errors["leadSourceId"] = ["Select a valid active lead source."];
    }
    if (request.TargetUserIds.Count > 0)
    {
        var validUsers = await db.Users
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && request.TargetUserIds.Contains(item.Id) && item.IsActive && item.Role != UserRole.Accountant && item.Role != UserRole.ReadOnly)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (validUsers.Count != request.TargetUserIds.Distinct().Count())
        {
            errors["targetUserIds"] = ["All target users must be active CRM working users from this institute."];
        }
    }
    return errors;
}

static string NormalizeDistributionStrategy(string? strategy)
{
    return strategy?.Trim().ToLowerInvariant() switch
    {
        "workload" => "Workload",
        "defaultassignee" or "default assignee" => "DefaultAssignee",
        "disabled" => "Disabled",
        _ => "RoundRobin"
    };
}

static bool IsSupportedDistributionStrategy(string strategy)
{
    return strategy is "RoundRobin" or "Workload" or "DefaultAssignee";
}

static void ValidateOptionalHttpUrl(Dictionary<string, string[]> errors, string key, string? value, string label)
{
    if (string.IsNullOrWhiteSpace(value) || errors.ContainsKey(key))
    {
        return;
    }

    if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
        string.IsNullOrWhiteSpace(uri.Host))
    {
        errors[key] = [$"Enter a valid HTTP or HTTPS {label}."];
    }
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

static Dictionary<string, string[]> ValidateCompleteFollowUpRequest(CompleteFollowUpRequest request)
{
    var errors = new Dictionary<string, string[]>();
    if (request.Version < 1)
    {
        errors["version"] = ["Refresh the follow-up before completing it."];
    }

    var outcome = NormalizeOptionalText(request.Outcome);
    var allowedOutcomes = new[] { "Connected", "No Answer", "Interested", "Not Interested" };
    if (outcome is not null && !allowedOutcomes.Contains(outcome, StringComparer.OrdinalIgnoreCase))
    {
        errors["outcome"] = ["Select a valid completion outcome."];
    }

    AddOptionalLengthError(errors, "notes", request.Notes, 1000);
    if (request.NextFollowUp is not null)
    {
        AddOptionalLengthError(errors, "nextFollowUp.type", request.NextFollowUp.Type, 40);
        AddOptionalLengthError(errors, "nextFollowUp.priority", request.NextFollowUp.Priority, 40);
        if (request.NextFollowUp.DueAt == default)
        {
            errors["nextFollowUp.dueAt"] = ["Next follow-up date is required."];
        }
        else if (IndianClock.ToIndianTime(request.NextFollowUp.DueAt) <= IndianClock.Now())
        {
            errors["nextFollowUp.dueAt"] = ["Next follow-up must be scheduled in the future."];
        }
    }

    return errors;
}

static string NormalizeCompletionOutcome(string? value)
{
    return NormalizeOptionalText(value)?.ToLowerInvariant() switch
    {
        "connected" => "Connected",
        "no answer" => "No Answer",
        "interested" => "Interested",
        "not interested" => "Not Interested",
        _ => "Connected"
    };
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

static string NormalizeTemplateBody(string? value)
{
    var normalized = (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    return Regex.Replace(normalized, @"[ \t]+", " ");
}

static string NormalizeTemplateChannel(string? value)
{
    var normalized = NormalizeOptionalText(value) ?? "Note";
    return AllowedTemplateChannels().FirstOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)) ?? normalized;
}

static IReadOnlyCollection<string> AllowedTemplateChannels() => ["Call", "WhatsApp", "Email", "SMS", "Meeting", "Note"];

static IReadOnlyCollection<string> SupportedTimeZones() =>
[
    "Asia/Kolkata",
    "Asia/Dubai",
    "Asia/Singapore",
    "Asia/Tokyo",
    "Asia/Kathmandu",
    "Asia/Dhaka",
    "Asia/Colombo",
    "Europe/London",
    "Europe/Berlin",
    "America/New_York",
    "America/Chicago",
    "America/Denver",
    "America/Los_Angeles",
    "Australia/Sydney",
    "UTC"
];

static TenantProfileResponse ToTenantProfileResponse(Tenant tenant)
{
    return new TenantProfileResponse(
        tenant.Id,
        tenant.Name,
        tenant.Slug,
        tenant.ContactEmail,
        tenant.ContactPhone,
        tenant.WebsiteUrl,
        tenant.AddressLine1,
        tenant.AddressLine2,
        tenant.City,
        tenant.State,
        tenant.PostalCode,
        tenant.Country,
        tenant.TimeZone,
        tenant.LogoUrl,
        tenant.BrandColor,
        tenant.DefaultBranchId,
        tenant.DefaultAssigneeUserId,
        tenant.IsActive,
        tenant.Version,
        tenant.CreatedAt,
        tenant.UpdatedAt ?? tenant.CreatedAt);
}

static async Task<LeadIntelligenceSettingsResponse> ToLeadIntelligenceSettingsResponseAsync(
    AppDbContext db,
    LeadIntelligenceSettings settings,
    CancellationToken cancellationToken)
{
    var rules = await db.LeadDistributionRules
        .AsNoTracking()
        .Where(item => item.TenantId == settings.TenantId)
        .OrderBy(item => item.PriorityOrder)
        .ThenBy(item => item.Name)
        .ToListAsync(cancellationToken);
    var ruleResponses = rules.Select(ToLeadDistributionRuleResponse).ToArray();

    return new LeadIntelligenceSettingsResponse(
        settings.ScoringEnabled,
        settings.DistributionEnabled,
        settings.DefaultDistributionStrategy,
        settings.PriorityWeight,
        settings.SourceWeight,
        settings.ResponseWeight,
        settings.EngagementWeight,
        settings.FreshnessWeight,
        settings.ProfileWeight,
        settings.HotThreshold,
        settings.WarmThreshold,
        settings.MaxActiveLeadsPerUser,
        settings.Version,
        ruleResponses);
}

static LeadDistributionRuleResponse ToLeadDistributionRuleResponse(LeadDistributionRule rule)
{
    return new LeadDistributionRuleResponse(
        rule.Id,
        rule.Name,
        rule.IsActive,
        rule.PriorityOrder,
        rule.Strategy,
        rule.BranchId,
        rule.CourseId,
        rule.LeadSourceId,
        ParseDistributionTargetUsers(rule.TargetUserIds),
        rule.Version,
        rule.CreatedAt,
        rule.UpdatedAt);
}

static IReadOnlyCollection<string> AllowedTemplatePlaceholders() =>
[
    "studentName",
    "leadNumber",
    "course",
    "stage",
    "status",
    "priority",
    "source",
    "counsellor",
    "phone",
    "email",
    "city",
    "tenantName",
    "nextFollowUp"
];

static IEnumerable<string> ExtractTemplatePlaceholders(string? body)
{
    if (string.IsNullOrWhiteSpace(body))
    {
        return [];
    }

    return Regex.Matches(body, @"{{\s*([^}]+?)\s*}}")
        .Select(match => match.Groups[1].Value.Trim());
}

static string RenderCommunicationTemplate(string body, TenantScope tenant, Lead lead)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["studentName"] = lead.StudentName,
        ["leadNumber"] = lead.LeadNumber,
        ["course"] = lead.Course.Name,
        ["stage"] = lead.LeadStage.Name,
        ["status"] = lead.Status,
        ["priority"] = lead.Priority,
        ["source"] = lead.LeadSource.Name,
        ["counsellor"] = lead.AssignedUser?.FullName ?? "your counsellor",
        ["phone"] = lead.Phone,
        ["email"] = lead.Email,
        ["city"] = lead.City ?? "your city",
        ["tenantName"] = tenant.Name,
        ["nextFollowUp"] = lead.NextFollowUpAt is null ? "not scheduled" : lead.NextFollowUpAt.Value.ToString("dd MMM yyyy, hh:mm tt")
    };

    return Regex.Replace(body, @"{{\s*([A-Za-z][A-Za-z0-9]*)\s*}}", match =>
    {
        var key = match.Groups[1].Value;
        return values.TryGetValue(key, out var value) ? value : match.Value;
    });
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
    return type is "Note" or "Call" or "WhatsApp" or "Email" or "SMS" or "Meeting" ? type : "Note";
}

static string NormalizeApplicationStatus(string? value)
{
    var status = NormalizeOptionalText(value)?.Replace(" ", "", StringComparison.OrdinalIgnoreCase) ?? string.Empty;
    return status switch
    {
        "Draft" => "Draft",
        "Submitted" => "Submitted",
        "UnderReview" => "UnderReview",
        "ChangesRequired" => "ChangesRequired",
        "Approved" => "Approved",
        "Rejected" => "Rejected",
        "Withdrawn" => "Withdrawn",
        "Cancelled" => "Cancelled",
        "Enrolled" => "Enrolled",
        _ => "Invalid"
    };
}

static string NormalizeEnrollmentStatus(string? value)
{
    var status = NormalizeOptionalText(value)?.Replace(" ", "", StringComparison.OrdinalIgnoreCase) ?? string.Empty;
    return status switch
    {
        "Active" => "Active",
        "Deferred" => "Deferred",
        "Cancelled" => "Cancelled",
        "Completed" => "Completed",
        _ => "Invalid"
    };
}

static bool CanReviewApplications(AuthenticatedUser? user)
{
    return user?.Role is nameof(UserRole.Owner) or nameof(UserRole.Admin) or nameof(UserRole.BranchManager);
}

static bool CanManageEnrollmentStatus(AuthenticatedUser? user)
{
    return user?.Role is nameof(UserRole.Owner) or nameof(UserRole.Admin) or nameof(UserRole.BranchManager);
}

static bool CanCounsellorTransition(AuthenticatedUser? user, string targetStatus)
{
    return user?.Role is nameof(UserRole.Counselor) or nameof(UserRole.Telecaller) &&
        targetStatus is "Submitted" or "Withdrawn";
}

static string? ValidateEnrollmentStatusTransition(string currentStatus, string targetStatus, AuthenticatedUser? currentUser)
{
    if (targetStatus == "Invalid") return "Select a valid enrollment status.";
    if (currentStatus == targetStatus) return "Enrollment is already in this status.";

    var isOwnerOrAdmin = currentUser?.Role is nameof(UserRole.Owner) or nameof(UserRole.Admin);
    var allowed = currentStatus switch
    {
        "Active" => new[] { "Deferred", "Cancelled", "Completed" },
        "Deferred" => new[] { "Active", "Cancelled" },
        "Completed" or "Cancelled" => isOwnerOrAdmin ? ["Active"] : Array.Empty<string>(),
        _ => Array.Empty<string>()
    };

    if (!allowed.Contains(targetStatus))
    {
        return currentStatus is "Completed" or "Cancelled"
            ? "Only owners and admins can reopen completed or cancelled enrollments."
            : $"Cannot move enrollment from {currentStatus} to {targetStatus}.";
    }

    return null;
}

static async Task<string?> ValidateApplicationTransitionAsync(
    AppDbContext db,
    AdmissionApplication application,
    AuthenticatedUser? currentUser,
    string targetStatus,
    CancellationToken cancellationToken)
{
    if (targetStatus == "Invalid") return "Select a valid application status.";
    if (application.Lead.ArchivedAt is not null) return "Restore this lead before changing the application.";
    if (!CanReviewApplications(currentUser) && !CanCounsellorTransition(currentUser, targetStatus))
    {
        return "You do not have permission to perform this application action.";
    }

    var allowed = application.Status switch
    {
        "Draft" => new[] { "Submitted", "Withdrawn" },
        "Submitted" => new[] { "UnderReview", "ChangesRequired", "Withdrawn" },
        "UnderReview" => new[] { "Approved", "ChangesRequired", "Rejected" },
        "ChangesRequired" => new[] { "Submitted", "Withdrawn" },
        "Approved" => new[] { "Cancelled" },
        "Rejected" or "Withdrawn" or "Cancelled" => CanReviewApplications(currentUser) ? ["Draft"] : Array.Empty<string>(),
        _ => Array.Empty<string>()
    };
    if (!allowed.Contains(targetStatus)) return $"Cannot move application from {application.Status} to {targetStatus}.";
    if (targetStatus == "Approved")
    {
        return await ValidateApplicationReadyForApprovalAsync(db, application, cancellationToken);
    }

    return null;
}

static async Task<string?> ValidateApplicationReadyForApprovalAsync(AppDbContext db, AdmissionApplication application, CancellationToken cancellationToken)
{
    if (application.Lead.ArchivedAt is not null) return "Restore this lead before approval.";
    if (!await db.Courses.AnyAsync(item => item.TenantId == application.TenantId && item.Id == application.CourseId && item.IsActive, cancellationToken))
        return "Application course is no longer active.";
    if (application.BranchId is not null &&
        !await db.Branches.AnyAsync(item => item.TenantId == application.TenantId && item.Id == application.BranchId && item.IsActive, cancellationToken))
        return "Application branch is no longer active.";
    if (application.ChecklistItems.Any(item => item.IsRequired && !item.IsCompleted && !item.IsWaived))
        return "Complete or waive all required admission checklist items before approval.";
    var requiredDocuments = await db.LeadDocuments.CountAsync(item =>
        item.TenantId == application.TenantId &&
        item.LeadId == application.LeadId &&
        item.DocumentType.IsRequired &&
        item.DocumentType.IsActive &&
        item.Status == "Verified",
        cancellationToken);
    var requiredDocumentTypes = await db.DocumentTypes.CountAsync(item => item.TenantId == application.TenantId && item.IsRequired && item.IsActive, cancellationToken);
    if (requiredDocuments < requiredDocumentTypes) return "Verify all required documents before approval.";
    var unpaidBalance = await db.LeadPayments
        .Where(item => item.TenantId == application.TenantId && item.LeadId == application.LeadId && item.CancelledAt == null)
        .Select(item => item.AmountDue - item.Transactions.Sum(txn => txn.Amount))
        .SumAsync(cancellationToken);
    if (unpaidBalance > 0) return $"Payment pending: {FormatMoney(unpaidBalance, "INR")} balance remains. Collect payment or cancel/adjust unpaid fee items before approval.";
    return null;
}

static void AddDefaultAdmissionChecklist(AppDbContext db, Guid tenantId, Guid applicationId, DateTimeOffset now)
{
    var defaults = new[]
    {
        ("Application form reviewed", "Application", true),
        ("Required documents verified", "Documents", true),
        ("Admission fee readiness checked", "Payments", true),
        ("Counsellor notes reviewed", "Review", false)
    };
    for (var index = 0; index < defaults.Length; index++)
    {
        var item = defaults[index];
        db.AdmissionChecklistItems.Add(new AdmissionChecklistItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            Name = item.Item1,
            Category = item.Item2,
            IsRequired = item.Item3,
            SortOrder = (index + 1) * 10,
            CreatedAt = now,
            UpdatedAt = now
        });
    }
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

static async Task<string> GenerateApplicationNumberAsync(AppDbContext db, Guid tenantId, CancellationToken cancellationToken)
{
    var latest = await db.AdmissionApplications
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.ApplicationNumber.StartsWith("APP-"))
        .OrderByDescending(item => item.CreatedAt)
        .ThenByDescending(item => item.ApplicationNumber)
        .Select(item => item.ApplicationNumber)
        .FirstOrDefaultAsync(cancellationToken);
    var next = 1001;
    if (!string.IsNullOrWhiteSpace(latest) && int.TryParse(latest.Replace("APP-", "", StringComparison.OrdinalIgnoreCase), out var latestNumber))
    {
        next = latestNumber + 1;
    }
    return $"APP-{next}";
}

static async Task<string> GenerateEnrollmentNumberAsync(AppDbContext db, Guid tenantId, CancellationToken cancellationToken)
{
    var latest = await db.Enrollments
        .AsNoTracking()
        .Where(item => item.TenantId == tenantId && item.EnrollmentNumber.StartsWith("ENR-"))
        .OrderByDescending(item => item.CreatedAt)
        .ThenByDescending(item => item.EnrollmentNumber)
        .Select(item => item.EnrollmentNumber)
        .FirstOrDefaultAsync(cancellationToken);
    var next = 1001;
    if (!string.IsNullOrWhiteSpace(latest) && int.TryParse(latest.Replace("ENR-", "", StringComparison.OrdinalIgnoreCase), out var latestNumber))
    {
        next = latestNumber + 1;
    }
    return $"ENR-{next}";
}

record DashboardSummary(
    int TotalLeads,
    int NewLeadsToday,
    int Contacted,
    int Enrolled,
    int PendingFollowUps,
    decimal ConversionRate);

record AdvancedDashboardResponse(
    DateTimeOffset GeneratedAt,
    string StartDate,
    string EndDate,
    ReportAccessResponse Access,
    AdvancedDashboardSummaryResponse Summary,
    IReadOnlyCollection<AdvancedDashboardTrendPoint> RevenueTrend,
    IReadOnlyCollection<AdvancedDashboardFunnelStep> Funnel,
    IReadOnlyCollection<AdvancedDashboardPerformanceRow> Courses,
    IReadOnlyCollection<AdvancedDashboardPerformanceRow> Branches,
    IReadOnlyCollection<AdvancedDashboardPerformanceRow> Counselors,
    IReadOnlyCollection<AdvancedDashboardAlert> Alerts);

record AdvancedDashboardSummaryResponse(
    int TotalLeads,
    int Applications,
    int ApprovedApplications,
    int Enrollments,
    decimal ExpectedRevenue,
    decimal CollectedRevenue,
    decimal PendingBalance,
    decimal CollectionRate,
    decimal EnrollmentRate,
    int OverdueFollowUps);

record AdvancedDashboardTrendPoint(
    string Label,
    string Date,
    decimal ExpectedRevenue,
    decimal CollectedRevenue,
    decimal PendingBalance,
    int Leads,
    int Applications,
    int Enrollments);

record AdvancedDashboardFunnelStep(string Key, string Label, int Count, decimal Percentage);

record AdvancedDashboardPerformanceRow(
    Guid? Id,
    string Name,
    int Leads,
    int Applications,
    int Enrollments,
    decimal ExpectedRevenue,
    decimal CollectedRevenue,
    decimal PendingBalance,
    decimal EnrollmentRate,
    decimal CollectionRate);

record AdvancedDashboardAlert(string Key, string Title, int Count, string Severity, string Description);

record AdvancedLeadAnalyticsRow(
    Guid Id,
    Guid? BranchId,
    string Branch,
    Guid CourseId,
    string Course,
    Guid? AssignedUserId,
    string Counselor,
    DateTimeOffset CreatedAt,
    bool IsWonStage,
    bool IsLostStage);

record AdvancedActivityAnalyticsRow(
    Guid LeadId,
    Guid? BranchId,
    string Branch,
    Guid CourseId,
    string Course,
    Guid? AssignedUserId,
    string Counselor,
    DateTimeOffset OccurredAt,
    string Status);

record AdvancedPaymentItemAnalyticsRow(
    Guid PaymentId,
    Guid LeadId,
    Guid? BranchId,
    string Branch,
    Guid CourseId,
    string Course,
    Guid? AssignedUserId,
    string Counselor,
    DateTimeOffset CreatedAt,
    decimal AmountDue,
    decimal PaidAllTime);

record AdvancedPaymentTransactionAnalyticsRow(
    Guid LeadId,
    Guid? BranchId,
    string Branch,
    Guid CourseId,
    string Course,
    Guid? AssignedUserId,
    string Counselor,
    DateTimeOffset PaidAt,
    decimal Amount);

sealed class AdvancedDashboardPerformanceAccumulator(Guid? id, string name)
{
    public Guid? Id { get; } = id;
    public string Name { get; } = name;
    public int Leads { get; set; }
    public int Applications { get; set; }
    public int Enrollments { get; set; }
    public decimal ExpectedRevenue { get; set; }
    public decimal CollectedRevenue { get; set; }
    public decimal PendingBalance { get; set; }
}

record ReportDateRange(DateOnly StartDate, DateOnly EndDate, DateTimeOffset Start, DateTimeOffset EndExclusive);
record ReportDateRangeResult(ReportDateRange? Range, Dictionary<string, string[]> Errors);

record CounsellorAttentionLead(
    string Id,
    string StudentName,
    string Course,
    string Stage,
    string Priority,
    DateTimeOffset? NextFollowUpAt,
    DateTimeOffset LastActivityAt);
record CounsellorAttentionGroup(string Key, string Title, string Guidance, int Count, IReadOnlyCollection<CounsellorAttentionLead> Items);
record CounsellorPipelineInsight(Guid StageId, string Stage, int SortOrder, int TotalLeads, int StuckLeads, bool IsWonStage, bool IsLostStage);
record CounsellorFollowUpInsight(int Scheduled, int Completed, int CompletedOnTime, int CompletedLate, int Cancelled, int CurrentlyOverdue, decimal CompletionRate);
record CounsellorOutcomeInsight(int NewLeads, int WonLeads, int LostLeads, int OpenLeads, decimal ConversionRate);
record CounsellorBreakdownInsight(Guid Id, string Name, int TotalLeads, int WonLeads, int OpenLeads);
record CounsellorWorkspaceResponse(
    string StartDate,
    string EndDate,
    DateTimeOffset GeneratedAt,
    IReadOnlyCollection<CounsellorAttentionGroup> Attention,
    IReadOnlyCollection<CounsellorPipelineInsight> Pipeline,
    CounsellorFollowUpInsight FollowUps,
    CounsellorOutcomeInsight Outcomes,
    IReadOnlyCollection<CounsellorBreakdownInsight> Courses,
    IReadOnlyCollection<CounsellorBreakdownInsight> Sources);

record ReportsResponse(
    DateTimeOffset GeneratedAt,
    string StartDate,
    string EndDate,
    ReportAccessResponse Access,
    ReportSummaryResponse Summary,
    IReadOnlyCollection<SourceReportRow> Sources,
    IReadOnlyCollection<CounselorReportRow> Counselors,
    IReadOnlyCollection<StageReportRow> Stages);

record ReportAccessResponse(string Scope, string Role);

record ReportSummaryResponse(
    int TotalLeads,
    int ContactedLeads,
    int WonLeads,
    int LostLeads,
    int OpenLeads,
    int ScheduledFollowUps,
    int CompletedFollowUps,
    int OverdueFollowUps,
    decimal ConversionRate);

record SourceReportRow(
    Guid SourceId,
    string Source,
    int TotalLeads,
    int WonLeads,
    int LostLeads,
    int OpenLeads,
    decimal ConversionRate);

record CounselorReportRow(
    Guid? UserId,
    string Counselor,
    int TotalLeads,
    int WonLeads,
    int LostLeads,
    int OpenLeads,
    int ScheduledFollowUps,
    int CompletedFollowUps,
    int OverdueFollowUps,
    decimal ConversionRate);

record StageReportRow(
    Guid StageId,
    string Stage,
    int SortOrder,
    int TotalLeads,
    decimal Percentage,
    bool IsWonStage,
    bool IsLostStage);

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

record TenantProfileResponse(
    Guid Id,
    string Name,
    string Slug,
    string? ContactEmail,
    string? ContactPhone,
    string? WebsiteUrl,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string Country,
    string TimeZone,
    string? LogoUrl,
    string BrandColor,
    Guid? DefaultBranchId,
    Guid? DefaultAssigneeUserId,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

record UpdateTenantProfileRequest(
    string Name,
    string? ContactEmail,
    string? ContactPhone,
    string? WebsiteUrl,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string Country,
    string TimeZone,
    string? LogoUrl,
    string BrandColor,
    Guid? DefaultBranchId,
    Guid? DefaultAssigneeUserId,
    int Version);

record LeadIntelligenceSettingsResponse(
    bool ScoringEnabled,
    bool DistributionEnabled,
    string DefaultDistributionStrategy,
    int PriorityWeight,
    int SourceWeight,
    int ResponseWeight,
    int EngagementWeight,
    int FreshnessWeight,
    int ProfileWeight,
    int HotThreshold,
    int WarmThreshold,
    int MaxActiveLeadsPerUser,
    int Version,
    IReadOnlyCollection<LeadDistributionRuleResponse> Rules);

record UpdateLeadIntelligenceSettingsRequest(
    bool ScoringEnabled,
    bool DistributionEnabled,
    string DefaultDistributionStrategy,
    int PriorityWeight,
    int SourceWeight,
    int ResponseWeight,
    int EngagementWeight,
    int FreshnessWeight,
    int ProfileWeight,
    int HotThreshold,
    int WarmThreshold,
    int MaxActiveLeadsPerUser,
    int Version);

record LeadDistributionRuleResponse(
    Guid Id,
    string Name,
    bool IsActive,
    int PriorityOrder,
    string Strategy,
    Guid? BranchId,
    Guid? CourseId,
    Guid? LeadSourceId,
    IReadOnlyCollection<Guid> TargetUserIds,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

record SaveLeadDistributionRuleRequest(
    string Name,
    bool IsActive,
    int PriorityOrder,
    string Strategy,
    Guid? BranchId,
    Guid? CourseId,
    Guid? LeadSourceId,
    IReadOnlyCollection<Guid> TargetUserIds,
    int Version);

record LeadIntelligenceRecalculateResponse(int Updated, string Message);

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

record CommunicationTemplateResponse(
    Guid Id,
    string Name,
    string Channel,
    string Category,
    string Body,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

record SaveCommunicationTemplateRequest(
    string Name,
    string Channel,
    string Category,
    string Body,
    bool IsActive,
    int Version);
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

record LeadDocumentUploadForm(
    Guid DocumentTypeId,
    int? Version,
    string? Notes,
    IFormFile? File,
    Dictionary<string, string[]> Errors);

record LeadDocumentsResponse(IReadOnlyCollection<LeadDocumentChecklistItemResponse> Items);

record LeadDocumentChecklistItemResponse(
    Guid DocumentTypeId,
    string Name,
    bool IsRequired,
    bool IsActive,
    int SortOrder,
    Guid? DocumentId,
    string Status,
    string? FileName,
    string? ContentType,
    long FileSizeBytes,
    string? Notes,
    int? Version,
    DateTimeOffset? UploadedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ReviewedAt,
    string? UploadedBy,
    string? ReviewedBy,
    bool CanDownload);

record LeadPaymentsResponse(IReadOnlyCollection<LeadPaymentResponse> Items);

record LeadPaymentResponse(
    Guid Id,
    string Title,
    decimal AmountDue,
    decimal AmountPaid,
    decimal Balance,
    string Currency,
    DateTimeOffset? DueDate,
    string Status,
    string? Notes,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CancelledAt,
    string CreatedBy,
    string UpdatedBy,
    IReadOnlyCollection<LeadPaymentTransactionResponse> Transactions);

record LeadPaymentTransactionResponse(
    Guid Id,
    decimal Amount,
    string Method,
    string? ReferenceNumber,
    string? ReceiptNumber,
    DateTimeOffset PaidAt,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy);

record LeadImportFormRequest(
    LeadImportSheet? Sheet,
    IReadOnlyDictionary<string, string>? Mapping,
    string DuplicateMode,
    string? Fingerprint,
    string? Error);

record ApplicationListResponse(IReadOnlyCollection<ApplicationListItemResponse> Items, int Page, int PageSize, int Total);
record ApplicationListItemResponse(
    string Id,
    string LeadId,
    string StudentName,
    string Course,
    string? Branch,
    string? Intake,
    string Status,
    int ChecklistTotal,
    int ChecklistDone,
    int Version,
    DateTimeOffset UpdatedAt);

record CreateAdmissionApplicationRequest(Guid? CourseId, Guid? BranchId, Guid? AssignedReviewerUserId, string? Intake, string? InternalNotes);
record TransitionApplicationRequest(string Status, string? Note, int Version);
record UpdateChecklistItemRequest(bool IsCompleted, bool IsWaived, string? Notes, int Version);
record EnrollApplicationRequest(string? Intake, string? Note, int Version);

record ApplicationDetailResponse(
    string Id,
    string LeadId,
    string StudentName,
    DateTimeOffset? ArchivedAt,
    Guid CourseId,
    string Course,
    Guid? BranchId,
    string? Branch,
    string? Intake,
    string Status,
    string? InternalNotes,
    string? DecisionReason,
    Guid? AssignedReviewerUserId,
    string? AssignedReviewer,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? RejectedAt,
    IReadOnlyCollection<ApplicationChecklistItemResponse> Checklist,
    IReadOnlyCollection<ApplicationStatusHistoryResponse> StatusHistory,
    IReadOnlyCollection<LeadDocumentChecklistItemResponse> Documents,
    IReadOnlyCollection<LeadPaymentResponse> Payments,
    ApplicationPaymentSummaryResponse PaymentSummary,
    EnrollmentResponse? Enrollment,
    ApplicationReadinessResponse? Readiness = null);
record ApplicationChecklistItemResponse(Guid Id, string Name, string Category, bool IsRequired, bool IsCompleted, bool IsWaived, string? Notes, int Version, DateTimeOffset? CompletedAt, string? CompletedBy);
record ApplicationStatusHistoryResponse(string? PreviousStatus, string NewStatus, string? Note, DateTimeOffset ChangedAt, string ChangedBy);
record EnrollmentResponse(string Id, string Status, string? Intake, DateTimeOffset EnrolledAt, int Version);
record ApplicationPaymentSummaryResponse(decimal TotalDue, decimal TotalPaid, decimal Balance, string Currency, bool PaymentsReady, bool ReceiptDocumentVerified);
record ApplicationReadinessResponse(bool LeadActive, bool DocumentsReady, bool PaymentsReady, int RequiredChecklistMissing, int RequiredDocuments, int VerifiedRequiredDocuments, decimal UnpaidBalance);

record EnrollmentListResponse(IReadOnlyCollection<EnrollmentListItemResponse> Items, int Page, int PageSize, int Total);
record EnrollmentListItemResponse(
    string Id,
    string StudentName,
    string LeadId,
    string ApplicationId,
    string Course,
    string? Branch,
    string? Intake,
    string Status,
    decimal FeeBalance,
    bool DocumentsReady,
    int Version,
    DateTimeOffset EnrolledAt,
    DateTimeOffset UpdatedAt);

record EnrollmentDetailResponse(
    string Id,
    string StudentName,
    string? GuardianName,
    string Phone,
    string Email,
    string? City,
    DateTimeOffset? ArchivedAt,
    string LeadId,
    string ApplicationId,
    string Course,
    string? Branch,
    string? Intake,
    string Status,
    string ApplicationStatus,
    int ChecklistDone,
    int ChecklistTotal,
    int RequiredDocuments,
    int VerifiedRequiredDocuments,
    decimal TotalDue,
    decimal TotalPaid,
    decimal Balance,
    string Currency,
    DateTimeOffset EnrolledAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CreatedBy,
    string UpdatedBy,
    int Version,
    IReadOnlyCollection<ActivityResponse> RecentActivities);
record UpdateEnrollmentStatusRequest(string Status, string? Note, int Version);

record LeadImportMappingResult(
    IReadOnlyDictionary<string, string>? Mapping,
    string? Error);

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
    DateTimeOffset? NextFollowUpAt,
    int IntelligenceScore,
    string IntelligenceTemperature,
    string? IntelligenceReason,
    DateTimeOffset? IntelligenceUpdatedAt);

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
    IReadOnlyCollection<ActivityResponse> Activities,
    IReadOnlyCollection<LeadIntelligenceEventResponse> IntelligenceEvents);

record LeadIntelligenceEventResponse(
    string Id,
    string EventType,
    int? PreviousScore,
    int? NewScore,
    string? PreviousTemperature,
    string? NewTemperature,
    string? PreviousAssignedUser,
    string? NewAssignedUser,
    string? Rule,
    string Reason,
    DateTimeOffset CreatedAt);

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
    string? CompletionOutcome,
    string? CompletionNotes,
    string AssignedTo);

record FollowUpAnalyticsRange(DateTimeOffset From, DateTimeOffset To);

record FollowUpTrendSource(string Status, DateTimeOffset DueAt, DateTimeOffset? CompletedAt);

record FollowUpAnalyticsResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset GeneratedAt,
    FollowUpAnalyticsSummary Summary,
    IReadOnlyCollection<FollowUpAnalyticsBreakdown> ByStatus,
    IReadOnlyCollection<FollowUpAnalyticsBreakdown> ByType,
    IReadOnlyCollection<FollowUpAnalyticsBreakdown> ByPriority,
    IReadOnlyCollection<FollowUpCounsellorAnalyticsRow> ByCounsellor,
    IReadOnlyCollection<FollowUpTrendPoint> Trend,
    IReadOnlyCollection<FollowUpOverdueRiskRow> OverdueRisk);

record FollowUpAnalyticsSummary(
    int Scheduled,
    int Completed,
    int Cancelled,
    int Open,
    int Overdue,
    int CompletedOnTime,
    int CompletedLate,
    int AverageDelayMinutes,
    decimal CompletionRate,
    decimal OnTimeRate,
    int ConvertedLeads);

record FollowUpAnalyticsBreakdown(string Label, int Count, decimal Percentage);

record FollowUpCounsellorAnalyticsRow(
    string Name,
    int Scheduled,
    int Completed,
    int Overdue,
    int CompletedOnTime,
    decimal CompletionRate,
    decimal OnTimeRate);

record FollowUpTrendPoint(string Date, int Scheduled, int Completed, int Overdue);

record FollowUpOverdueRiskRow(
    string Id,
    string LeadId,
    string StudentName,
    string Type,
    string Priority,
    string AssignedTo,
    DateTimeOffset DueAt,
    int HoursOverdue);

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
    IReadOnlyCollection<LookupOption> Counselors,
    Guid? DefaultBranchId,
    Guid? DefaultAssigneeUserId);

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
record BulkLeadActionItem(string LeadId, int Version);
record BulkLeadActionRequest(
    string Action,
    IReadOnlyCollection<BulkLeadActionItem> Items,
    Guid? AssignedUserId,
    Guid? LeadStageId);
record BulkLeadActionResponse(int Requested, int Updated, int Unchanged, string Message);

record AddLeadActivityRequest(
    string Description,
    string? Type);

record ApplyCommunicationTemplateRequest(
    Guid TemplateId,
    string? Note);

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
record CompleteFollowUpRequest(
    int Version,
    string? Outcome,
    string? Notes,
    NextFollowUpRequest? NextFollowUp);
record NextFollowUpRequest(
    string? Type,
    string? Priority,
    DateTimeOffset DueAt);
record LeadDocumentVersionRequest(int Version);
record ReviewLeadDocumentRequest(int Version, string? Notes);
record SaveLeadPaymentRequest(
    string Title,
    decimal AmountDue,
    string? Currency,
    DateTimeOffset? DueDate,
    string? Notes,
    int Version);
record CreateLeadPaymentTransactionRequest(
    decimal Amount,
    string? Method,
    string? ReferenceNumber,
    string? ReceiptNumber,
    DateTimeOffset? PaidAt,
    string? Notes,
    int Version);
record LeadPaymentVersionRequest(int Version);
record NotificationPreferenceRequest(
    bool FollowUpRemindersEnabled,
    bool PaymentRemindersEnabled,
    int Version);

record LeadScoreCalculation(int Score, string Temperature, string Reason);

record LeadDistributionDecision(Guid? AssignedUserId, Guid? RuleId, string Reason)
{
    public static LeadDistributionDecision Assigned(Guid assignedUserId, Guid? ruleId, string reason) => new(assignedUserId, ruleId, reason);
    public static LeadDistributionDecision None(string reason) => new(null, null, reason);
}
