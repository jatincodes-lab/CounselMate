namespace EducationCrm.Api.Models;

public sealed class LeadIntelligenceSettings
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public bool ScoringEnabled { get; set; } = true;
    public bool DistributionEnabled { get; set; }
    public string DefaultDistributionStrategy { get; set; } = "DefaultAssignee";
    public int PriorityWeight { get; set; } = 25;
    public int SourceWeight { get; set; } = 15;
    public int ResponseWeight { get; set; } = 20;
    public int EngagementWeight { get; set; } = 20;
    public int FreshnessWeight { get; set; } = 10;
    public int ProfileWeight { get; set; } = 10;
    public int HotThreshold { get; set; } = 75;
    public int WarmThreshold { get; set; } = 45;
    public int MaxActiveLeadsPerUser { get; set; } = 100;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
