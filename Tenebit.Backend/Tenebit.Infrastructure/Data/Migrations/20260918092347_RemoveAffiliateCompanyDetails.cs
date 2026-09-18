using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAffiliateCompanyDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyName",
                schema: "tenebit",
                table: "affiliates");

            migrationBuilder.DropColumn(
                name: "TaxId",
                schema: "tenebit",
                table: "affiliates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                schema: "tenebit",
                table: "affiliates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                schema: "tenebit",
                table: "affiliates",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }
    }
}
