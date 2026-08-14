using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnPolicyAndAssetInspections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoOnIssue",
                schema: "tenebit",
                table: "asset_categories",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Disabled");

            migrationBuilder.AddColumn<string>(
                name: "PhotoOnReturn",
                schema: "tenebit",
                table: "asset_categories",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Disabled");

            migrationBuilder.AddColumn<string>(
                name: "PostReturnDisposition",
                schema: "tenebit",
                table: "asset_categories",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Reuse");

            migrationBuilder.AddColumn<string>(
                name: "ReturnChecklistTemplate",
                schema: "tenebit",
                table: "asset_categories",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnHandlingMode",
                schema: "tenebit",
                table: "asset_categories",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "DirectToStock");

            migrationBuilder.CreateTable(
                name: "asset_inspections",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    SerialNumberMatched = table.Column<bool>(type: "boolean", nullable: true),
                    AccessoriesComplete = table.Column<bool>(type: "boolean", nullable: true),
                    DataWiped = table.Column<bool>(type: "boolean", nullable: true),
                    FunctionalTestPassed = table.Column<bool>(type: "boolean", nullable: true),
                    DamageAssessmentNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_inspections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_asset_inspections_OrganizationId_AssetId_Outcome",
                schema: "tenebit",
                table: "asset_inspections",
                columns: new[] { "OrganizationId", "AssetId", "Outcome" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asset_inspections",
                schema: "tenebit");

            migrationBuilder.DropColumn(
                name: "PhotoOnIssue",
                schema: "tenebit",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "PhotoOnReturn",
                schema: "tenebit",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "PostReturnDisposition",
                schema: "tenebit",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "ReturnChecklistTemplate",
                schema: "tenebit",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "ReturnHandlingMode",
                schema: "tenebit",
                table: "asset_categories");
        }
    }
}
