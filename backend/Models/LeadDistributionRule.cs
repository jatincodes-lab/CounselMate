namespace EducationCrm.Api.Models;

public sealed class LeadDistributionRule
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int PriorityOrder { get; set; } = 100;
    public string Strategy { get; set; } = "RoundRobin";
    public Guid? BranchId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? LeadSourceId { get; set; }
    public string TargetUserIds { get; set; } = string.Empty;
    public Guid? LastAssignedUserId { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Branch? Branch { get; set; }
    public Course? Course { get; set; }
    public LeadSource? LeadSource { get; set; }
    public AppUser? LastAssignedUser { get; set; }
}
