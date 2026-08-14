using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertRulesAndDigestSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_digest_settings",
                schema: "tenebit",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DayOfWeek = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    LocalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    QuietHoursStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    QuietHoursEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    BusinessDays = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HolidayCalendarCountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    IncludeEmptyDigest = table.Column<bool>(type: "boolean", nullable: false),
                    LastGeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_digest_settings", x => x.OrganizationId);
                });

            migrationBuilder.CreateTable(
                name: "alert_rules",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ThresholdDays = table.Column<List<int>>(type: "integer[]", nullable: false),
                    DeliveryMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecipientMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CustomEmails = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    CooldownDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_rules_OrganizationId",
                schema: "tenebit",
                table: "alert_rules",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_rules_OrganizationId_Type",
                schema: "tenebit",
                table: "alert_rules",
                columns: new[] { "OrganizationId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_digest_settings",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "alert_rules",
                schema: "tenebit");
        }
    }
}
