using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantProfileSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandColor",
                table: "tenants",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#2171D3");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "tenants",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "tenants",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "tenants",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "tenants",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "India");

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "tenants",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Asia/Kolkata");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "tenants",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "tenants",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "AddressLine1", "AddressLine2", "BrandColor", "City", "ContactEmail", "ContactPhone", "Country", "LogoUrl", "PostalCode", "State", "TimeZone", "UpdatedAt", "Version", "WebsiteUrl" },
                values: new object[] { null, null, "#2171D3", "New Delhi", "admissions@demo-academy.test", "+91 11 4000 0000", "India", null, null, "Delhi", "Asia/Kolkata", null, 1, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "BrandColor",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "City",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "State",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "tenants");
        }
    }
}
