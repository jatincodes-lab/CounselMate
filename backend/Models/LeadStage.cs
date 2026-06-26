namespace EducationCrm.Api.Models;

public sealed class LeadStage
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsWonStage { get; set; }
    public bool IsLostStage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Lead> Leads { get; set; } = [];
}
