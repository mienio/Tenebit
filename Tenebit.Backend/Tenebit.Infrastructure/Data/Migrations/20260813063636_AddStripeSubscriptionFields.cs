using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeSubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                schema: "tenebit",
                table: "subscriptions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                schema: "tenebit",
                table: "subscriptions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_StripeCustomerId",
                schema: "tenebit",
                table: "subscriptions",
                column: "StripeCustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subscriptions_StripeCustomerId",
                schema: "tenebit",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                schema: "tenebit",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                schema: "tenebit",
                table: "subscriptions");
        }
    }
}
