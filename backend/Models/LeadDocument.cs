namespace EducationCrm.Api.Models;

public sealed class LeadDocument
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid DocumentTypeId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string CloudinaryAssetId { get; set; } = string.Empty;
    public string CloudinaryPublicId { get; set; } = string.Empty;
    public string CloudinaryResourceType { get; set; } = string.Empty;
    public string CloudinaryDeliveryType { get; set; } = string.Empty;
    public string? CloudinarySecureUrl { get; set; }
    public string Status { get; set; } = "Uploaded";
    public string? Notes { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public DocumentType DocumentType { get; set; } = null!;
    public AppUser? UploadedByUser { get; set; }
    public AppUser? UpdatedByUser { get; set; }
    public AppUser? ReviewedByUser { get; set; }
}
