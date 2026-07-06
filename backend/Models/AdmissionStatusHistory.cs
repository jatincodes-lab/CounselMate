namespace EducationCrm.Api.Models;

public sealed class AdmissionStatusHistory
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public Guid? ChangedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AdmissionApplication Application { get; set; } = null!;
    public AppUser? ChangedByUser { get; set; }
}
