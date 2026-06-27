using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class StoreIndianStandardTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ConvertToIndianTimestamp(migrationBuilder, "users", "LastLoginAt");
            ConvertToIndianTimestamp(migrationBuilder, "users", "CreatedAt");
            ConvertToIndianTimestamp(migrationBuilder, "tenants", "CreatedAt");
            ConvertToIndianTimestamp(migrationBuilder, "leads", "NextFollowUpAt");
            ConvertToIndianTimestamp(migrationBuilder, "leads", "CreatedAt");
            ConvertToIndianTimestamp(migrationBuilder, "lead_stages", "CreatedAt");
            ConvertToIndianTimestamp(migrationBuilder, "lead_sources", "CreatedAt");
            ConvertToIndianTimestamp(migrationBuilder, "follow_ups", "DueAt");
            ConvertToIndianTimestamp(migrationBuilder, "follow_ups", "CreatedAt");
            ConvertToIndianTimestamp(migrationBuilder, "courses", "CreatedAt");
            ConvertToIndianTimestamp(migrationBuilder, "branches", "CreatedAt");
            ConvertToIndianTimestamp(migrationBuilder, "activities", "CreatedAt");

            migrationBuilder.UpdateData(
                table: "branches",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "DueAt" },
                values: new object[] { new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 6, 25, 6, 15, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "DueAt" },
                values: new object[] { new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 6, 25, 7, 30, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "DueAt" },
                values: new object[] { new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 6, 25, 9, 30, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "NextFollowUpAt" },
                values: new object[] { new DateTime(2026, 6, 7, 5, 30, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 6, 25, 9, 30, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "NextFollowUpAt" },
                values: new object[] { new DateTime(2026, 6, 19, 5, 30, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 6, 26, 7, 30, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "NextFollowUpAt" },
                values: new object[] { new DateTime(2026, 6, 21, 5, 30, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 6, 25, 8, 30, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "NextFollowUpAt" },
                values: new object[] { new DateTime(2026, 5, 31, 5, 30, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedAt", "NextFollowUpAt" },
                values: new object[] { new DateTime(2026, 6, 24, 5, 30, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 6, 25, 11, 30, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "tenants",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "LastLoginAt" },
                values: new object[] { new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "LastLoginAt" },
                values: new object[] { new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "LastLoginAt" },
                values: new object[] { new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ConvertFromIndianTimestamp(migrationBuilder, "users", "LastLoginAt");
            ConvertFromIndianTimestamp(migrationBuilder, "users", "CreatedAt");
            ConvertFromIndianTimestamp(migrationBuilder, "tenants", "CreatedAt");
            ConvertFromIndianTimestamp(migrationBuilder, "leads", "NextFollowUpAt");
            ConvertFromIndianTimestamp(migrationBuilder, "leads", "CreatedAt");
            ConvertFromIndianTimestamp(migrationBuilder, "lead_stages", "CreatedAt");
            ConvertFromIndianTimestamp(migrationBuilder, "lead_sources", "CreatedAt");
            ConvertFromIndianTimestamp(migrationBuilder, "follow_ups", "DueAt");
            ConvertFromIndianTimestamp(migrationBuilder, "follow_ups", "CreatedAt");
            ConvertFromIndianTimestamp(migrationBuilder, "courses", "CreatedAt");
            ConvertFromIndianTimestamp(migrationBuilder, "branches", "CreatedAt");
            ConvertFromIndianTimestamp(migrationBuilder, "activities", "CreatedAt");

            migrationBuilder.UpdateData(
                table: "branches",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "DueAt" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 6, 25, 0, 45, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "DueAt" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 6, 25, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "DueAt" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 6, 25, 4, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "NextFollowUpAt" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 6, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 6, 25, 4, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "NextFollowUpAt" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 6, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 6, 26, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "NextFollowUpAt" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 6, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 6, 25, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "NextFollowUpAt" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 5, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.UpdateData(
                table: "leads",
                keyColumn: "Id",
                keyValue: new Guid("70000000-0000-0000-0000-000000000005"),
                columns: new[] { "CreatedAt", "NextFollowUpAt" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 6, 25, 6, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                table: "tenants",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "LastLoginAt" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "LastLoginAt" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "LastLoginAt" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 6, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });
        }

        private static void ConvertToIndianTimestamp(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE "{table}"
                ALTER COLUMN "{column}"
                TYPE timestamp without time zone
                USING ("{column}" AT TIME ZONE 'Asia/Kolkata');
                """);
        }

        private static void ConvertFromIndianTimestamp(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE "{table}"
                ALTER COLUMN "{column}"
                TYPE timestamp with time zone
                USING ("{column}" AT TIME ZONE 'Asia/Kolkata');
                """);
        }
    }
}
