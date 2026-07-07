namespace EducationCrm.Api.Models;

public sealed class LeadIntelligenceEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? PreviousScore { get; set; }
    public int? NewScore { get; set; }
    public string? PreviousTemperature { get; set; }
    public string? NewTemperature { get; set; }
    public Guid? PreviousAssignedUserId { get; set; }
    public Guid? NewAssignedUserId { get; set; }
    public Guid? DistributionRuleId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser? PreviousAssignedUser { get; set; }
    public AppUser? NewAssignedUser { get; set; }
    public LeadDistributionRule? DistributionRule { get; set; }
}
