namespace EducationCrm.Api.Models;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "India";
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public string? LogoUrl { get; set; }
    public string BrandColor { get; set; } = "#2171D3";
    public Guid? DefaultBranchId { get; set; }
    public Guid? DefaultAssigneeUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<Branch> Branches { get; set; } = [];
    public ICollection<AppUser> Users { get; set; } = [];
    public ICollection<Course> Courses { get; set; } = [];
    public ICollection<LeadStage> LeadStages { get; set; } = [];
    public ICollection<LeadSource> LeadSources { get; set; } = [];
    public ICollection<Lead> Leads { get; set; } = [];
    public ICollection<CommunicationTemplate> CommunicationTemplates { get; set; } = [];
    public ICollection<DocumentType> DocumentTypes { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<NotificationPreference> NotificationPreferences { get; set; } = [];
    public ICollection<AdmissionApplication> AdmissionApplications { get; set; } = [];
    public ICollection<Enrollment> Enrollments { get; set; } = [];
    public Branch? DefaultBranch { get; set; }
    public AppUser? DefaultAssigneeUser { get; set; }
}
