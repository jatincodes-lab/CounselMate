using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntelligenceReason",
                table: "leads",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntelligenceScore",
                table: "leads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IntelligenceTemperature",
                table: "leads",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "IntelligenceUpdatedAt",
                table: "leads",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lead_distribution_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PriorityOrder = table.Column<int>(type: "integer", nullable: false),
                    Strategy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeadSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetUserIds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LastAssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_distribution_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lead_distribution_rules_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_lead_distribution_rules_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_lead_distribution_rules_lead_sources_LeadSourceId",
                        column: x => x.LeadSourceId,
                        principalTable: "lead_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_lead_distribution_rules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lead_distribution_rules_users_LastAssignedUserId",
                        column: x => x.LastAssignedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "lead_intelligence_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoringEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DistributionEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultDistributionStrategy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PriorityWeight = table.Column<int>(type: "integer", nullable: false),
                    SourceWeight = table.Column<int>(type: "integer", nullable: false),
                    ResponseWeight = table.Column<int>(type: "integer", nullable: false),
                    EngagementWeight = table.Column<int>(type: "integer", nullable: false),
                    FreshnessWeight = table.Column<int>(type: "integer", nullable: false),
                    ProfileWeight = table.Column<int>(type: "integer", nullable: false),
                    HotThreshold = table.Column<int>(type: "integer", nullable: false),
                    WarmThreshold = table.Column<int>(type: "integer", nullable: false),
                    MaxActiveLeadsPerUser = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_intelligence_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lead_intelligence_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lead_intelligence_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PreviousScore = table.Column<int>(type: "integer", nullable: true),
                    NewScore = table.Column<int>(type: "integer", nullable: true),
                    PreviousTemperature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NewTemperature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PreviousAssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewAssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DistributionRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_intelligence_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lead_intelligence_events_lead_distribution_rules_Distributi~",
                        column: x => x.DistributionRuleId,
                        principalTable: "lead_distribution_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_lead_intelligence_events_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lead_intelligence_events_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lead_intelligence_events_users_NewAssignedUserId",
                        column: x => x.NewAssignedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_lead_intelligence_events_users_PreviousAssignedUserId",
                        column: x => x.PreviousAssignedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000001"),
                columns: new[] { "IntelligenceReason", "IntelligenceScore", "IntelligenceTemperature", "IntelligenceUpdatedAt" },
                values: new object[] { null, 0, "Cold", null });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000002"),
                columns: new[] { "IntelligenceReason", "IntelligenceScore", "IntelligenceTemperature", "IntelligenceUpdatedAt" },
                values: new object[] { null, 0, "Cold", null });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000003"),
                columns: new[] { "IntelligenceReason", "IntelligenceScore", "IntelligenceTemperature", "IntelligenceUpdatedAt" },
                values: new object[] { null, 0, "Cold", null });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000004"),
                columns: new[] { "IntelligenceReason", "IntelligenceScore", "IntelligenceTemperature", "IntelligenceUpdatedAt" },
                values: new object[] { null, 0, "Cold", null });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000005"),
                columns: new[] { "IntelligenceReason", "IntelligenceScore", "IntelligenceTemperature", "IntelligenceUpdatedAt" },
                values: new object[] { null, 0, "Cold", null });

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId_IntelligenceScore",
                table: "leads",
                columns: new[] { "TenantId", "IntelligenceScore" });

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId_IntelligenceTemperature",
                table: "leads",
                columns: new[] { "TenantId", "IntelligenceTemperature" });

            migrationBuilder.CreateIndex(
                name: "IX_lead_distribution_rules_BranchId",
                table: "lead_distribution_rules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_distribution_rules_CourseId",
                table: "lead_distribution_rules",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_distribution_rules_LastAssignedUserId",
                table: "lead_distribution_rules",
                column: "LastAssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_distribution_rules_LeadSourceId",
                table: "lead_distribution_rules",
                column: "LeadSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_distribution_rules_TenantId_IsActive_PriorityOrder",
                table: "lead_distribution_rules",
                columns: new[] { "TenantId", "IsActive", "PriorityOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_lead_intelligence_events_DistributionRuleId",
                table: "lead_intelligence_events",
                column: "DistributionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_intelligence_events_LeadId",
                table: "lead_intelligence_events",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_intelligence_events_NewAssignedUserId",
                table: "lead_intelligence_events",
                column: "NewAssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_intelligence_events_PreviousAssignedUserId",
                table: "lead_intelligence_events",
                column: "PreviousAssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_intelligence_events_TenantId_EventType_CreatedAt",
                table: "lead_intelligence_events",
                columns: new[] { "TenantId", "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_lead_intelligence_events_TenantId_LeadId_CreatedAt",
                table: "lead_intelligence_events",
                columns: new[] { "TenantId", "LeadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_lead_intelligence_settings_TenantId",
                table: "lead_intelligence_settings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lead_intelligence_events");

            migrationBuilder.DropTable(
                name: "lead_intelligence_settings");

            migrationBuilder.DropTable(
                name: "lead_distribution_rules");

            migrationBuilder.DropIndex(
                name: "IX_leads_TenantId_IntelligenceScore",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "IX_leads_TenantId_IntelligenceTemperature",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "IntelligenceReason",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "IntelligenceScore",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "IntelligenceTemperature",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "IntelligenceUpdatedAt",
                table: "leads");
        }
    }
}
