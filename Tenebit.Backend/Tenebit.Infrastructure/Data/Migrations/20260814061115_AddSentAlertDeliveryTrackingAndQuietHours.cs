using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSentAlertDeliveryTrackingAndQuietHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sent_alerts_OrganizationId_AlertKey_EntityId",
                schema: "tenebit",
                table: "sent_alerts");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SentAt",
                schema: "tenebit",
                table: "sent_alerts",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "tenebit",
                table: "sent_alerts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "tenebit",
                table: "sent_alerts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "DigestId",
                schema: "tenebit",
                table: "sent_alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                schema: "tenebit",
                table: "sent_alerts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                schema: "tenebit",
                table: "sent_alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientEmail",
                schema: "tenebit",
                table: "sent_alerts",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "tenebit",
                table: "sent_alerts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "QuietHoursEnd",
                schema: "tenebit",
                table: "organizations",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "QuietHoursStart",
                schema: "tenebit",
                table: "organizations",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sent_alerts_OrganizationId_AlertKey_EntityId_RecipientEmail",
                schema: "tenebit",
                table: "sent_alerts",
                columns: new[] { "OrganizationId", "AlertKey", "EntityId", "RecipientEmail" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sent_alerts_OrganizationId_AlertKey_EntityId_RecipientEmail",
                schema: "tenebit",
                table: "sent_alerts");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "tenebit",
                table: "sent_alerts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "tenebit",
                table: "sent_alerts");

            migrationBuilder.DropColumn(
                name: "DigestId",
                schema: "tenebit",
                table: "sent_alerts");

            migrationBuilder.DropColumn(
                name: "LastError",
                schema: "tenebit",
                table: "sent_alerts");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                schema: "tenebit",
                table: "sent_alerts");

            migrationBuilder.DropColumn(
                name: "RecipientEmail",
                schema: "tenebit",
                table: "sent_alerts");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "tenebit",
                table: "sent_alerts");

            migrationBuilder.DropColumn(
                name: "QuietHoursEnd",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "QuietHoursStart",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SentAt",
                schema: "tenebit",
                table: "sent_alerts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sent_alerts_OrganizationId_AlertKey_EntityId",
                schema: "tenebit",
                table: "sent_alerts",
                columns: new[] { "OrganizationId", "AlertKey", "EntityId" },
                unique: true);
        }
    }
}
