using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AffiliateCustomerDiscountDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultCustomerDiscountDurationMonths",
                schema: "tenebit",
                table: "affiliate_program_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultCustomerDiscountPercent",
                schema: "tenebit",
                table: "affiliate_program_settings",
                type: "numeric(5,2)",
                nullable: true);

            // Turns on the "manually-typed affiliate code also discounts the customer" behavior for the
            // one already-existing settings row (a fresh environment gets this from
            // AffiliateProgramSettings.CreateDefault instead) - product decision: 20% off for 3 months,
            // matching the default commission rate's own recent 20% -> 10% history is unrelated, this is
            // a separate customer-facing number.
            migrationBuilder.Sql(@"
                UPDATE tenebit.affiliate_program_settings
                SET ""CodeGrantsCustomerDiscountByDefault"" = true,
                    ""DefaultCustomerDiscountPercent"" = 20,
                    ""DefaultCustomerDiscountDurationMonths"" = 3
                WHERE ""Id"" = '00000000-0000-0000-0000-000000000001';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultCustomerDiscountDurationMonths",
                schema: "tenebit",
                table: "affiliate_program_settings");

            migrationBuilder.DropColumn(
                name: "DefaultCustomerDiscountPercent",
                schema: "tenebit",
                table: "affiliate_program_settings");
        }
    }
}
