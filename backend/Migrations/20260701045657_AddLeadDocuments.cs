using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EducationCrm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_types_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lead_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CloudinaryAssetId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CloudinaryPublicId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CloudinaryResourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CloudinaryDeliveryType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CloudinarySecureUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lead_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lead_documents_document_types_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "document_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lead_documents_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lead_documents_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lead_documents_users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_lead_documents_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_lead_documents_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "document_types",
                columns: new[] { "Id", "CreatedAt", "IsActive", "IsRequired", "Name", "NormalizedName", "SortOrder", "TenantId", "UpdatedAt", "Version" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000001"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), true, true, "ID Proof", "ID PROOF", 10, new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 },
                    { new Guid("a0000000-0000-0000-0000-000000000002"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), true, true, "Address Proof", "ADDRESS PROOF", 20, new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 },
                    { new Guid("a0000000-0000-0000-0000-000000000003"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), true, true, "Academic Marksheet", "ACADEMIC MARKSHEET", 30, new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 },
                    { new Guid("a0000000-0000-0000-0000-000000000004"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), true, true, "Admission Form", "ADMISSION FORM", 40, new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 },
                    { new Guid("a0000000-0000-0000-0000-000000000005"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), true, false, "Payment Receipt", "PAYMENT RECEIPT", 50, new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 6, 25, 5, 30, 0, 0, DateTimeKind.Unspecified), 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_types_TenantId_NormalizedName",
                table: "document_types",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_types_TenantId_SortOrder",
                table: "document_types",
                columns: new[] { "TenantId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lead_documents_DocumentTypeId",
                table: "lead_documents",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_documents_LeadId",
                table: "lead_documents",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_documents_ReviewedByUserId",
                table: "lead_documents",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_documents_TenantId_CloudinaryPublicId",
                table: "lead_documents",
                columns: new[] { "TenantId", "CloudinaryPublicId" });

            migrationBuilder.CreateIndex(
                name: "IX_lead_documents_TenantId_LeadId_DocumentTypeId",
                table: "lead_documents",
                columns: new[] { "TenantId", "LeadId", "DocumentTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lead_documents_TenantId_Status",
                table: "lead_documents",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_lead_documents_UpdatedByUserId",
                table: "lead_documents",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_lead_documents_UploadedByUserId",
                table: "lead_documents",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lead_documents");

            migrationBuilder.DropTable(
                name: "document_types");
        }
    }
}
