namespace EducationCrm.Api.Models;

public sealed class LeadPayment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal AmountDue { get; set; }
    public string Currency { get; set; } = "INR";
    public DateTimeOffset? DueDate { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public Guid? CancelledByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser? CreatedByUser { get; set; }
    public AppUser? UpdatedByUser { get; set; }
    public AppUser? CancelledByUser { get; set; }
    public ICollection<LeadPaymentTransaction> Transactions { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
}
