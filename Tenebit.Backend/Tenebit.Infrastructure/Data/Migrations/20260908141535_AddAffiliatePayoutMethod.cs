using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliatePayoutMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayoutMethod",
                schema: "tenebit",
                table: "affiliates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Revolut");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayoutMethod",
                schema: "tenebit",
                table: "affiliates");
        }
    }
}
