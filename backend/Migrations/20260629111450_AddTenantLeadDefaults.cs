using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLeadDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAssigneeUserId",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultBranchId",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "tenants",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "DefaultAssigneeUserId", "DefaultBranchId" },
                values: new object[] { new Guid("30000000-0000-0000-0000-000000000002"), new Guid("20000000-0000-0000-0000-000000000001") });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_DefaultAssigneeUserId",
                table: "tenants",
                column: "DefaultAssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_DefaultBranchId",
                table: "tenants",
                column: "DefaultBranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_tenants_branches_DefaultBranchId",
                table: "tenants",
                column: "DefaultBranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tenants_users_DefaultAssigneeUserId",
                table: "tenants",
                column: "DefaultAssigneeUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenants_branches_DefaultBranchId",
                table: "tenants");

            migrationBuilder.DropForeignKey(
                name: "FK_tenants_users_DefaultAssigneeUserId",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_DefaultAssigneeUserId",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_DefaultBranchId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "DefaultAssigneeUserId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "DefaultBranchId",
                table: "tenants");
        }
    }
}
