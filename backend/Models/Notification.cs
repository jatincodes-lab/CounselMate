namespace EducationCrm.Api.Models;

public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RecipientUserId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? FollowUpId { get; set; }
    public Guid? LeadPaymentId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string DeduplicationKey { get; set; } = string.Empty;
    public int? EntityVersion { get; set; }
    public DateTimeOffset ScheduledFor { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AppUser RecipientUser { get; set; } = null!;
    public Lead? Lead { get; set; }
    public FollowUp? FollowUp { get; set; }
    public LeadPayment? LeadPayment { get; set; }
    public ICollection<NotificationDeliveryAttempt> DeliveryAttempts { get; set; } = [];
}
