using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeWebhookIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastWebhookEventAt",
                schema: "tenebit",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "processed_stripe_events",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_stripe_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_processed_stripe_events_EventId",
                schema: "tenebit",
                table: "processed_stripe_events",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_stripe_events",
                schema: "tenebit");

            migrationBuilder.DropColumn(
                name: "LastWebhookEventAt",
                schema: "tenebit",
                table: "subscriptions");
        }
    }
}
