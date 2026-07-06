namespace EducationCrm.Api.Models;

public sealed class LeadPaymentTransaction
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadPaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateTimeOffset PaidAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public LeadPayment LeadPayment { get; set; } = null!;
    public AppUser? CreatedByUser { get; set; }
}
