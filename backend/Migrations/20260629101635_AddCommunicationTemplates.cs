using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunicationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "communication_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_communication_templates_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_communication_templates_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_communication_templates_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "communication_templates",
                columns: new[] { "Id", "Body", "Category", "Channel", "CreatedAt", "CreatedByUserId", "IsActive", "Name", "NormalizedName", "TenantId", "UpdatedAt", "UpdatedByUserId", "Version" },
                values: new object[,]
                {
                    { new Guid("90000000-0000-0000-0000-000000000001"), "Hi {{studentName}}, thank you for your interest in {{course}} at {{tenantName}}. Our counsellor will help you with the next steps. Reply here or call us for any questions.", "Initial Follow-up", "WhatsApp", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), null, true, "Initial inquiry WhatsApp", "INITIAL INQUIRY WHATSAPP", new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), null, 1 },
                    { new Guid("90000000-0000-0000-0000-000000000002"), "Hi {{studentName}}, this is a reminder for your {{course}} demo. Please keep your questions ready. Your counsellor: {{counsellor}}.", "Demo Reminder", "WhatsApp", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), null, true, "Demo reminder", "DEMO REMINDER", new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), null, 1 },
                    { new Guid("90000000-0000-0000-0000-000000000003"), "Dear {{studentName}}, we are following up on your {{course}} admission application. Current stage: {{stage}}. Please share any pending details so we can proceed.", "Application Follow-up", "Email", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), null, true, "Application follow-up email", "APPLICATION FOLLOW-UP EMAIL", new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), null, 1 },
                    { new Guid("90000000-0000-0000-0000-000000000004"), "Hi {{studentName}}, please share the pending documents for your {{course}} admission process. Lead ID: {{leadNumber}}.", "Document Reminder", "WhatsApp", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), null, true, "Document reminder", "DOCUMENT REMINDER", new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), null, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_communication_templates_CreatedByUserId",
                table: "communication_templates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_communication_templates_TenantId_IsActive_Channel",
                table: "communication_templates",
                columns: new[] { "TenantId", "IsActive", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_communication_templates_TenantId_NormalizedName",
                table: "communication_templates",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_communication_templates_UpdatedByUserId",
                table: "communication_templates",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "communication_templates");
        }
    }
}
