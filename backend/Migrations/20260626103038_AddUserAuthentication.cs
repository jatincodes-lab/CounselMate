using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLoginAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "users",
                type: "character varying(220)",
                maxLength: 220,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                columns: new[] { "FailedLoginAttempts", "LastLoginAt", "PasswordHash", "Role" },
                values: new object[] { 0, null, "v1.100000.Mt8GC3coU3xegrVi+C2aAw==.i10uchOOE1k5pG2zdL/PW+FxQ7wZ9yM+MW/hwgowbPM=", "Admin" });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                columns: new[] { "FailedLoginAttempts", "LastLoginAt", "PasswordHash" },
                values: new object[] { 0, null, "v1.100000.Mt8GC3coU3xegrVi+C2aAw==.i10uchOOE1k5pG2zdL/PW+FxQ7wZ9yM+MW/hwgowbPM=" });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                columns: new[] { "FailedLoginAttempts", "LastLoginAt", "PasswordHash", "Role" },
                values: new object[] { 0, null, "v1.100000.Mt8GC3coU3xegrVi+C2aAw==.i10uchOOE1k5pG2zdL/PW+FxQ7wZ9yM+MW/hwgowbPM=", "ReadOnly" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "users");

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "Role",
                value: "Counselor");

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                column: "Role",
                value: "Counselor");
        }
    }
}
