using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowUpCompletionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompletionNextFollowUpId",
                table: "follow_ups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionNotes",
                table: "follow_ups",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionOutcome",
                table: "follow_ups",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000001"),
                columns: new[] { "CompletionNextFollowUpId", "CompletionNotes", "CompletionOutcome" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000002"),
                columns: new[] { "CompletionNextFollowUpId", "CompletionNotes", "CompletionOutcome" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "follow_ups",
                keyColumn: "Id",
                keyValue: new Guid("80000000-0000-0000-0000-000000000003"),
                columns: new[] { "CompletionNextFollowUpId", "CompletionNotes", "CompletionOutcome" },
                values: new object[] { null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionNextFollowUpId",
                table: "follow_ups");

            migrationBuilder.DropColumn(
                name: "CompletionNotes",
                table: "follow_ups");

            migrationBuilder.DropColumn(
                name: "CompletionOutcome",
                table: "follow_ups");
        }
    }
}
