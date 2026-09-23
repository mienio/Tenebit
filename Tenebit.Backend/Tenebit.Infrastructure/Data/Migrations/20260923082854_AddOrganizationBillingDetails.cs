using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationBillingDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine1",
                schema: "tenebit",
                table: "organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine2",
                schema: "tenebit",
                table: "organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCity",
                schema: "tenebit",
                table: "organizations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCompanyName",
                schema: "tenebit",
                table: "organizations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCountry",
                schema: "tenebit",
                table: "organizations",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingPostalCode",
                schema: "tenebit",
                table: "organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                schema: "tenebit",
                table: "organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingAddressLine1",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "BillingAddressLine2",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "BillingCity",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "BillingCompanyName",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "BillingCountry",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "BillingPostalCode",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "TaxId",
                schema: "tenebit",
                table: "organizations");
        }
    }
}
