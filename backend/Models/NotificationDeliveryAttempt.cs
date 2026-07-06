namespace EducationCrm.Api.Models;

public sealed class NotificationDeliveryAttempt
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid NotificationId { get; set; }
    public string Channel { get; set; } = "InApp";
    public string Status { get; set; } = "Delivered";
    public int AttemptNumber { get; set; } = 1;
    public string? ProviderMessageId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset AttemptedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Notification Notification { get; set; } = null!;
}
