using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

[DbContext(typeof(TenebitDbContext))]
[Migration("20260818143000_Audit11RegressionCredentialRevocation")]
public partial class Audit11RegressionCredentialRevocation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // AUD11 incident response: the source package regressed to legacy public credential URLs and
        // contains runtime logs with those request targets. Revoke again even when the earlier AUD9
        // migration was already applied before the regression window.
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

            UPDATE tenebit.activity_logs
            SET "ActorSubject" = 'public-scan'
            WHERE "ActorSubject" LIKE 'public-scan:%';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversible by design. A rollback must never resurrect credentials treated as exposed.
    }
}
