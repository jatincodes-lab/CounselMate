using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowUpLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "follow_ups",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "follow_ups",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "follow_ups",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "follow_ups",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000001"),
                columns: new[] { "CancelledAt", "CompletedAt", "UpdatedAt", "Version" },
                values: new object[] { null, null, new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000002"),
                columns: new[] { "CancelledAt", "CompletedAt", "UpdatedAt", "Version" },
                values: new object[] { null, null, new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000003"),
                columns: new[] { "CancelledAt", "CompletedAt", "UpdatedAt", "Version" },
                values: new object[] { null, null, new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.Sql("""
                UPDATE follow_ups
                SET "UpdatedAt" = "CreatedAt",
                    "Version" = 1
                WHERE "UpdatedAt" <= TIMESTAMP '1900-01-01'
                   OR "Version" < 1;

                ALTER TABLE follow_ups ALTER COLUMN "UpdatedAt" DROP DEFAULT;
                ALTER TABLE follow_ups ALTER COLUMN "Version" SET DEFAULT 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_follow_ups_TenantId_Status_DueAt",
                table: "follow_ups",
                columns: new[] { "TenantId", "Status", "DueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_follow_ups_TenantId_Status_DueAt",
                table: "follow_ups");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "follow_ups");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "follow_ups");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "follow_ups");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "follow_ups");
        }
    }
}
