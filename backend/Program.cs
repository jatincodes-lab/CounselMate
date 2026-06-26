using EducationCrm.Api.Data;
using EducationCrm.Api.Models;
using EducationCrm.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

var api = app.MapGroup("/api");

api.MapGet("/health", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);

    return Results.Ok(new
    {
        status = canConnect ? "healthy" : "degraded",
        service = "CounselMate API",
        database = canConnect ? "connected" : "unavailable",
        checkedAt = DateTimeOffset.UtcNow
    });
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

    var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
    var totalLeads = await db.Leads.CountAsync(lead => lead.TenantId == tenant.TenantId, cancellationToken);
    var enrolled = await db.Leads.CountAsync(lead => lead.TenantId == tenant.TenantId && lead.LeadStage.IsWonStage, cancellationToken);
    var contacted = await db.Leads.CountAsync(lead => lead.TenantId == tenant.TenantId && lead.LeadStage.SortOrder >= 20, cancellationToken);
    var pendingFollowUps = await db.FollowUps.CountAsync(item => item.TenantId == tenant.TenantId && item.Status == "Scheduled", cancellationToken);
    var newLeadsToday = await db.Leads.CountAsync(lead => lead.TenantId == tenant.TenantId && lead.CreatedAt >= todayStart, cancellationToken);
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

    var leads = await db.Leads
        .AsNoTracking()
        .Where(lead => lead.TenantId == tenant.TenantId)
        .OrderByDescending(lead => lead.CreatedAt)
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
            lead.CreatedAt,
            lead.NextFollowUpAt
        ))
        .ToListAsync(cancellationToken);

    return Results.Ok(leads);
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
        .Where(item => item.TenantId == tenant.TenantId)
        .OrderBy(item => item.SortOrder)
        .Select(item => new LookupOption(item.Id, item.Name))
        .ToListAsync(cancellationToken);

    var counselors = await db.Users
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.IsActive)
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
        item => item.TenantId == tenant.TenantId && item.Id == request.LeadStageId,
        cancellationToken);
    if (!stageExists)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["leadStageId"] = ["Select a valid lead stage."]
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
        var userExists = await db.Users.AnyAsync(
            item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == request.AssignedUserId,
            cancellationToken);
        if (!userExists)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedUserId"] = ["Select a valid active counsellor."]
            });
        }
    }

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
        CreatedAt = DateTimeOffset.UtcNow,
        NextFollowUpAt = request.NextFollowUpAt?.ToUniversalTime()
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
        CreatedAt = DateTimeOffset.UtcNow
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
            item.CreatedAt,
            item.NextFollowUpAt
        ))
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
            item.CreatedAt,
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
                    followUp.DueAt,
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

    var lead = await db.Leads
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null)
    {
        return Results.NotFound(new { message = "Lead not found." });
    }

    var validationErrors = ValidateUpdateLeadRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var stage = await db.LeadStages
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.Id == request.LeadStageId, cancellationToken);
    if (stage is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["leadStageId"] = ["Select a valid lead stage."]
        });
    }

    if (request.AssignedUserId is not null)
    {
        var userExists = await db.Users.AnyAsync(
            item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == request.AssignedUserId,
            cancellationToken);
        if (!userExists)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedUserId"] = ["Select a valid active counsellor."]
            });
        }
    }

    var previousStageId = lead.LeadStageId;
    var previousStatus = lead.Status;
    var previousPriority = lead.Priority;
    var previousAssignedUserId = lead.AssignedUserId;
    var previousFollowUpAt = lead.NextFollowUpAt;

    lead.LeadStageId = request.LeadStageId;
    lead.AssignedUserId = request.AssignedUserId;
    lead.Status = NormalizeOptionalText(request.Status) ?? lead.Status;
    lead.Priority = NormalizePriority(request.Priority);
    lead.NextFollowUpAt = request.NextFollowUpAt?.ToUniversalTime();

    var changes = new List<string>();
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
            CreatedByUserId = lead.AssignedUserId,
            Type = "LeadUpdated",
            Description = $"Lead {lead.LeadNumber} {string.Join(", ", changes)}.",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    await db.SaveChangesAsync(cancellationToken);
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

    var lead = await db.Leads
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.LeadNumber == id)
        .Select(item => new { item.Id, item.LeadNumber, item.AssignedUserId })
        .FirstOrDefaultAsync(cancellationToken);
    if (lead is null)
    {
        return Results.NotFound(new { message = "Lead not found." });
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
        CreatedByUserId = lead.AssignedUserId,
        Type = NormalizeActivityType(request.Type),
        Description = NormalizeName(request.Description),
        CreatedAt = DateTimeOffset.UtcNow
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

    var lead = await db.Leads
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == id, cancellationToken);
    if (lead is null)
    {
        return Results.NotFound(new { message = "Lead not found." });
    }

    var validationErrors = ValidateCreateFollowUpRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var assignedUserId = request.AssignedUserId ?? lead.AssignedUserId;
    if (assignedUserId is not null)
    {
        var userExists = await db.Users.AnyAsync(
            item => item.TenantId == tenant.TenantId && item.IsActive && item.Id == assignedUserId,
            cancellationToken);
        if (!userExists)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedUserId"] = ["Select a valid active counsellor."]
            });
        }
    }

    var dueAt = request.DueAt.ToUniversalTime();
    var followUp = new FollowUp
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        AssignedUserId = assignedUserId,
        Type = NormalizeFollowUpType(request.Type),
        Priority = NormalizePriority(request.Priority),
        Status = "Scheduled",
        DueAt = dueAt,
        CreatedAt = DateTimeOffset.UtcNow
    };

    lead.NextFollowUpAt = dueAt;
    db.FollowUps.Add(followUp);
    db.Activities.Add(new EducationCrm.Api.Models.Activity
    {
        Id = Guid.NewGuid(),
        TenantId = tenant.TenantId,
        LeadId = lead.Id,
        CreatedByUserId = assignedUserId,
        Type = "FollowUpScheduled",
        Description = $"{followUp.Type} follow-up scheduled for lead {lead.LeadNumber}.",
        CreatedAt = DateTimeOffset.UtcNow
    });

    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/leads/{lead.LeadNumber}", await GetLeadDetailAsync(db, tenant.TenantId, lead.LeadNumber, cancellationToken));
});

