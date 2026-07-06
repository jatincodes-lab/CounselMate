using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "enrollments",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE enrollments
                SET ""UpdatedAt"" = ""CreatedAt""
                WHERE ""UpdatedAt"" IS NULL;
            ");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "enrollments",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "enrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_UpdatedByUserId",
                table: "enrollments",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_enrollments_users_UpdatedByUserId",
                table: "enrollments",
                column: "UpdatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_enrollments_users_UpdatedByUserId",
                table: "enrollments");

            migrationBuilder.DropIndex(
                name: "IX_enrollments_UpdatedByUserId",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "enrollments");
        }
    }
}
