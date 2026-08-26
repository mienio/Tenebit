using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Rate-limit state for public QR issue reports.
///
/// Previously the limit was inferred from the activity log, whose actor for these reports is the
/// constant "public-scan" - so every reporter shared one identity and the first report silenced
/// everyone else on that asset until the window passed. This table gives the limiter its own storage,
/// keyed by a one-way pseudonym of the reporter, so the limits can be per reporter and per asset at
/// once without the audit trail having to carry addresses.
///
/// Rows are disposable: nothing outside the newest hour is ever read, and they are purged as reports
/// come in. Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260826120000_PublicReportThrottle")]
public partial class PublicReportThrottle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS tenebit.public_report_throttle (
                "Id" uuid NOT NULL CONSTRAINT "PK_public_report_throttle" PRIMARY KEY,
                "OrganizationId" uuid NOT NULL,
                "AssetId" uuid NOT NULL,
                "ReporterHash" character varying(64) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "FK_public_report_throttle_assets_OrganizationId_AssetId"
                    FOREIGN KEY ("OrganizationId", "AssetId")
                    REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE CASCADE
            );
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_public_report_throttle_OrganizationId_AssetId_CreatedAt"
                ON tenebit.public_report_throttle ("OrganizationId", "AssetId", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_public_report_throttle_OrganizationId_ReporterHash_CreatedAt"
                ON tenebit.public_report_throttle ("OrganizationId", "ReporterHash", "CreatedAt");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS tenebit.public_report_throttle;");
}
