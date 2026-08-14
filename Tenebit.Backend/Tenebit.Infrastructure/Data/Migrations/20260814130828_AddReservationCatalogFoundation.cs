using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationCatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReservable",
                schema: "tenebit",
                table: "assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxReservationDays",
                schema: "tenebit",
                table: "assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservationInstructions",
                schema: "tenebit",
                table: "assets",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatalogDescription",
                schema: "tenebit",
                table: "asset_categories",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatalogImageUrl",
                schema: "tenebit",
                table: "asset_categories",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatalogName",
                schema: "tenebit",
                table: "asset_categories",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservationMode",
                schema: "tenebit",
                table: "asset_categories",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "RequestByCategory");

            migrationBuilder.AddColumn<bool>(
                name: "VisibleInEmployeeCatalog",
                schema: "tenebit",
                table: "asset_categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "equipment_kit_definitions",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    VisibleInEmployeeCatalog = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_kit_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "equipment_kit_definition_items",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    KitDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_kit_definition_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_equipment_kit_definition_items_equipment_kit_definitions_Ki~",
                        column: x => x.KitDefinitionId,
                        principalSchema: "tenebit",
                        principalTable: "equipment_kit_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_kit_definition_items_KitDefinitionId",
                schema: "tenebit",
                table: "equipment_kit_definition_items",
                column: "KitDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_kit_definition_items_OrganizationId_KitDefinition~",
                schema: "tenebit",
                table: "equipment_kit_definition_items",
                columns: new[] { "OrganizationId", "KitDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_kit_definitions_OrganizationId_Name",
                schema: "tenebit",
                table: "equipment_kit_definitions",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment_kit_definition_items",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "equipment_kit_definitions",
                schema: "tenebit");

            migrationBuilder.DropColumn(
                name: "IsReservable",
                schema: "tenebit",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "MaxReservationDays",
                schema: "tenebit",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "ReservationInstructions",
                schema: "tenebit",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "CatalogDescription",
                schema: "tenebit",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "CatalogImageUrl",
                schema: "tenebit",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "CatalogName",
                schema: "tenebit",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "ReservationMode",
                schema: "tenebit",
                table: "asset_categories");

            migrationBuilder.DropColumn(
                name: "VisibleInEmployeeCatalog",
                schema: "tenebit",
                table: "asset_categories");
        }
    }
}
