namespace EducationCrm.Api.Models;

public sealed class LeadStage
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefaultStage { get; set; }
    public bool IsWonStage { get; set; }
    public bool IsLostStage { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Lead> Leads { get; set; } = [];
}
