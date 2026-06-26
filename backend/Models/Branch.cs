namespace EducationCrm.Api.Models;

public sealed class Branch
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<AppUser> Users { get; set; } = [];
    public ICollection<Lead> Leads { get; set; } = [];
}
