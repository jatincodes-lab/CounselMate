namespace EducationCrm.Api.Models;

public sealed class AppUser
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Branch? Branch { get; set; }
    public ICollection<Lead> AssignedLeads { get; set; } = [];
    public ICollection<FollowUp> AssignedFollowUps { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public NotificationPreference? NotificationPreference { get; set; }
}

public enum UserRole
{
    Owner,
    Admin,
    BranchManager,
    Counselor,
    Telecaller,
    Accountant,
    ReadOnly
}
