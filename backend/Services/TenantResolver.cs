using EducationCrm.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EducationCrm.Api.Services;

public sealed record TenantScope(Guid TenantId, string Name, string Slug);

public sealed record AuthenticatedUser(
    Guid UserId,
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string? TenantLogoUrl,
    string TenantBrandColor,
    string FullName,
    string Email,
    string Role);

public static class TenantResolver
{
    public static AuthenticatedUser? GetCurrentUser(HttpContext httpContext)
    {
        return httpContext.Items.TryGetValue("CurrentUser", out var value)
            ? value as AuthenticatedUser
            : null;
    }

    public static async Task<TenantScope?> ResolveAsync(
        HttpContext httpContext,
        AppDbContext dbContext,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var currentUser = GetCurrentUser(httpContext);
        if (currentUser is not null)
        {
            return new TenantScope(currentUser.TenantId, currentUser.TenantName, currentUser.TenantSlug);
        }

        var tenantSlug = httpContext.Request.Headers["X-Tenant-Slug"].FirstOrDefault()
            ?? httpContext.Request.Query["tenant"].FirstOrDefault()
            ?? configuration["DefaultTenantSlug"]
            ?? "demo-academy";

        return await dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.IsActive && tenant.Slug == tenantSlug)
            .Select(tenant => new TenantScope(tenant.Id, tenant.Name, tenant.Slug))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
