using EducationCrm.Api.Data;
using EducationCrm.Api.Services;
using Microsoft.EntityFrameworkCore;

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
        .FirstOrDefaultAsync(cancellationToken);

    return lead is null
        ? Results.NotFound(new { message = "Lead not found." })
        : Results.Ok(lead);
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

record FollowUpResponse(
    string Id,
    string LeadId,
    string StudentName,
    string Type,
    string Priority,
    string Status,
    DateTimeOffset DueAt,
    string AssignedTo);

record PipelineStageResponse(string Name, int Count, IReadOnlyCollection<LeadResponse> Leads);

record FunnelStep(string Name, int Count, decimal Percentage);
