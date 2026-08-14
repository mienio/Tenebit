using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentAssetPartialReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReturnLocation",
                schema: "tenebit",
                table: "assignment_assets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnNotes",
                schema: "tenebit",
                table: "assignment_assets",
                type: "character varying(800)",
                maxLength: 800,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnResolution",
                schema: "tenebit",
                table: "assignment_assets",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReturnedAt",
                schema: "tenebit",
                table: "assignment_assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnedBy",
                schema: "tenebit",
                table: "assignment_assets",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnLocation",
                schema: "tenebit",
                table: "assignment_assets");

            migrationBuilder.DropColumn(
                name: "ReturnNotes",
                schema: "tenebit",
                table: "assignment_assets");

            migrationBuilder.DropColumn(
                name: "ReturnResolution",
                schema: "tenebit",
                table: "assignment_assets");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                schema: "tenebit",
                table: "assignment_assets");

            migrationBuilder.DropColumn(
                name: "ReturnedBy",
                schema: "tenebit",
                table: "assignment_assets");
        }
    }
}
