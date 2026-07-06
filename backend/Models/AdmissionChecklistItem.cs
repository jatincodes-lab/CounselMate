namespace EducationCrm.Api.Models;

public sealed class AdmissionChecklistItem
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public bool IsRequired { get; set; } = true;
    public bool IsCompleted { get; set; }
    public bool IsWaived { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AdmissionApplication Application { get; set; } = null!;
    public AppUser? CompletedByUser { get; set; }
}
