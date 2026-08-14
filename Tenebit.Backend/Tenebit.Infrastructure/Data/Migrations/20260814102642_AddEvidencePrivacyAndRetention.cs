using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidencePrivacyAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CapturePublicIp",
                schema: "tenebit",
                table: "organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Off");

            migrationBuilder.AddColumn<int>(
                name: "DefaultEvidenceRetentionMonths",
                schema: "tenebit",
                table: "organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyContactEmail",
                schema: "tenebit",
                table: "organizations",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyNoticeUrl",
                schema: "tenebit",
                table: "organizations",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublicIpRetentionDays",
                schema: "tenebit",
                table: "organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LegalHold",
                schema: "tenebit",
                table: "asset_evidence",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RedactedAt",
                schema: "tenebit",
                table: "asset_evidence",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapturePublicIp",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "DefaultEvidenceRetentionMonths",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PrivacyContactEmail",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PrivacyNoticeUrl",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PublicIpRetentionDays",
                schema: "tenebit",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "LegalHold",
                schema: "tenebit",
                table: "asset_evidence");

            migrationBuilder.DropColumn(
                name: "RedactedAt",
                schema: "tenebit",
                table: "asset_evidence");
        }
    }
}
