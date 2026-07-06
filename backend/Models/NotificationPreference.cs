namespace EducationCrm.Api.Models;

public sealed class NotificationPreference
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool FollowUpRemindersEnabled { get; set; } = true;
    public bool PaymentRemindersEnabled { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
