namespace EducationCrm.Api.Models;

public sealed class Activity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser? CreatedByUser { get; set; }
}
