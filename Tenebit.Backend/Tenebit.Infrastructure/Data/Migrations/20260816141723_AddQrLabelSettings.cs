using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQrLabelSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "QrLabelShowName",
                schema: "tenebit",
                table: "organizations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "QrLabelShowTag",
                schema: "tenebit",
                table: "organizations",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QrLabelShowName",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "QrLabelShowTag",
                schema: "tenebit",
                table: "organizations");
        }
    }
}
