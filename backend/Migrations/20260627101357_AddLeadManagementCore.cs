using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadManagementCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "leads",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "leads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "leads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "leads",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "leads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "leads",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000001"),
                columns: new[] { "ArchivedAt", "ArchivedByUserId", "CreatedByUserId", "UpdatedAt", "UpdatedByUserId", "Version" },
                values: new object[] { null, null, null, new DateTime(1, 1, 1, 5, 30, 0, 0, DateTimeKind.Unspecified), null, 1 });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000002"),
                columns: new[] { "ArchivedAt", "ArchivedByUserId", "CreatedByUserId", "UpdatedAt", "UpdatedByUserId", "Version" },
                values: new object[] { null, null, null, new DateTime(1, 1, 1, 5, 30, 0, 0, DateTimeKind.Unspecified), null, 1 });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000003"),
                columns: new[] { "ArchivedAt", "ArchivedByUserId", "CreatedByUserId", "UpdatedAt", "UpdatedByUserId", "Version" },
                values: new object[] { null, null, null, new DateTime(1, 1, 1, 5, 30, 0, 0, DateTimeKind.Unspecified), null, 1 });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000004"),
                columns: new[] { "ArchivedAt", "ArchivedByUserId", "CreatedByUserId", "UpdatedAt", "UpdatedByUserId", "Version" },
                values: new object[] { null, null, null, new DateTime(1, 1, 1, 5, 30, 0, 0, DateTimeKind.Unspecified), null, 1 });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000005"),
                columns: new[] { "ArchivedAt", "ArchivedByUserId", "CreatedByUserId", "UpdatedAt", "UpdatedByUserId", "Version" },
                values: new object[] { null, null, null, new DateTime(1, 1, 1, 5, 30, 0, 0, DateTimeKind.Unspecified), null, 1 });

            migrationBuilder.Sql("""
                UPDATE leads
                SET "UpdatedAt" = "CreatedAt",
                    "Version" = 1
                WHERE "UpdatedAt" <= TIMESTAMP '1900-01-01'
                   OR "Version" < 1;

                ALTER TABLE leads ALTER COLUMN "UpdatedAt" DROP DEFAULT;
                ALTER TABLE leads ALTER COLUMN "Version" SET DEFAULT 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_leads_ArchivedByUserId",
                table: "leads",
                column: "ArchivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_leads_CreatedByUserId",
                table: "leads",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId_ArchivedAt",
                table: "leads",
                columns: new[] { "TenantId", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId_AssignedUserId",
                table: "leads",
                columns: new[] { "TenantId", "AssignedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId_BranchId",
                table: "leads",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId_LeadStageId",
                table: "leads",
                columns: new[] { "TenantId", "LeadStageId" });

            migrationBuilder.CreateIndex(
                name: "IX_leads_UpdatedByUserId",
                table: "leads",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_leads_users_ArchivedByUserId",
                table: "leads",
                column: "ArchivedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_leads_users_CreatedByUserId",
                table: "leads",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_leads_users_UpdatedByUserId",
                table: "leads",
                column: "UpdatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_leads_users_ArchivedByUserId",
                table: "leads");

            migrationBuilder.DropForeignKey(
                name: "FK_leads_users_CreatedByUserId",
                table: "leads");

            migrationBuilder.DropForeignKey(
                name: "FK_leads_users_UpdatedByUserId",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "IX_leads_ArchivedByUserId",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "IX_leads_CreatedByUserId",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "IX_leads_TenantId_ArchivedAt",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "IX_leads_TenantId_AssignedUserId",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "IX_leads_TenantId_BranchId",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "IX_leads_TenantId_LeadStageId",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "IX_leads_UpdatedByUserId",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "leads");
        }
    }
}
