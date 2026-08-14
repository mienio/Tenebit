using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asset_evidence",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    OffboardingItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetAuditItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    UploadedVia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_evidence_assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenebit",
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asset_evidence_assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "tenebit",
                        principalTable: "assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_asset_evidence_AssetId",
                schema: "tenebit",
                table: "asset_evidence",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_evidence_AssignmentId",
                schema: "tenebit",
                table: "asset_evidence",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_asset_evidence_OrganizationId_AssetId_Phase",
                schema: "tenebit",
                table: "asset_evidence",
                columns: new[] { "OrganizationId", "AssetId", "Phase" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_evidence",
                schema: "tenebit");
        }
    }
}
