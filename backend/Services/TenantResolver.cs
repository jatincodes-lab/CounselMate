using EducationCrm.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EducationCrm.Api.Services;

public sealed record TenantScope(Guid TenantId, string Name, string Slug);

public static class TenantResolver
{
    public static async Task<TenantScope?> ResolveAsync(
        HttpContext httpContext,
        AppDbContext dbContext,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
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
