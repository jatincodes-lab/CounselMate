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
        const string demoPasswordHash = "v1.100000.Mt8GC3coU3xegrVi+C2aAw==.i10uchOOE1k5pG2zdL/PW+FxQ7wZ9yM+MW/hwgowbPM=";

        modelBuilder.Entity<Tenant>().HasData(new Tenant
        {
            Id = tenantId,
            Name = "Demo Academy",
            Slug = "demo-academy",
            IsActive = true,
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
    }
}
