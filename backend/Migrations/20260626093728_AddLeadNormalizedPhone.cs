using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadNormalizedPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_leads_TenantId_Phone",
                table: "leads");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "leads",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000001"),
                column: "NormalizedPhone",
                value: "919876543210");

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000002"),
                column: "NormalizedPhone",
                value: "919123456789");

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000003"),
                column: "NormalizedPhone",
                value: "919988776655");

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000004"),
                column: "NormalizedPhone",
                value: "919000011223");

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000005"),
                column: "NormalizedPhone",
                value: "918877665544");

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId_NormalizedPhone",
                table: "leads",
                columns: new[] { "TenantId", "NormalizedPhone" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_leads_TenantId_NormalizedPhone",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "leads");

            migrationBuilder.CreateIndex(
                name: "IX_leads_TenantId_Phone",
                table: "leads",
                columns: new[] { "TenantId", "Phone" },
                unique: true);
        }
    }
}
