using EducationCrm.Api.Models;
using EducationCrm.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using CrmActivity = EducationCrm.Api.Models.Activity;

namespace EducationCrm.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<LeadStage> LeadStages => Set<LeadStage>();
    public DbSet<LeadSource> LeadSources => Set<LeadSource>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<CrmActivity> Activities => Set<CrmActivity>();
    public DbSet<CommunicationTemplate> CommunicationTemplates => Set<CommunicationTemplate>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<LeadDocument> LeadDocuments => Set<LeadDocument>();
    public DbSet<LeadPayment> LeadPayments => Set<LeadPayment>();
    public DbSet<LeadPaymentTransaction> LeadPaymentTransactions => Set<LeadPaymentTransaction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<AdmissionApplication> AdmissionApplications => Set<AdmissionApplication>();
    public DbSet<AdmissionChecklistItem> AdmissionChecklistItems => Set<AdmissionChecklistItem>();
    public DbSet<AdmissionStatusHistory> AdmissionStatusHistories => Set<AdmissionStatusHistory>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureIndianTimestamps(modelBuilder);
        ConfigureTenants(modelBuilder);
        ConfigureBranches(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureCourses(modelBuilder);
        ConfigureLeadStages(modelBuilder);
        ConfigureLeadSources(modelBuilder);
        ConfigureLeads(modelBuilder);
        ConfigureFollowUps(modelBuilder);
        ConfigureActivities(modelBuilder);
        ConfigureCommunicationTemplates(modelBuilder);
        ConfigureDocumentTypes(modelBuilder);
        ConfigureLeadDocuments(modelBuilder);
        ConfigureLeadPayments(modelBuilder);
        ConfigureLeadPaymentTransactions(modelBuilder);
        ConfigureNotifications(modelBuilder);
        ConfigureNotificationDeliveryAttempts(modelBuilder);
        ConfigureNotificationPreferences(modelBuilder);
        ConfigureAdmissionApplications(modelBuilder);
        ConfigureAdmissionChecklistItems(modelBuilder);
        ConfigureAdmissionStatusHistories(modelBuilder);
        ConfigureEnrollments(modelBuilder);
        SeedDemoTenant(modelBuilder);
    }

    private static void ConfigureIndianTimestamps(ModelBuilder modelBuilder)
    {
        var timestampConverter = new ValueConverter<DateTimeOffset, DateTime>(
            value => value.ToOffset(IndianClock.Offset).DateTime,
            value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), IndianClock.Offset));

        var nullableTimestampConverter = new ValueConverter<DateTimeOffset?, DateTime?>(
            value => value.HasValue ? value.Value.ToOffset(IndianClock.Offset).DateTime : null,
            value => value.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified), IndianClock.Offset)
                : null);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(timestampConverter);
                    property.SetColumnType("timestamp without time zone");
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableTimestampConverter);
                    property.SetColumnType("timestamp without time zone");
                }
            }
        }
    }

    private static void ConfigureTenants(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Slug).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ContactEmail).HasMaxLength(240);
            entity.Property(item => item.ContactPhone).HasMaxLength(40);
            entity.Property(item => item.WebsiteUrl).HasMaxLength(500);
            entity.Property(item => item.AddressLine1).HasMaxLength(200);
            entity.Property(item => item.AddressLine2).HasMaxLength(200);
            entity.Property(item => item.City).HasMaxLength(120);
            entity.Property(item => item.State).HasMaxLength(120);
            entity.Property(item => item.PostalCode).HasMaxLength(20);
            entity.Property(item => item.Country).HasMaxLength(80).IsRequired();
            entity.Property(item => item.TimeZone).HasMaxLength(100).IsRequired();
            entity.Property(item => item.LogoUrl).HasMaxLength(500);
            entity.Property(item => item.BrandColor).HasMaxLength(7).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.DefaultBranch)
                .WithMany()
                .HasForeignKey(item => item.DefaultBranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.DefaultAssigneeUser)
                .WithMany()
                .HasForeignKey(item => item.DefaultAssigneeUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureBranches(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("branches");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.NormalizedName }).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.City).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.Branches)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.Email }).IsUnique();
            entity.Property(item => item.FullName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(240).IsRequired();
            entity.Property(item => item.PasswordHash).HasMaxLength(220).IsRequired();
            entity.Property(item => item.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.Users)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Branch)
                .WithMany(item => item.Users)
                .HasForeignKey(item => item.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureCourses(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("courses");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.NormalizedName }).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.Courses)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLeadStages(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadStage>(entity =>
        {
            entity.ToTable("lead_stages");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.NormalizedName }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.SortOrder }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.IsDefaultStage })
                .HasFilter("\"IsDefaultStage\" = TRUE AND \"IsActive\" = TRUE")
                .IsUnique();
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.LeadStages)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLeadSources(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadSource>(entity =>
        {
            entity.ToTable("lead_sources");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.NormalizedName }).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.LeadSources)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLeads(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lead>(entity =>
        {
            entity.ToTable("leads");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.TenantId);
            entity.HasIndex(item => new { item.TenantId, item.LeadNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.NormalizedPhone }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.ArchivedAt });
            entity.HasIndex(item => new { item.TenantId, item.AssignedUserId });
            entity.HasIndex(item => new { item.TenantId, item.BranchId });
            entity.HasIndex(item => new { item.TenantId, item.LeadStageId });
            entity.Property(item => item.LeadNumber).HasMaxLength(40).IsRequired();
            entity.Property(item => item.StudentName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.GuardianName).HasMaxLength(160);
            entity.Property(item => item.Email).HasMaxLength(240).IsRequired();
            entity.Property(item => item.Phone).HasMaxLength(40).IsRequired();
            entity.Property(item => item.NormalizedPhone).HasMaxLength(32).IsRequired();
            entity.Property(item => item.City).HasMaxLength(120);
            entity.Property(item => item.Status).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Priority).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.Leads)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Branch)
                .WithMany(item => item.Leads)
                .HasForeignKey(item => item.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.Course)
                .WithMany(item => item.Leads)
                .HasForeignKey(item => item.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LeadStage)
                .WithMany(item => item.Leads)
                .HasForeignKey(item => item.LeadStageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LeadSource)
                .WithMany(item => item.Leads)
                .HasForeignKey(item => item.LeadSourceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AssignedUser)
                .WithMany(item => item.AssignedLeads)
                .HasForeignKey(item => item.AssignedUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.UpdatedByUser)
                .WithMany()
                .HasForeignKey(item => item.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.ArchivedByUser)
                .WithMany()
                .HasForeignKey(item => item.ArchivedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureFollowUps(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FollowUp>(entity =>
        {
            entity.ToTable("follow_ups");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.DueAt });
            entity.HasIndex(item => new { item.TenantId, item.Status, item.DueAt });
            entity.Property(item => item.Type).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Priority).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant)
                .WithMany()
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Lead)
                .WithMany(item => item.FollowUps)
                .HasForeignKey(item => item.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.AssignedUser)
                .WithMany(item => item.AssignedFollowUps)
                .HasForeignKey(item => item.AssignedUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureActivities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CrmActivity>(entity =>
        {
            entity.ToTable("activities");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.CreatedAt });
            entity.Property(item => item.Type).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000).IsRequired();
            entity.HasOne(item => item.Tenant)
                .WithMany()
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Lead)
                .WithMany(item => item.Activities)
                .HasForeignKey(item => item.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureCommunicationTemplates(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommunicationTemplate>(entity =>
        {
            entity.ToTable("communication_templates");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.NormalizedName }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.IsActive, item.Channel });
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Channel).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Body).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.CommunicationTemplates)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.UpdatedByUser)
                .WithMany()
                .HasForeignKey(item => item.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureDocumentTypes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentType>(entity =>
        {
            entity.ToTable("document_types");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.NormalizedName }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.SortOrder }).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.DocumentTypes)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLeadDocuments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadDocument>(entity =>
        {
            entity.ToTable("lead_documents");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.LeadId, item.DocumentTypeId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Status });
            entity.HasIndex(item => new { item.TenantId, item.CloudinaryPublicId });
            entity.Property(item => item.OriginalFileName).HasMaxLength(240).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(item => item.CloudinaryAssetId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.CloudinaryPublicId).HasMaxLength(500).IsRequired();
            entity.Property(item => item.CloudinaryResourceType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CloudinaryDeliveryType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CloudinarySecureUrl).HasMaxLength(1000);
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(500);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant)
                .WithMany()
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Lead)
                .WithMany(item => item.Documents)
                .HasForeignKey(item => item.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.DocumentType)
                .WithMany(item => item.LeadDocuments)
                .HasForeignKey(item => item.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.UploadedByUser)
                .WithMany()
                .HasForeignKey(item => item.UploadedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.UpdatedByUser)
                .WithMany()
                .HasForeignKey(item => item.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.ReviewedByUser)
                .WithMany()
                .HasForeignKey(item => item.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureLeadPayments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadPayment>(entity =>
        {
            entity.ToTable("lead_payments");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.LeadId });
            entity.HasIndex(item => new { item.TenantId, item.Status });
            entity.HasIndex(item => new { item.TenantId, item.DueDate });
            entity.Property(item => item.Title).HasMaxLength(160).IsRequired();
            entity.Property(item => item.AmountDue).HasPrecision(12, 2);
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(500);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant)
                .WithMany()
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Lead)
                .WithMany(item => item.Payments)
                .HasForeignKey(item => item.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.UpdatedByUser)
                .WithMany()
                .HasForeignKey(item => item.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.CancelledByUser)
                .WithMany()
                .HasForeignKey(item => item.CancelledByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureLeadPaymentTransactions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadPaymentTransaction>(entity =>
        {
            entity.ToTable("lead_payment_transactions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.LeadPaymentId });
            entity.HasIndex(item => new { item.TenantId, item.ReceiptNumber })
                .HasFilter("\"ReceiptNumber\" IS NOT NULL")
                .IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ReferenceNumber });
            entity.Property(item => item.Amount).HasPrecision(12, 2);
            entity.Property(item => item.Method).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ReferenceNumber).HasMaxLength(120);
            entity.Property(item => item.ReceiptNumber).HasMaxLength(120);
            entity.Property(item => item.Notes).HasMaxLength(500);
            entity.HasOne(item => item.Tenant)
                .WithMany()
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LeadPayment)
                .WithMany(item => item.Transactions)
                .HasForeignKey(item => item.LeadPaymentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.RecipientUserId, item.DeduplicationKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.RecipientUserId, item.DismissedAt, item.ReadAt, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.FollowUpId });
            entity.HasIndex(item => new { item.TenantId, item.LeadPaymentId });
            entity.HasIndex(item => item.ExpiresAt);
            entity.Property(item => item.Type).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(180).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(600).IsRequired();
            entity.Property(item => item.Severity).HasMaxLength(20).IsRequired();
            entity.Property(item => item.DeduplicationKey).HasMaxLength(240).IsRequired();
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.Notifications)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RecipientUser)
                .WithMany(item => item.Notifications)
                .HasForeignKey(item => item.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Lead)
                .WithMany(item => item.Notifications)
                .HasForeignKey(item => item.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.FollowUp)
                .WithMany(item => item.Notifications)
                .HasForeignKey(item => item.FollowUpId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.LeadPayment)
                .WithMany(item => item.Notifications)
                .HasForeignKey(item => item.LeadPaymentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureNotificationDeliveryAttempts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationDeliveryAttempt>(entity =>
        {
            entity.ToTable("notification_delivery_attempts");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.NotificationId, item.Channel, item.AttemptNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Status, item.AttemptedAt });
            entity.Property(item => item.Channel).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(30).IsRequired();
            entity.Property(item => item.ProviderMessageId).HasMaxLength(240);
            entity.Property(item => item.ErrorCode).HasMaxLength(80);
            entity.Property(item => item.ErrorMessage).HasMaxLength(500);
            entity.HasOne(item => item.Tenant)
                .WithMany()
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Notification)
                .WithMany(item => item.DeliveryAttempts)
                .HasForeignKey(item => item.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureNotificationPreferences(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.ToTable("notification_preferences");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.UserId }).IsUnique();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant)
                .WithMany(item => item.NotificationPreferences)
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.User)
                .WithOne(item => item.NotificationPreference)
                .HasForeignKey<NotificationPreference>(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureAdmissionApplications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdmissionApplication>(entity =>
        {
            entity.ToTable("admission_applications");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.ApplicationNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.LeadId, item.CourseId, item.Intake }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Status, item.UpdatedAt });
            entity.HasIndex(item => new { item.TenantId, item.AssignedReviewerUserId });
            entity.Property(item => item.ApplicationNumber).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Intake).HasMaxLength(120);
            entity.Property(item => item.InternalNotes).HasMaxLength(1000);
            entity.Property(item => item.DecisionReason).HasMaxLength(500);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant).WithMany(item => item.AdmissionApplications).HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Lead).WithMany(item => item.AdmissionApplications).HasForeignKey(item => item.LeadId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Course).WithMany().HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Branch).WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.AssignedReviewerUser).WithMany().HasForeignKey(item => item.AssignedReviewerUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.CreatedByUser).WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.UpdatedByUser).WithMany().HasForeignKey(item => item.UpdatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureAdmissionChecklistItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdmissionChecklistItem>(entity =>
        {
            entity.ToTable("admission_checklist_items");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.ApplicationId, item.SortOrder });
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(500);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Application).WithMany(item => item.ChecklistItems).HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.CompletedByUser).WithMany().HasForeignKey(item => item.CompletedByUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureAdmissionStatusHistories(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdmissionStatusHistory>(entity =>
        {
            entity.ToTable("admission_status_history");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.ApplicationId, item.ChangedAt });
            entity.Property(item => item.PreviousStatus).HasMaxLength(40);
            entity.Property(item => item.NewStatus).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Note).HasMaxLength(500);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Application).WithMany(item => item.StatusHistory).HasForeignKey(item => item.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.ChangedByUser).WithMany().HasForeignKey(item => item.ChangedByUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureEnrollments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("enrollments");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.EnrollmentNumber }).IsUnique();
            entity.HasIndex(item => item.ApplicationId).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.LeadId });
            entity.Property(item => item.EnrollmentNumber).HasMaxLength(40).IsRequired();
            entity.Property(item => item.StudentName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Intake).HasMaxLength(120);
            entity.Property(item => item.Status).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasOne(item => item.Tenant).WithMany(item => item.Enrollments).HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Application).WithOne(item => item.Enrollment).HasForeignKey<Enrollment>(item => item.ApplicationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Lead).WithMany(item => item.Enrollments).HasForeignKey(item => item.LeadId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Course).WithMany().HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Branch).WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.CreatedByUser).WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void SeedDemoTenant(ModelBuilder modelBuilder)
    {
        var createdAt = new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero);
        var tenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var branchId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var rahulId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var vermaId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var khannaId = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var mbaCourseId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var dataScienceCourseId = Guid.Parse("40000000-0000-0000-0000-000000000002");
        var uiUxCourseId = Guid.Parse("40000000-0000-0000-0000-000000000003");
        var fullStackCourseId = Guid.Parse("40000000-0000-0000-0000-000000000004");
        var digitalMarketingCourseId = Guid.Parse("40000000-0000-0000-0000-000000000005");
        var newInquiryStageId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        var contactedStageId = Guid.Parse("50000000-0000-0000-0000-000000000002");
        var interestedStageId = Guid.Parse("50000000-0000-0000-0000-000000000003");
        var demoStageId = Guid.Parse("50000000-0000-0000-0000-000000000004");
        var applicationStageId = Guid.Parse("50000000-0000-0000-0000-000000000005");
        var enrolledStageId = Guid.Parse("50000000-0000-0000-0000-000000000006");
        var droppedStageId = Guid.Parse("50000000-0000-0000-0000-000000000007");
        var googleSourceId = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var websiteSourceId = Guid.Parse("60000000-0000-0000-0000-000000000002");
        var linkedInSourceId = Guid.Parse("60000000-0000-0000-0000-000000000003");
        var referralSourceId = Guid.Parse("60000000-0000-0000-0000-000000000004");
        var expoSourceId = Guid.Parse("60000000-0000-0000-0000-000000000005");
        var lead1Id = Guid.Parse("70000000-0000-0000-0000-000000000001");
        var lead2Id = Guid.Parse("70000000-0000-0000-0000-000000000002");
        var lead3Id = Guid.Parse("70000000-0000-0000-0000-000000000003");
        var lead4Id = Guid.Parse("70000000-0000-0000-0000-000000000004");
        var lead5Id = Guid.Parse("70000000-0000-0000-0000-000000000005");
        var template1Id = Guid.Parse("90000000-0000-0000-0000-000000000001");
        var template2Id = Guid.Parse("90000000-0000-0000-0000-000000000002");
        var template3Id = Guid.Parse("90000000-0000-0000-0000-000000000003");
        var template4Id = Guid.Parse("90000000-0000-0000-0000-000000000004");
        var idProofDocumentTypeId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
        var addressProofDocumentTypeId = Guid.Parse("a0000000-0000-0000-0000-000000000002");
        var academicMarksheetDocumentTypeId = Guid.Parse("a0000000-0000-0000-0000-000000000003");
        var admissionFormDocumentTypeId = Guid.Parse("a0000000-0000-0000-0000-000000000004");
        var paymentReceiptDocumentTypeId = Guid.Parse("a0000000-0000-0000-0000-000000000005");
        const string demoPasswordHash = "v1.100000.Mt8GC3coU3xegrVi+C2aAw==.i10uchOOE1k5pG2zdL/PW+FxQ7wZ9yM+MW/hwgowbPM=";

        modelBuilder.Entity<Tenant>().HasData(new Tenant
        {
            Id = tenantId,
            Name = "Demo Academy",
            Slug = "demo-academy",
            ContactEmail = "admissions@demo-academy.test",
            ContactPhone = "+91 11 4000 0000",
            City = "New Delhi",
            State = "Delhi",
            Country = "India",
            TimeZone = "Asia/Kolkata",
            BrandColor = "#2171D3",
            DefaultBranchId = branchId,
            DefaultAssigneeUserId = vermaId,
            IsActive = true,
            Version = 1,
            CreatedAt = createdAt
        });

        modelBuilder.Entity<Branch>().HasData(new Branch
        {
            Id = branchId,
            TenantId = tenantId,
            Name = "Main Branch",
            NormalizedName = "MAIN BRANCH",
            City = "New Delhi",
            IsActive = true,
            Version = 1,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });

        modelBuilder.Entity<AppUser>().HasData(
            new AppUser { Id = rahulId, TenantId = tenantId, BranchId = branchId, FullName = "Rahul Sharma", Email = "rahul@demo-academy.test", PasswordHash = demoPasswordHash, Role = UserRole.Owner, IsActive = true, CreatedAt = createdAt },
            new AppUser { Id = vermaId, TenantId = tenantId, BranchId = branchId, FullName = "S. Verma", Email = "verma@demo-academy.test", PasswordHash = demoPasswordHash, Role = UserRole.Counselor, IsActive = true, CreatedAt = createdAt },
            new AppUser { Id = khannaId, TenantId = tenantId, BranchId = branchId, FullName = "R. Khanna", Email = "khanna@demo-academy.test", PasswordHash = demoPasswordHash, Role = UserRole.ReadOnly, IsActive = true, CreatedAt = createdAt }
        );

        modelBuilder.Entity<Course>().HasData(
            new Course { Id = mbaCourseId, TenantId = tenantId, Name = "MBA Global", NormalizedName = "MBA GLOBAL", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new Course { Id = dataScienceCourseId, TenantId = tenantId, Name = "Data Science", NormalizedName = "DATA SCIENCE", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new Course { Id = uiUxCourseId, TenantId = tenantId, Name = "UI/UX Design", NormalizedName = "UI/UX DESIGN", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new Course { Id = fullStackCourseId, TenantId = tenantId, Name = "Full Stack Dev", NormalizedName = "FULL STACK DEV", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new Course { Id = digitalMarketingCourseId, TenantId = tenantId, Name = "Digital Marketing", NormalizedName = "DIGITAL MARKETING", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt }
        );

        modelBuilder.Entity<LeadStage>().HasData(
            new LeadStage { Id = newInquiryStageId, TenantId = tenantId, Name = "New Inquiry", NormalizedName = "NEW INQUIRY", SortOrder = 10, IsActive = true, IsDefaultStage = true, IsWonStage = false, IsLostStage = false, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new LeadStage { Id = contactedStageId, TenantId = tenantId, Name = "Contacted", NormalizedName = "CONTACTED", SortOrder = 20, IsActive = true, IsDefaultStage = false, IsWonStage = false, IsLostStage = false, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new LeadStage { Id = interestedStageId, TenantId = tenantId, Name = "Interested", NormalizedName = "INTERESTED", SortOrder = 30, IsActive = true, IsDefaultStage = false, IsWonStage = false, IsLostStage = false, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new LeadStage { Id = demoStageId, TenantId = tenantId, Name = "Demo Scheduled", NormalizedName = "DEMO SCHEDULED", SortOrder = 40, IsActive = true, IsDefaultStage = false, IsWonStage = false, IsLostStage = false, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new LeadStage { Id = applicationStageId, TenantId = tenantId, Name = "Application Started", NormalizedName = "APPLICATION STARTED", SortOrder = 50, IsActive = true, IsDefaultStage = false, IsWonStage = false, IsLostStage = false, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new LeadStage { Id = enrolledStageId, TenantId = tenantId, Name = "Enrolled", NormalizedName = "ENROLLED", SortOrder = 60, IsActive = true, IsDefaultStage = false, IsWonStage = true, IsLostStage = false, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new LeadStage { Id = droppedStageId, TenantId = tenantId, Name = "Dropped", NormalizedName = "DROPPED", SortOrder = 70, IsActive = true, IsDefaultStage = false, IsWonStage = false, IsLostStage = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt }
        );

        modelBuilder.Entity<LeadSource>().HasData(
            new LeadSource { Id = googleSourceId, TenantId = tenantId, Name = "Google Ads", NormalizedName = "GOOGLE ADS", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new LeadSource { Id = websiteSourceId, TenantId = tenantId, Name = "Website", NormalizedName = "WEBSITE", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new LeadSource { Id = linkedInSourceId, TenantId = tenantId, Name = "LinkedIn", NormalizedName = "LINKEDIN", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new LeadSource { Id = referralSourceId, TenantId = tenantId, Name = "Referral", NormalizedName = "REFERRAL", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new LeadSource { Id = expoSourceId, TenantId = tenantId, Name = "Offline Expo", NormalizedName = "OFFLINE EXPO", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt }
        );

        modelBuilder.Entity<Lead>().HasData(
            new Lead { Id = lead1Id, TenantId = tenantId, BranchId = branchId, LeadNumber = "LD-1001", StudentName = "Arjun Adhikari", Email = "arjun.a@email.com", Phone = "+91 98765 43210", NormalizedPhone = "919876543210", CourseId = mbaCourseId, LeadStageId = enrolledStageId, LeadSourceId = googleSourceId, AssignedUserId = vermaId, Status = "Enrolled", Priority = "High", Version = 1, CreatedAt = createdAt.AddDays(-18), UpdatedAt = createdAt.AddDays(-18), NextFollowUpAt = createdAt.AddHours(4) },
            new Lead { Id = lead2Id, TenantId = tenantId, BranchId = branchId, LeadNumber = "LD-1002", StudentName = "Priya Sharma", Email = "priya.s@outlook.com", Phone = "+91 91234 56789", NormalizedPhone = "919123456789", CourseId = dataScienceCourseId, LeadStageId = interestedStageId, LeadSourceId = websiteSourceId, AssignedUserId = khannaId, Status = "Interested", Priority = "Medium", Version = 1, CreatedAt = createdAt.AddDays(-6), UpdatedAt = createdAt.AddDays(-6), NextFollowUpAt = createdAt.AddDays(1).AddHours(2) },
            new Lead { Id = lead3Id, TenantId = tenantId, BranchId = branchId, LeadNumber = "LD-1003", StudentName = "Michael Jones", Email = "m.jones@gmail.com", Phone = "+91 99887 76655", NormalizedPhone = "919988776655", CourseId = uiUxCourseId, LeadStageId = demoStageId, LeadSourceId = linkedInSourceId, AssignedUserId = rahulId, Status = "Follow Up", Priority = "High", Version = 1, CreatedAt = createdAt.AddDays(-4), UpdatedAt = createdAt.AddDays(-4), NextFollowUpAt = createdAt.AddHours(3) },
            new Lead { Id = lead4Id, TenantId = tenantId, BranchId = branchId, LeadNumber = "LD-1004", StudentName = "Deepak Reddy", Email = "d.reddy@tcs.com", Phone = "+91 90000 11223", NormalizedPhone = "919000011223", CourseId = fullStackCourseId, LeadStageId = droppedStageId, LeadSourceId = referralSourceId, AssignedUserId = vermaId, Status = "Dropped", Priority = "Low", Version = 1, CreatedAt = createdAt.AddDays(-25), UpdatedAt = createdAt.AddDays(-25), NextFollowUpAt = null },
            new Lead { Id = lead5Id, TenantId = tenantId, BranchId = branchId, LeadNumber = "LD-1005", StudentName = "Kriti Luthra", Email = "k.luthra@gmail.com", Phone = "+91 88776 65544", NormalizedPhone = "918877665544", CourseId = digitalMarketingCourseId, LeadStageId = newInquiryStageId, LeadSourceId = expoSourceId, AssignedUserId = rahulId, Status = "New Lead", Priority = "Medium", Version = 1, CreatedAt = createdAt.AddDays(-1), UpdatedAt = createdAt.AddDays(-1), NextFollowUpAt = createdAt.AddHours(6) }
        );

        modelBuilder.Entity<FollowUp>().HasData(
            new FollowUp { Id = Guid.Parse("80000000-0000-0000-0000-000000000001"), TenantId = tenantId, LeadId = lead1Id, AssignedUserId = rahulId, Type = "Call", Priority = "High", Status = "Scheduled", Version = 1, DueAt = createdAt.AddMinutes(45), CreatedAt = createdAt, UpdatedAt = createdAt },
            new FollowUp { Id = Guid.Parse("80000000-0000-0000-0000-000000000002"), TenantId = tenantId, LeadId = lead2Id, AssignedUserId = vermaId, Type = "WhatsApp", Priority = "Medium", Status = "Scheduled", Version = 1, DueAt = createdAt.AddHours(2), CreatedAt = createdAt, UpdatedAt = createdAt },
            new FollowUp { Id = Guid.Parse("80000000-0000-0000-0000-000000000003"), TenantId = tenantId, LeadId = lead3Id, AssignedUserId = khannaId, Type = "Email", Priority = "Low", Status = "Scheduled", Version = 1, DueAt = createdAt.AddHours(4), CreatedAt = createdAt, UpdatedAt = createdAt }
        );

        modelBuilder.Entity<CommunicationTemplate>().HasData(
            new CommunicationTemplate { Id = template1Id, TenantId = tenantId, Name = "Initial inquiry WhatsApp", NormalizedName = "INITIAL INQUIRY WHATSAPP", Channel = "WhatsApp", Category = "Initial Follow-up", Body = "Hi {{studentName}}, thank you for your interest in {{course}} at {{tenantName}}. Our counsellor will help you with the next steps. Reply here or call us for any questions.", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new CommunicationTemplate { Id = template2Id, TenantId = tenantId, Name = "Demo reminder", NormalizedName = "DEMO REMINDER", Channel = "WhatsApp", Category = "Demo Reminder", Body = "Hi {{studentName}}, this is a reminder for your {{course}} demo. Please keep your questions ready. Your counsellor: {{counsellor}}.", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new CommunicationTemplate { Id = template3Id, TenantId = tenantId, Name = "Application follow-up email", NormalizedName = "APPLICATION FOLLOW-UP EMAIL", Channel = "Email", Category = "Application Follow-up", Body = "Dear {{studentName}}, we are following up on your {{course}} admission application. Current stage: {{stage}}. Please share any pending details so we can proceed.", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new CommunicationTemplate { Id = template4Id, TenantId = tenantId, Name = "Document reminder", NormalizedName = "DOCUMENT REMINDER", Channel = "WhatsApp", Category = "Document Reminder", Body = "Hi {{studentName}}, please share the pending documents for your {{course}} admission process. Lead ID: {{leadNumber}}.", IsActive = true, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt }
        );

        modelBuilder.Entity<DocumentType>().HasData(
            new DocumentType { Id = idProofDocumentTypeId, TenantId = tenantId, Name = "ID Proof", NormalizedName = "ID PROOF", IsRequired = true, IsActive = true, SortOrder = 10, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new DocumentType { Id = addressProofDocumentTypeId, TenantId = tenantId, Name = "Address Proof", NormalizedName = "ADDRESS PROOF", IsRequired = true, IsActive = true, SortOrder = 20, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new DocumentType { Id = academicMarksheetDocumentTypeId, TenantId = tenantId, Name = "Academic Marksheet", NormalizedName = "ACADEMIC MARKSHEET", IsRequired = true, IsActive = true, SortOrder = 30, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new DocumentType { Id = admissionFormDocumentTypeId, TenantId = tenantId, Name = "Admission Form", NormalizedName = "ADMISSION FORM", IsRequired = true, IsActive = true, SortOrder = 40, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt },
            new DocumentType { Id = paymentReceiptDocumentTypeId, TenantId = tenantId, Name = "Payment Receipt", NormalizedName = "PAYMENT RECEIPT", IsRequired = false, IsActive = true, SortOrder = 50, Version = 1, CreatedAt = createdAt, UpdatedAt = createdAt }
        );
    }
}
