namespace EducationCrm.Api.Models;

public sealed class Enrollment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid LeadId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? BranchId { get; set; }
    public string EnrollmentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? Intake { get; set; }
    public string Status { get; set; } = "Active";
    public int Version { get; set; } = 1;
    public DateTimeOffset EnrolledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AdmissionApplication Application { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public Branch? Branch { get; set; }
    public AppUser? CreatedByUser { get; set; }
}
