using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDataManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lead_stages_TenantId_Name",
                table: "lead_stages");

            migrationBuilder.DropIndex(
                name: "IX_lead_sources_TenantId_Name",
                table: "lead_sources");

            migrationBuilder.DropIndex(
                name: "IX_courses_TenantId_Name",
                table: "courses");

            migrationBuilder.DropIndex(
                name: "IX_branches_TenantId_Name",
                table: "branches");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "lead_stages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultStage",
                table: "lead_stages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "lead_stages",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "lead_stages",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "lead_stages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "lead_sources",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "lead_sources",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "lead_sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "courses",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "courses",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "courses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "branches",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "branches",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "branches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT "Id",
                           upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g')) AS normalized,
                           row_number() OVER (
                               PARTITION BY "TenantId", upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g'))
                               ORDER BY "CreatedAt", "Id") AS duplicate_number
                    FROM branches
                )
                UPDATE branches AS item
                SET "NormalizedName" = CASE
                        WHEN ranked.duplicate_number = 1 THEN left(ranked.normalized, 160)
                        ELSE left(ranked.normalized, 150) || ' #' || left(item."Id"::text, 8)
                    END,
                    "UpdatedAt" = item."CreatedAt",
                    "Version" = 1
                FROM ranked
                WHERE item."Id" = ranked."Id";

                WITH ranked AS (
                    SELECT "Id",
                           upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g')) AS normalized,
                           row_number() OVER (
                               PARTITION BY "TenantId", upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g'))
                               ORDER BY "CreatedAt", "Id") AS duplicate_number
                    FROM courses
                )
                UPDATE courses AS item
                SET "NormalizedName" = CASE
                        WHEN ranked.duplicate_number = 1 THEN left(ranked.normalized, 160)
                        ELSE left(ranked.normalized, 150) || ' #' || left(item."Id"::text, 8)
                    END,
                    "UpdatedAt" = item."CreatedAt",
                    "Version" = 1
                FROM ranked
                WHERE item."Id" = ranked."Id";

                WITH ranked AS (
                    SELECT "Id",
                           upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g')) AS normalized,
                           row_number() OVER (
                               PARTITION BY "TenantId", upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g'))
                               ORDER BY "CreatedAt", "Id") AS duplicate_number
                    FROM lead_sources
                )
                UPDATE lead_sources AS item
                SET "NormalizedName" = CASE
                        WHEN ranked.duplicate_number = 1 THEN left(ranked.normalized, 120)
                        ELSE left(ranked.normalized, 110) || ' #' || left(item."Id"::text, 8)
                    END,
                    "UpdatedAt" = item."CreatedAt",
                    "Version" = 1
                FROM ranked
                WHERE item."Id" = ranked."Id";

                WITH ranked AS (
                    SELECT "Id",
                           upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g')) AS normalized,
                           row_number() OVER (
                               PARTITION BY "TenantId", upper(regexp_replace(btrim("Name"), '\s+', ' ', 'g'))
                               ORDER BY "CreatedAt", "Id") AS duplicate_number
                    FROM lead_stages
                )
                UPDATE lead_stages AS item
                SET "NormalizedName" = CASE
                        WHEN ranked.duplicate_number = 1 THEN left(ranked.normalized, 120)
                        ELSE left(ranked.normalized, 110) || ' #' || left(item."Id"::text, 8)
                    END,
                    "IsActive" = TRUE,
                    "UpdatedAt" = item."CreatedAt",
                    "Version" = 1
                FROM ranked
                WHERE item."Id" = ranked."Id";

                WITH defaults AS (
                    SELECT "Id",
                           row_number() OVER (
                               PARTITION BY "TenantId"
                               ORDER BY CASE WHEN upper(btrim("Name")) = 'NEW INQUIRY' THEN 0 ELSE 1 END,
                                        "SortOrder", "CreatedAt", "Id") AS stage_number
                    FROM lead_stages
                )
                UPDATE lead_stages AS item
                SET "IsDefaultStage" = defaults.stage_number = 1
                FROM defaults
                WHERE item."Id" = defaults."Id";
                """);

            migrationBuilder.UpdateData(
                table: "branches",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                columns: new[] { "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { "MAIN BRANCH", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                columns: new[] { "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { "MBA GLOBAL", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                columns: new[] { "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { "DATA SCIENCE", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                columns: new[] { "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { "UI/UX DESIGN", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                columns: new[] { "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { "FULL STACK DEV", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"),
                columns: new[] { "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { "DIGITAL MARKETING", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"),
                columns: new[] { "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { "GOOGLE ADS", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"),
                columns: new[] { "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { "WEBSITE", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"),
                columns: new[] { "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { "LINKEDIN", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000004"),
                columns: new[] { "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { "REFERRAL", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_sources",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000005"),
                columns: new[] { "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { "OFFLINE EXPO", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000001"),
                columns: new[] { "IsActive", "IsDefaultStage", "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { true, true, "NEW INQUIRY", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000002"),
                columns: new[] { "IsActive", "IsDefaultStage", "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { true, false, "CONTACTED", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000003"),
                columns: new[] { "IsActive", "IsDefaultStage", "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { true, false, "INTERESTED", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000004"),
                columns: new[] { "IsActive", "IsDefaultStage", "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { true, false, "DEMO SCHEDULED", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000005"),
                columns: new[] { "IsActive", "IsDefaultStage", "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { true, false, "APPLICATION STARTED", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000006"),
                columns: new[] { "IsActive", "IsDefaultStage", "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { true, false, "ENROLLED", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "lead_stages",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000007"),
                columns: new[] { "IsActive", "IsDefaultStage", "NormalizedName", "UpdatedAt", "Version" },
                values: new object[] { true, false, "DROPPED", new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.CreateIndex(
                name: "IX_lead_stages_TenantId_IsDefaultStage",
                table: "lead_stages",
                columns: new[] { "TenantId", "IsDefaultStage" },
                unique: true,
                filter: "\"IsDefaultStage\" = TRUE AND \"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_lead_stages_TenantId_NormalizedName",
                table: "lead_stages",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lead_sources_TenantId_NormalizedName",
                table: "lead_sources",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courses_TenantId_NormalizedName",
                table: "courses",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_NormalizedName",
                table: "branches",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lead_stages_TenantId_IsDefaultStage",
                table: "lead_stages");

            migrationBuilder.DropIndex(
                name: "IX_lead_stages_TenantId_NormalizedName",
                table: "lead_stages");

            migrationBuilder.DropIndex(
                name: "IX_lead_sources_TenantId_NormalizedName",
                table: "lead_sources");

            migrationBuilder.DropIndex(
                name: "IX_courses_TenantId_NormalizedName",
                table: "courses");

            migrationBuilder.DropIndex(
                name: "IX_branches_TenantId_NormalizedName",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "lead_stages");

            migrationBuilder.DropColumn(
                name: "IsDefaultStage",
                table: "lead_stages");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "lead_stages");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "lead_stages");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "lead_stages");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "lead_sources");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "lead_sources");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "lead_sources");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "branches");

            migrationBuilder.CreateIndex(
                name: "IX_lead_stages_TenantId_Name",
                table: "lead_stages",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lead_sources_TenantId_Name",
                table: "lead_sources",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courses_TenantId_Name",
                table: "courses",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_Name",
                table: "branches",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }
    }
}
