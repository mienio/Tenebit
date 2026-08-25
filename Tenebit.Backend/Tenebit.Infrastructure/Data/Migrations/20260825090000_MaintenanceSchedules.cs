using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Recurring maintenance obligations per asset (annual fire-extinguisher inspection, six-monthly ladder
/// check, and so on). The composite foreign key to (OrganizationId, AssetId) makes it impossible to
/// attach a schedule to an asset belonging to another organization, matching the pattern the rest of
/// the tenant tables use.
/// Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260825090000_MaintenanceSchedules")]
public partial class MaintenanceSchedules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS tenebit.maintenance_schedules (
                "Id" uuid NOT NULL CONSTRAINT "PK_maintenance_schedules" PRIMARY KEY,
                "OrganizationId" uuid NOT NULL,
                "AssetId" uuid NOT NULL,
                "Name" character varying(160) NOT NULL,
                "IntervalMonths" integer NOT NULL,
                "NextDueOn" date NOT NULL,
                "LastPerformedOn" date NULL,
                "LastPerformedBy" character varying(240) NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "AK_maintenance_schedules_OrganizationId_Id" UNIQUE ("OrganizationId", "Id"),
                CONSTRAINT "FK_maintenance_schedules_assets_OrganizationId_AssetId"
                    FOREIGN KEY ("OrganizationId", "AssetId")
                    REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE CASCADE
            );
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_maintenance_schedules_OrganizationId_NextDueOn"
                ON tenebit.maintenance_schedules ("OrganizationId", "NextDueOn");
            CREATE INDEX IF NOT EXISTS "IX_maintenance_schedules_OrganizationId_AssetId"
                ON tenebit.maintenance_schedules ("OrganizationId", "AssetId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS tenebit.maintenance_schedules;");
}
