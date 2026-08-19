using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

[DbContext(typeof(TenebitDbContext))]
[Migration("20260818093000_Audit9CriticalClosure")]
public partial class Audit9CriticalClosure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SourceIp", schema: "tenebit", table: "activity_logs",
            type: "character varying(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SourceIpExpiresAt", schema: "tenebit", table: "activity_logs",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_activity_logs_SourceIpExpiresAt", schema: "tenebit", table: "activity_logs",
            column: "SourceIpExpiresAt");

        migrationBuilder.AddColumn<int>(
            name: "IntegrityVersion", schema: "tenebit", table: "procedure_acceptances",
            type: "integer", nullable: false, defaultValue: 1);

        migrationBuilder.AddColumn<Guid>(
            name: "CheckoutAttemptId", schema: "tenebit", table: "subscriptions",
            type: "uuid", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CheckoutAttemptExpiresAt", schema: "tenebit", table: "subscriptions",
            type: "timestamp with time zone", nullable: true);

        migrationBuilder.CreateTable(
            name: "auth_rate_limit_buckets",
            schema: "tenebit",
            columns: table => new
            {
                KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                BucketStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Count = table.Column<int>(type: "integer", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_auth_rate_limit_buckets", x => new { x.KeyHash, x.BucketStart }));
        migrationBuilder.CreateIndex(
            name: "IX_auth_rate_limit_buckets_ExpiresAt", schema: "tenebit", table: "auth_rate_limit_buckets",
            column: "ExpiresAt");

        // Remove legacy raw IP that was embedded in a non-retainable text identity field.
        migrationBuilder.Sql(
            """
            UPDATE tenebit.activity_logs
            SET "ActorSubject" = 'public-scan'
            WHERE "ActorSubject" LIKE 'public-scan:%';
            """);

        // AUD9-001 incident response: the delivered audit proves raw bearer credentials were copied into
        // runtime logs. Treat every pre-deployment capability/recovery link as potentially disclosed.
        migrationBuilder.Sql(
            """
            UPDATE tenebit.assignments
            SET "PublicTokenRevokedAt" = NOW()
            WHERE "PublicTokenHash" IS NOT NULL AND "PublicTokenRevokedAt" IS NULL;

            UPDATE tenebit.offboarding_cases
            SET "PublicTokenRevokedAt" = NOW()
            WHERE "PublicTokenHash" IS NOT NULL AND "PublicTokenRevokedAt" IS NULL;

            UPDATE tenebit.asset_audit_participants
            SET "TokenRevokedAt" = NOW()
            WHERE "TokenHash" IS NOT NULL AND "TokenRevokedAt" IS NULL;

            UPDATE tenebit.password_reset_tokens
            SET "UsedAt" = NOW()
            WHERE "UsedAt" IS NULL;

            UPDATE tenebit.email_verification_tokens
            SET "UsedAt" = NOW()
            WHERE "UsedAt" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Revocation/redaction above is intentionally irreversible: a rollback must never resurrect a
        // credential already treated as exposed.
        migrationBuilder.DropTable(name: "auth_rate_limit_buckets", schema: "tenebit");
        migrationBuilder.DropColumn(name: "CheckoutAttemptId", schema: "tenebit", table: "subscriptions");
        migrationBuilder.DropColumn(name: "CheckoutAttemptExpiresAt", schema: "tenebit", table: "subscriptions");
        migrationBuilder.DropColumn(name: "IntegrityVersion", schema: "tenebit", table: "procedure_acceptances");
        migrationBuilder.DropIndex(name: "IX_activity_logs_SourceIpExpiresAt", schema: "tenebit", table: "activity_logs");
        migrationBuilder.DropColumn(name: "SourceIp", schema: "tenebit", table: "activity_logs");
        migrationBuilder.DropColumn(name: "SourceIpExpiresAt", schema: "tenebit", table: "activity_logs");
    }
}
