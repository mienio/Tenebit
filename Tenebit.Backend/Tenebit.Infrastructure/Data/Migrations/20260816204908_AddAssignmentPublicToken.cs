using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentPublicToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublicTokenExpiresAt",
                schema: "tenebit",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicTokenHash",
                schema: "tenebit",
                table: "assignments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublicTokenRevokedAt",
                schema: "tenebit",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicTokenExpiresAt",
                schema: "tenebit",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "PublicTokenHash",
                schema: "tenebit",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "PublicTokenRevokedAt",
                schema: "tenebit",
                table: "assignments");
        }
    }
}
