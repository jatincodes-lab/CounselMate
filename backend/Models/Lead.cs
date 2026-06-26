namespace EducationCrm.Api.Models;

public sealed class Lead
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid CourseId { get; set; }
    public Guid LeadStageId { get; set; }
    public Guid LeadSourceId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string LeadNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? GuardianName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string NormalizedPhone { get; set; } = string.Empty;
    public string? City { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? NextFollowUpAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Branch? Branch { get; set; }
    public Course Course { get; set; } = null!;
    public LeadStage LeadStage { get; set; } = null!;
    public LeadSource LeadSource { get; set; } = null!;
    public AppUser? AssignedUser { get; set; }
    public ICollection<FollowUp> FollowUps { get; set; } = [];
    public ICollection<Activity> Activities { get; set; } = [];
}