api.MapPost("/leads/{leadId}/follow-ups/{followUpId}/complete", async (
    string leadId,
    Guid followUpId,
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

    var lead = await db.Leads
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadNumber == leadId, cancellationToken);
    if (lead is null)
    {
        return Results.NotFound(new { message = "Lead not found." });
    }

    var followUp = await db.FollowUps
        .FirstOrDefaultAsync(item => item.TenantId == tenant.TenantId && item.LeadId == lead.Id && item.Id == followUpId, cancellationToken);
    if (followUp is null)
    {
        return Results.NotFound(new { message = "Follow-up not found." });
    }

    if (followUp.Status != "Completed")
    {
        followUp.Status = "Completed";
        db.Activities.Add(new EducationCrm.Api.Models.Activity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            LeadId = lead.Id,
            CreatedByUserId = followUp.AssignedUserId,
            Type = "FollowUpCompleted",
            Description = $"{followUp.Type} follow-up completed for lead {lead.LeadNumber}.",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    var nextScheduled = await db.FollowUps
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId && item.LeadId == lead.Id && item.Id != followUp.Id && item.Status == "Scheduled")
        .OrderBy(item => item.DueAt)
        .Select(item => (DateTimeOffset?)item.DueAt)
        .FirstOrDefaultAsync(cancellationToken);
    lead.NextFollowUpAt = nextScheduled;

    await db.SaveChangesAsync(cancellationToken);
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

    var stages = await db.LeadStages
        .AsNoTracking()
        .Where(stage => stage.TenantId == tenant.TenantId)
        .OrderBy(stage => stage.SortOrder)
        .Select(stage => new { stage.Id, stage.Name })
        .ToListAsync(cancellationToken);

    var leads = await db.Leads
        .AsNoTracking()
        .Where(lead => lead.TenantId == tenant.TenantId)
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
                lead.CreatedAt,
                lead.NextFollowUpAt
            )
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

    var followUps = await db.FollowUps
        .AsNoTracking()
        .Where(item => item.TenantId == tenant.TenantId)
        .OrderBy(item => item.DueAt)
        .Select(item => new FollowUpResponse(
            item.Id.ToString(),
            item.Lead.LeadNumber,
            item.Lead.StudentName,
            item.Type,
            item.Priority,
            item.Status,
            item.DueAt,
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

    var totalLeads = await db.Leads.CountAsync(lead => lead.TenantId == tenant.TenantId, cancellationToken);
    var stageCounts = await db.LeadStages
        .AsNoTracking()
        .Where(stage => stage.TenantId == tenant.TenantId)
        .OrderBy(stage => stage.SortOrder)
        .Select(stage => new
        {
            stage.Name,
            Count = stage.Leads.Count(lead => lead.TenantId == tenant.TenantId)
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
            item.CreatedAt,
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
                    followUp.DueAt,
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

    if (request.NextFollowUpAt is not null && request.NextFollowUpAt.Value < DateTimeOffset.UtcNow.AddMinutes(-5))
    {
        errors["nextFollowUpAt"] = ["Next follow-up cannot be in the past."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateUpdateLeadRequest(UpdateLeadRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (request.LeadStageId == Guid.Empty)
    {
        errors["leadStageId"] = ["Lead stage is required."];
    }

    AddOptionalLengthError(errors, "status", request.Status, 80);
    AddOptionalLengthError(errors, "priority", request.Priority, 40);

    if (request.NextFollowUpAt is not null && request.NextFollowUpAt.Value < DateTimeOffset.UtcNow.AddMinutes(-5))
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
    else if (request.DueAt < DateTimeOffset.UtcNow.AddMinutes(-5))
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

static bool IsValidEmail(string email)
{
    return Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
}

static string NormalizeName(string value)
{
    return Regex.Replace(value.Trim(), @"\s+", " ");
}

static string NormalizeEmail(string value)
{
    return value.Trim().ToLowerInvariant();
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
    DateTimeOffset CreatedAt,
    DateTimeOffset? NextFollowUpAt);

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
    DateTimeOffset CreatedAt,
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
    DateTimeOffset DueAt,
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
    Guid LeadStageId,
    Guid? AssignedUserId,
    string? Status,
    string? Priority,
    DateTimeOffset? NextFollowUpAt);

record AddLeadActivityRequest(
    string Description,
    string? Type);

record CreateFollowUpRequest(
    string? Type,
    string? Priority,
    Guid? AssignedUserId,
    DateTimeOffset DueAt);
