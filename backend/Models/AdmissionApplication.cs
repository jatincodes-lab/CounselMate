namespace EducationCrm.Api.Models;

public sealed class AdmissionApplication
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? AssignedReviewerUserId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string? Intake { get; set; }
    public string? InternalNotes { get; set; }
    public string? DecisionReason { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public Branch? Branch { get; set; }
    public AppUser? AssignedReviewerUser { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public AppUser? UpdatedByUser { get; set; }
    public ICollection<AdmissionChecklistItem> ChecklistItems { get; set; } = [];
    public ICollection<AdmissionStatusHistory> StatusHistory { get; set; } = [];
    public Enrollment? Enrollment { get; set; }
}
