using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

/// <summary>
/// Backing store for the platform admin panel:
/// - login_events: append-only sign-in history (successes and failures), the data behind the panel's
///   login timeline. Nothing recorded sign-ins before this migration, so history starts here.
/// - admin_audit_logs: append-only record of admin actions, kept separate from activity_logs because
///   that table is tenant-scoped and admin actions are cross-tenant.
/// - organizations suspension columns: reversible, data-preserving moderation.
/// Written by hand as SQL to match the convention used by the surrounding migrations.
/// </summary>
[DbContext(typeof(TenebitDbContext))]
[Migration("20260824090000_AdminPanelLoginHistoryAndModeration")]
public partial class AdminPanelLoginHistoryAndModeration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS tenebit.login_events (
                "Id" uuid NOT NULL CONSTRAINT "PK_login_events" PRIMARY KEY,
                "OrganizationId" uuid NULL,
                "UserId" uuid NULL,
                "Email" character varying(320) NOT NULL,
                "Succeeded" boolean NOT NULL,
                "FailureReason" character varying(64) NULL,
                "IpAddress" character varying(64) NULL,
                "UserAgent" character varying(400) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "IpExpiresAt" timestamp with time zone NULL
            );
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_login_events_CreatedAt"
                ON tenebit.login_events ("CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_login_events_OrganizationId_CreatedAt"
                ON tenebit.login_events ("OrganizationId", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_login_events_Email_CreatedAt"
                ON tenebit.login_events ("Email", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_login_events_IpExpiresAt"
                ON tenebit.login_events ("IpExpiresAt") WHERE "IpExpiresAt" IS NOT NULL;
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS tenebit.admin_audit_logs (
                "Id" uuid NOT NULL CONSTRAINT "PK_admin_audit_logs" PRIMARY KEY,
                "Action" character varying(80) NOT NULL,
                "TargetType" character varying(40) NULL,
                "TargetId" uuid NULL,
                "TargetLabel" character varying(240) NULL,
                "Details" character varying(1000) NULL,
                "IpAddress" character varying(64) NULL,
                "CreatedAt" timestamp with time zone NOT NULL
            );
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_admin_audit_logs_CreatedAt"
                ON tenebit.admin_audit_logs ("CreatedAt");
            """);

        migrationBuilder.Sql("""
            ALTER TABLE tenebit.organizations
                ADD COLUMN IF NOT EXISTS "IsSuspended" boolean NOT NULL DEFAULT FALSE,
                ADD COLUMN IF NOT EXISTS "SuspendedAt" timestamp with time zone NULL,
                ADD COLUMN IF NOT EXISTS "SuspendedReason" character varying(500) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenebit.organizations
                DROP COLUMN IF EXISTS "IsSuspended",
                DROP COLUMN IF EXISTS "SuspendedAt",
                DROP COLUMN IF EXISTS "SuspendedReason";
            """);
        migrationBuilder.Sql("DROP TABLE IF EXISTS tenebit.admin_audit_logs;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS tenebit.login_events;");
    }
}
