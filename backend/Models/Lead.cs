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
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset? NextFollowUpAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public Guid? ArchivedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Branch? Branch { get; set; }
    public Course Course { get; set; } = null!;
    public LeadStage LeadStage { get; set; } = null!;
    public LeadSource LeadSource { get; set; } = null!;
    public AppUser? AssignedUser { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public AppUser? UpdatedByUser { get; set; }
    public AppUser? ArchivedByUser { get; set; }
    public ICollection<FollowUp> FollowUps { get; set; } = [];
    public ICollection<Activity> Activities { get; set; } = [];
    public ICollection<LeadDocument> Documents { get; set; } = [];
    public ICollection<LeadPayment> Payments { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<AdmissionApplication> AdmissionApplications { get; set; } = [];
    public ICollection<Enrollment> Enrollments { get; set; } = [];
}
