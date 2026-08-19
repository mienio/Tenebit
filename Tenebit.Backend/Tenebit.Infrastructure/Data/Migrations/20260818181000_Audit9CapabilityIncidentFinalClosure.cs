using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

[DbContext(typeof(TenebitDbContext))]
[Migration("20260818181000_Audit9CapabilityIncidentFinalClosure")]
public partial class Audit9CapabilityIncidentFinalClosure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Final AUD9-001 incident action. The supplied support/source bundle still contained historical
        // request logs with raw capability credentials. Revoke every credential that existed before this
        // deployment, even if either of the earlier incident migrations had already run on the server.
        // New credentials issued after this migration use fragment -> POST exchange -> scoped HttpOnly cookie
        // and therefore are not exposed in the HTTP request target.
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

            -- A pending security e-mail can contain the raw credential that has just been revoked above.
            -- Quarantine it instead of delivering a known-dead link after the deployment. The payload is
            -- erased and AttemptCount=8 keeps the dispatcher from reclaiming the row.
            UPDATE tenebit.email_outbox_messages
            SET "AttemptCount" = 8,
                "NextAttemptAt" = NULL,
                "LeaseId" = NULL,
                "LeaseUntil" = NULL,
                "LastError" = 'incident_credential_revoked',
                "RecipientCiphertext" = '',
                "SubjectCiphertext" = '',
                "HtmlCiphertext" = ''
            WHERE "SentAt" IS NULL
              AND "Purpose" IN (
                  'assignment-acceptance',
                  'offboarding-public-link',
                  'asset-audit-public-link',
                  'asset-audit-reminder',
                  'password-reset',
                  'email-verification',
                  'organization-invitation'
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately irreversible. Rollback must never resurrect credentials or e-mail payloads that were
        // treated as potentially disclosed during incident response.
    }
}
