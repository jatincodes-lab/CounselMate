using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenantSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branches_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_courses_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lead_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lead_sources_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lead_stages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsWonStage = table.Column<bool>(type: "boolean", nullable: false),
                    IsLostStage = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lead_stages_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_users_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeadNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StudentName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    GuardianName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Email = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextFollowUpAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leads_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_leads_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_lead_sources_LeadSourceId",
                        column: x => x.LeadSourceId,
                        principalTable: "lead_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_lead_stages_LeadStageId",
                        column: x => x.LeadStageId,
                        principalTable: "lead_stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leads_users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activities_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activities_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activities_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "follow_ups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_follow_ups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_follow_ups_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_follow_ups_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_follow_ups_users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "tenants",
                columns: new[] { "Id", "CreatedAt", "IsActive", "Name", "Slug" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Demo Academy", "demo-academy" });

            migrationBuilder.InsertData(
                table: "branches",
                columns: new[] { "Id", "City", "CreatedAt", "IsActive", "Name", "TenantId" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000001"), "New Delhi", new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Main Branch", new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.InsertData(
                table: "courses",
                columns: new[] { "Id", "CreatedAt", "IsActive", "Name", "TenantId" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "MBA Global", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("40000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Data Science", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("40000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "UI/UX Design", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("40000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Full Stack Dev", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("40000000-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Digital Marketing", new Guid("10000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.InsertData(
                table: "lead_sources",
                columns: new[] { "Id", "CreatedAt", "IsActive", "Name", "TenantId" },
                values: new object[,]
                {
                    { new Guid("60000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Google Ads", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("60000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Website", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("60000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "LinkedIn", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("60000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Referral", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("60000000-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "Offline Expo", new Guid("10000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.InsertData(
                table: "lead_stages",
                columns: new[] { "Id", "CreatedAt", "IsLostStage", "IsWonStage", "Name", "SortOrder", "TenantId" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, false, "New Inquiry", 10, new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("50000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, false, "Contacted", 20, new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("50000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, false, "Interested", 30, new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("50000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, false, "Demo Scheduled", 40, new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("50000000-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, false, "Application Started", 50, new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("50000000-0000-0000-0000-000000000006"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, true, "Enrolled", 60, new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("50000000-0000-0000-0000-000000000007"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, false, "Dropped", 70, new Guid("10000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "BranchId", "CreatedAt", "Email", "FullName", "IsActive", "Role", "TenantId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "rahul@demo-academy.test", "Rahul Sharma", true, "Counselor", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000002"), new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "verma@demo-academy.test", "S. Verma", true, "Counselor", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000003"), new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "khanna@demo-academy.test", "R. Khanna", true, "Counselor", new Guid("10000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.InsertData(
                table: "leads",
                columns: new[] { "Id", "AssignedUserId", "BranchId", "City", "CourseId", "CreatedAt", "Email", "GuardianName", "LeadNumber", "LeadSourceId", "LeadStageId", "NextFollowUpAt", "Phone", "Priority", "Status", "StudentName", "TenantId" },
                values: new object[,]
                {
                    { new Guid("70000000-0000-0000-0000-000000000001"), new Guid("30000000-0000-0000-0000-000000000002"), new Guid("20000000-0000-0000-0000-000000000001"), null, new Guid("40000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "arjun.a@email.com", null, "LD-1001", new Guid("60000000-0000-0000-0000-000000000001"), new Guid("50000000-0000-0000-0000-000000000006"), new DateTimeOffset(new DateTime(2026, 6, 25, 4, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "+91 98765 43210", "High", "Enrolled", "Arjun Adhikari", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("70000000-0000-0000-0000-000000000002"), new Guid("30000000-0000-0000-0000-000000000003"), new Guid("20000000-0000-0000-0000-000000000001"), null, new Guid("40000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 6, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "priya.s@outlook.com", null, "LD-1002", new Guid("60000000-0000-0000-0000-000000000002"), new Guid("50000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 6, 26, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "+91 91234 56789", "Medium", "Interested", "Priya Sharma", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("70000000-0000-0000-0000-000000000003"), new Guid("30000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000001"), null, new Guid("40000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "m.jones@gmail.com", null, "LD-1003", new Guid("60000000-0000-0000-0000-000000000003"), new Guid("50000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 6, 25, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "+91 99887 76655", "High", "Follow Up", "Michael Jones", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("70000000-0000-0000-0000-000000000004"), new Guid("30000000-0000-0000-0000-000000000002"), new Guid("20000000-0000-0000-0000-000000000001"), null, new Guid("40000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "d.reddy@tcs.com", null, "LD-1004", new Guid("60000000-0000-0000-0000-000000000004"), new Guid("50000000-0000-0000-0000-000000000007"), null, "+91 90000 11223", "Low", "Dropped", "Deepak Reddy", new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("70000000-0000-0000-0000-000000000005"), new Guid("30000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000001"), null, new Guid("40000000-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "k.luthra@gmail.com", null, "LD-1005", new Guid("60000000-0000-0000-0000-000000000005"), new Guid("50000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 25, 6, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "+91 88776 65544", "Medium", "New Lead", "Kriti Luthra", new Guid("10000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.InsertData(
                table: "follow_ups",
                columns: new[] { "Id", "AssignedUserId", "CreatedAt", "DueAt", "LeadId", "Priority", "Status", "TenantId", "Type" },
                values: new object[,]
                {
                    { new Guid("80000000-0000-0000-0000-000000000001"), new Guid("30000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 45, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("70000000-0000-0000-0000-000000000001"), "High", "Scheduled", new Guid("10000000-0000-0000-0000-000000000001"), "Call" },
                    { new Guid("80000000-0000-0000-0000-000000000002"), new Guid("30000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 6, 25, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("70000000-0000-0000-0000-000000000002"), "Medium", "Scheduled", new Guid("10000000-0000-0000-0000-000000000001"), "WhatsApp" },
                    { new Guid("80000000-0000-0000-0000-000000000003"), new Guid("30000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 6, 25, 4, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("70000000-0000-0000-0000-000000000003"), "Low", "Scheduled", new Guid("10000000-0000-0000-0000-000000000001"), "Email" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_activities_CreatedByUserId",
                table: "activities",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_activities_LeadId",
                table: "activities",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_activities_TenantId_CreatedAt",
                table: "activities",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_Name",
                table: "branches",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courses_TenantId_Name",
                table: "courses",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_follow_ups_AssignedUserId",
                table: "follow_ups",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_follow_ups_LeadId",
                table: "follow_ups",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_follow_ups_TenantId_DueAt",
                table: "follow_ups",
                columns: new[] { "TenantId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_lead_sources_TenantId_Name",
                table: "lead_sources",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lead_stages_TenantId_Name",
                table: "lead_stages",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lead_stages_TenantId_SortOrder",
                table: "lead_stages",
                columns: new[] { "TenantId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leads_AssignedUserId",
                table: "leads",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_leads_BranchId",
                table: "leads",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_leads_CourseId",
                table: "leads",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_leads_LeadSourceId",
                table: "leads",
                column: "LeadSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_leads_LeadStageId",
                table: "leads",
                column: "LeadStageId");

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId",
                table: "leads",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId_CreatedAt",
                table: "leads",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId_LeadNumber",
                table: "leads",
                columns: new[] { "TenantId", "LeadNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId_Phone",
                table: "leads",
                columns: new[] { "TenantId", "Phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_BranchId",
                table: "users",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId_Email",
                table: "users",
                columns: new[] { "TenantId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activities");

            migrationBuilder.DropTable(
                name: "follow_ups");

            migrationBuilder.DropTable(
                name: "leads");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropTable(
                name: "lead_sources");

            migrationBuilder.DropTable(
                name: "lead_stages");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
