using EducationCrm.Api.Data;
using EducationCrm.Api.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace EducationCrm.Api.Services;

public sealed class ReminderNotificationJob(AppDbContext db, ILogger<ReminderNotificationJob> logger)
{
    private const int LookAheadMinutes = 15;
    private const int MaximumPastDueDays = 30;

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 900], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisableConcurrentExecution(timeoutInSeconds: 240)]
    [Queue("notifications")]
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var now = IndianClock.Now();
        var dismissed = await DismissObsoleteNotificationsAsync(now, cancellationToken);
        var followUpCount = await CreateFollowUpNotificationsAsync(now, cancellationToken);
        var paymentCount = await CreatePaymentNotificationsAsync(now, cancellationToken);
        var deleted = await DeleteExpiredNotificationsAsync(now, cancellationToken);

        logger.LogInformation(
            "Reminder scan completed. Created {FollowUpCount} follow-up and {PaymentCount} payment notifications; dismissed {DismissedCount}; deleted {DeletedCount} expired records.",
            followUpCount,
            paymentCount,
            dismissed,
            deleted);
    }

    private async Task<int> DismissObsoleteNotificationsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var active = await db.Notifications
            .Include(item => item.RecipientUser)
                .ThenInclude(item => item.NotificationPreference)
            .Include(item => item.Lead)
            .Include(item => item.FollowUp)
            .Include(item => item.LeadPayment)
                .ThenInclude(item => item!.Transactions)
            .Where(item => item.DismissedAt == null && (item.FollowUpId != null || item.LeadPaymentId != null))
            .OrderBy(item => item.CreatedAt)
            .Take(5000)
            .ToListAsync(cancellationToken);

        foreach (var notification in active)
        {
            var obsolete = !notification.RecipientUser.IsActive || notification.Lead?.ArchivedAt is not null;

            if (notification.FollowUpId is not null)
            {
                obsolete = obsolete ||
                    notification.FollowUp is null ||
                    !string.Equals(notification.FollowUp.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
                    notification.EntityVersion != notification.FollowUp.Version ||
                    notification.RecipientUser.NotificationPreference?.FollowUpRemindersEnabled == false ||
                    !CanUserAccessLead(notification.RecipientUser, notification.Lead);
            }

            if (notification.LeadPaymentId is not null)
            {
                var payment = notification.LeadPayment;
                obsolete = obsolete || payment is null || payment.CancelledAt is not null ||
                    notification.EntityVersion != payment.Version ||
                    payment.Transactions.Sum(item => item.Amount) >= payment.AmountDue ||
                    notification.RecipientUser.NotificationPreference?.PaymentRemindersEnabled == false ||
                    notification.RecipientUser.Role is not (UserRole.Owner or UserRole.Admin or UserRole.Accountant);
            }

            if (obsolete)
            {
                notification.DismissedAt = now;
            }
        }

        return await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> CreateFollowUpNotificationsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lowerBound = now.AddDays(-MaximumPastDueDays);
        var upperBound = now.AddMinutes(LookAheadMinutes);
        var followUps = await db.FollowUps
            .AsNoTracking()
            .Where(item => item.Status == "Scheduled" &&
                item.DueAt >= lowerBound &&
                item.DueAt <= upperBound &&
                item.Lead.ArchivedAt == null &&
                item.Tenant.IsActive)
            .Select(item => new
            {
                item.Id,
                item.TenantId,
                item.LeadId,
                item.Version,
                item.DueAt,
                item.Type,
                item.Priority,
                StudentName = item.Lead.StudentName,
                RecipientUserId = item.AssignedUserId ?? item.Lead.AssignedUserId,
                LeadAssignedUserId = item.Lead.AssignedUserId,
                LeadBranchId = item.Lead.BranchId
            })
            .Where(item => item.RecipientUserId != null)
            .OrderBy(item => item.DueAt)
            .Take(1000)
            .ToListAsync(cancellationToken);

        var recipientIds = followUps.Select(item => item.RecipientUserId!.Value).Distinct().ToArray();
        var enabledUsers = await GetEnabledUsersAsync(recipientIds, followUp: true, cancellationToken);
        var accessUsers = await db.Users
            .AsNoTracking()
            .Where(item => recipientIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Role, item.BranchId })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var pending = new List<Notification>();

        foreach (var followUp in followUps.Where(item =>
            enabledUsers.Contains(item.RecipientUserId!.Value) &&
            accessUsers.TryGetValue(item.RecipientUserId.Value, out var user) &&
            CanUserAccessLead(user.Role, user.Id, user.BranchId, item.LeadAssignedUserId, item.LeadBranchId)))
        {
            var overdue = followUp.DueAt < now;
            var type = overdue ? "FollowUpOverdue" : "FollowUpDue";
            var key = $"{type}:{followUp.Id:N}:v{followUp.Version}";
            pending.Add(CreateNotification(
                followUp.TenantId,
                followUp.RecipientUserId!.Value,
                followUp.LeadId,
                followUp.Id,
                null,
                followUp.Version,
                type,
                overdue ? "Follow-up overdue" : "Follow-up due soon",
                $"{followUp.Type} with {followUp.StudentName} is {(overdue ? "overdue" : "due")} at {followUp.DueAt:dd MMM yyyy, hh:mm tt}.",
                overdue || string.Equals(followUp.Priority, "High", StringComparison.OrdinalIgnoreCase) ? "Warning" : "Info",
                key,
                followUp.DueAt,
                now));
        }

        return await AddMissingNotificationsAsync(pending, cancellationToken);
    }

    private async Task<int> CreatePaymentNotificationsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lowerBound = now.AddDays(-MaximumPastDueDays);
        var upperBound = now.AddHours(24);
        var payments = await db.LeadPayments
            .AsNoTracking()
            .Where(item => item.DueDate != null &&
                item.DueDate >= lowerBound &&
                item.DueDate <= upperBound &&
                item.CancelledAt == null &&
                item.Lead.ArchivedAt == null &&
                item.Tenant.IsActive)
            .Select(item => new
            {
                item.Id,
                item.TenantId,
                item.LeadId,
                item.Version,
                DueDate = item.DueDate!.Value,
                item.Title,
                item.AmountDue,
                Paid = item.Transactions.Sum(transaction => (decimal?)transaction.Amount) ?? 0m,
                StudentName = item.Lead.StudentName
            })
            .Where(item => item.Paid < item.AmountDue)
            .OrderBy(item => item.DueDate)
            .Take(1000)
            .ToListAsync(cancellationToken);

        if (payments.Count == 0)
        {
            return 0;
        }

        var tenantIds = payments.Select(item => item.TenantId).Distinct().ToArray();
        var recipients = await db.Users
            .AsNoTracking()
            .Where(item => tenantIds.Contains(item.TenantId) && item.IsActive &&
                (item.Role == UserRole.Owner || item.Role == UserRole.Admin || item.Role == UserRole.Accountant))
            .Select(item => new { item.Id, item.TenantId })
            .ToListAsync(cancellationToken);
        var enabledUsers = await GetEnabledUsersAsync(recipients.Select(item => item.Id).ToArray(), followUp: false, cancellationToken);
        var pending = new List<Notification>();

        foreach (var payment in payments)
        {
            var overdue = payment.DueDate.Date < now.Date;
            var type = overdue ? "PaymentOverdue" : "PaymentDue";
            foreach (var recipient in recipients.Where(item => item.TenantId == payment.TenantId && enabledUsers.Contains(item.Id)))
            {
                var key = $"{type}:{payment.Id:N}:v{payment.Version}";
                var balance = payment.AmountDue - payment.Paid;
                pending.Add(CreateNotification(
                    payment.TenantId,
                    recipient.Id,
                    payment.LeadId,
                    null,
                    payment.Id,
                    payment.Version,
                    type,
                    overdue ? "Payment overdue" : "Payment due soon",
                    $"{payment.Title} for {payment.StudentName} has an INR {balance:N2} balance {(overdue ? "overdue since" : "due on")} {payment.DueDate:dd MMM yyyy}.",
                    overdue ? "Critical" : "Warning",
                    key,
                    payment.DueDate,
                    now));
            }
        }

        return await AddMissingNotificationsAsync(pending, cancellationToken);
    }

    private async Task<HashSet<Guid>> GetEnabledUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        bool followUp,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var users = await db.Users
            .AsNoTracking()
            .Where(item => userIds.Contains(item.Id) && item.IsActive && item.Tenant.IsActive)
            .Select(item => new
            {
                item.Id,
                Preference = item.NotificationPreference
            })
            .ToListAsync(cancellationToken);

        return users
            .Where(item => item.Preference is null ||
                (followUp ? item.Preference.FollowUpRemindersEnabled : item.Preference.PaymentRemindersEnabled))
            .Select(item => item.Id)
            .ToHashSet();
    }

    private async Task<int> AddMissingNotificationsAsync(List<Notification> candidates, CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return 0;
        }

        var tenantIds = candidates.Select(item => item.TenantId).Distinct().ToArray();
        var recipientIds = candidates.Select(item => item.RecipientUserId).Distinct().ToArray();
        var keys = candidates.Select(item => item.DeduplicationKey).Distinct().ToArray();
        var existing = await db.Notifications
            .AsNoTracking()
            .Where(item => tenantIds.Contains(item.TenantId) &&
                recipientIds.Contains(item.RecipientUserId) &&
                keys.Contains(item.DeduplicationKey))
            .Select(item => new { item.TenantId, item.RecipientUserId, item.DeduplicationKey })
            .ToListAsync(cancellationToken);
        var existingKeys = existing
            .Select(item => $"{item.TenantId:N}:{item.RecipientUserId:N}:{item.DeduplicationKey}")
            .ToHashSet(StringComparer.Ordinal);
        var missing = candidates
            .Where(item => existingKeys.Add($"{item.TenantId:N}:{item.RecipientUserId:N}:{item.DeduplicationKey}"))
            .ToList();

        foreach (var notification in missing)
        {
            notification.DeliveryAttempts.Add(new NotificationDeliveryAttempt
            {
                Id = Guid.NewGuid(),
                TenantId = notification.TenantId,
                Channel = "InApp",
                Status = "Delivered",
                AttemptNumber = 1,
                AttemptedAt = notification.CreatedAt,
                CompletedAt = notification.CreatedAt
            });
        }

        db.Notifications.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }

    private async Task<int> DeleteExpiredNotificationsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var retentionCutoff = now.AddDays(-90);
        return await db.Notifications
            .Where(item => item.CreatedAt < retentionCutoff && (item.ReadAt != null || item.DismissedAt != null || item.ExpiresAt < now))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static Notification CreateNotification(
        Guid tenantId,
        Guid recipientUserId,
        Guid leadId,
        Guid? followUpId,
        Guid? paymentId,
        int entityVersion,
        string type,
        string title,
        string message,
        string severity,
        string deduplicationKey,
        DateTimeOffset scheduledFor,
        DateTimeOffset now)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientUserId = recipientUserId,
            LeadId = leadId,
            FollowUpId = followUpId,
            LeadPaymentId = paymentId,
            EntityVersion = entityVersion,
            Type = type,
            Title = title,
            Message = message,
            Severity = severity,
            DeduplicationKey = deduplicationKey,
            ScheduledFor = scheduledFor,
            CreatedAt = now,
            ExpiresAt = now.AddDays(90)
        };
    }

    private static bool CanUserAccessLead(AppUser user, Lead? lead)
    {
        return lead is not null && CanUserAccessLead(user.Role, user.Id, user.BranchId, lead.AssignedUserId, lead.BranchId);
    }

    private static bool CanUserAccessLead(
        UserRole role,
        Guid userId,
        Guid? userBranchId,
        Guid? leadAssignedUserId,
        Guid? leadBranchId)
    {
        return role switch
        {
            UserRole.Owner or UserRole.Admin => true,
            UserRole.BranchManager => userBranchId is not null && userBranchId == leadBranchId,
            UserRole.Counselor or UserRole.Telecaller => leadAssignedUserId == userId,
            _ => false
        };
    }
}
