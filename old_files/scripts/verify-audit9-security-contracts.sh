#!/usr/bin/env bash
set -euo pipefail
fail() { printf 'AUD9 regression: %s\n' "$1" >&2; exit 1; }

# Raw public capability credentials must never be part of an HTTP request path.
if grep -RIE --include='*.cs' '/public/(assignments|offboarding|asset-audits)/?\{token' Tenebit.Backend/Tenebit.Api/Endpoints; then
  fail 'raw capability token returned to an API route path'
fi
if grep -REn --include='*.tsx' --include='*.ts' '/api/public/(assignments|offboarding|asset-audits)/\$?\{?token' Tenebit.Frontend/src; then
  fail 'frontend again puts capability token in an API path'
fi

# E-mail credentials use browser fragments; query/path tokens would be visible to proxies/access logs.
grep -q 'accept#{Uri.EscapeDataString(rawToken)}' Tenebit.Backend/Tenebit.Infrastructure/Services/AppLinkBuilder.cs || fail 'assignment link is not fragment-only'
grep -q 'exit#{Uri.EscapeDataString(rawToken)}' Tenebit.Backend/Tenebit.Infrastructure/Services/AppLinkBuilder.cs || fail 'offboarding link is not fragment-only'
grep -q 'audit#{Uri.EscapeDataString(rawToken)}' Tenebit.Backend/Tenebit.Infrastructure/Services/AppLinkBuilder.cs || fail 'audit link is not fragment-only'
grep -q 'reset-password#{Uri.EscapeDataString(rawToken)}' Tenebit.Backend/Tenebit.Infrastructure/Services/AppLinkBuilder.cs || fail 'reset link is not fragment-only'
grep -q 'verify-email#{Uri.EscapeDataString(rawToken)}' Tenebit.Backend/Tenebit.Infrastructure/Services/AppLinkBuilder.cs || fail 'verification link is not fragment-only'

# Reverse-proxy and API request logs must not capture capability request targets.
grep -Eq '^[[:space:]]*access_log[[:space:]]+off;' Tenebit.Frontend/nginx.conf || fail 'nginx access log is enabled'
grep -q '!context.Request.Path.StartsWithSegments("/api/public")' Tenebit.Backend/Tenebit.Api/Program.cs || fail 'Serilog public-route exclusion missing'
if grep -n 'MessageTemplate.*RequestPath' Tenebit.Backend/Tenebit.Api/Program.cs; then fail 'Serilog template logs RequestPath'; fi

# The exact concurrency/security primitives required by audit9 may not silently regress.
grep -q 'TryConsumeAsync(string tokenHash' Tenebit.Backend/Tenebit.Application/Abstractions/IRepositories.cs || fail 'atomic password reset consume missing'
grep -q 'TryConsumeAsync(Guid organizationUserId, string codeHash' Tenebit.Backend/Tenebit.Application/Abstractions/IRepositories.cs || fail 'atomic recovery-code consume missing'
grep -q 'ListPagedScopedAsync' Tenebit.Backend/Tenebit.Application/Abstractions/IRepositories.cs || fail 'scoped ticket query missing'
grep -q 'Idempotency-Key' Tenebit.Backend/Tenebit.Infrastructure/Services/StripePaymentGateway.cs || fail 'Stripe idempotency key missing'

# A second incident-response migration is required because audit11 may have been deployed after the
# original revocation migration. Reusing the old migration ID would leave regression-window links live.
grep -q '20260818143000_Audit11RegressionCredentialRevocation' Tenebit.Backend/Tenebit.Infrastructure/Data/Migrations/20260818143000_Audit11RegressionCredentialRevocation.cs || fail 'audit11 regression credential revocation migration missing'
grep -q '20260818143000_Audit11RegressionCredentialRevocation' Tenebit.Backend/migrate.sql || fail 'manual migration script omits audit11 regression credential revocation'


# AUD9-001 final incident-closure controls. Code fixes alone are insufficient after a proven log exposure.
grep -q '20260818181000_Audit9CapabilityIncidentFinalClosure' Tenebit.Backend/Tenebit.Infrastructure/Data/Migrations/20260818181000_Audit9CapabilityIncidentFinalClosure.cs || fail 'final AUD9-001 credential revocation migration missing'
grep -q '20260818181000_Audit9CapabilityIncidentFinalClosure' Tenebit.Backend/migrate.sql || fail 'manual migration script omits final AUD9-001 credential revocation'
grep -q 'incident_credential_revoked' Tenebit.Backend/Tenebit.Infrastructure/Data/Migrations/20260818181000_Audit9CapabilityIncidentFinalClosure.cs || fail 'pending credential e-mails are not quarantined by final incident migration'
for file in scripts/scan-sensitive-runtime-artifacts.sh scripts/verify-aud9-001-incident-closure.sh scripts/close-aud9-001-incident.sh SECURITY_INCIDENT_AUD9_001_RUNBOOK.md; do
  [[ -f "$file" ]] || fail "AUD9-001 incident closure artifact missing: $file"
done

# AUD9-008 final closure: capability/recovery mail must use a durable encrypted transactional outbox.
grep -q 'class PostgresEmailOutboxWriter' Tenebit.Backend/Tenebit.Infrastructure/Services/PostgresEmailOutboxWriter.cs || fail 'transactional email outbox writer missing'
grep -q 'FOR UPDATE SKIP LOCKED' Tenebit.Backend/Tenebit.Infrastructure/Services/EmailOutboxBackgroundService.cs || fail 'outbox worker is not multi-replica safe'
grep -q '20260818162000_TransactionalEmailOutbox' Tenebit.Backend/Tenebit.Infrastructure/Data/Migrations/20260818162000_TransactionalEmailOutbox.cs || fail 'outbox migration missing'
grep -q '20260818162000_TransactionalEmailOutbox' Tenebit.Backend/migrate.sql || fail 'manual migration script omits transactional outbox'
for file in \
  Tenebit.Backend/Tenebit.Application/Assignments/AssignmentService.cs \
  Tenebit.Backend/Tenebit.Application/Offboarding/OffboardingService.cs \
  Tenebit.Backend/Tenebit.Application/Audits/AssetAuditCampaignService.cs \
  Tenebit.Backend/Tenebit.Application/Identity/AuthService.cs \
  Tenebit.Backend/Tenebit.Application/Identity/UserAccessService.cs; do
  grep -q '_emailOutbox' "$file" || fail "security-sensitive e-mail bypasses outbox in $file"
done

grep -q 'Enqueue_RollsBackWithBusinessTransaction_AndEncryptsSecretAtRest' Tenebit.Backend/Tenebit.Tests/Integration/EmailOutboxIntegrationTests.cs || fail 'outbox rollback fault test missing'
grep -q 'TwoDispatchers_ClaimSamePendingMessage_ExactlyOneTransportSendOccurs' Tenebit.Backend/Tenebit.Tests/Integration/EmailOutboxIntegrationTests.cs || fail 'outbox duplicate-worker test missing'
grep -q 'ExpiredLeaseAfterSimulatedPostSmtpCrash_RetriesWithSameStableMessageId' Tenebit.Backend/Tenebit.Tests/Integration/EmailOutboxIntegrationTests.cs || fail 'outbox post-SMTP crash recovery test missing'
grep -q 'ExhaustedDelivery_DeadLettersAndErasesEncryptedPayload' Tenebit.Backend/Tenebit.Tests/Integration/EmailOutboxIntegrationTests.cs || fail 'outbox dead-letter test missing'
grep -q 'TenantA_CannotReadCriticalResourcesOwnedByTenantB' Tenebit.Backend/Tenebit.Tests/Integration/Audit9TenantHttpMatrixIntegrationTests.cs || fail 'tenant A/B HTTP matrix test missing'
grep -q 'clientA.PutAsJsonAsync' Tenebit.Backend/Tenebit.Tests/Integration/Audit9TenantHttpMatrixIntegrationTests.cs || fail 'tenant A/B mutation matrix missing'
grep -q 'clientA.DeleteAsync' Tenebit.Backend/Tenebit.Tests/Integration/Audit9TenantHttpMatrixIntegrationTests.cs || fail 'tenant A/B delete matrix missing'
grep -q '/api/assets/{assetB.Id}/evidence' Tenebit.Backend/Tenebit.Tests/Integration/Audit9TenantHttpMatrixIntegrationTests.cs || fail 'tenant A/B child-resource matrix missing'
grep -q 'Production SMTP must use TLS' Tenebit.Backend/Tenebit.Api/ProductionSecurityConfiguration.cs || fail 'production SMTP TLS fail-closed guard missing'
grep -q 'Backup restore + encrypted-data key-ring drill' .github/workflows/ci.yml || fail 'backup/restore key-ring CI gate missing'
grep -q 'TENEBIT_RESTORE_DB_CONNECTION' scripts/backup-restore-drill.sh || fail 'Npgsql restore verification connection missing'

printf '%s\n' 'AUD9 static security contracts: PASS'

# Unverified preregistration credentials must never survive through ordinary JWT/session gates.
grep -q '!user.IsEmailVerified' Tenebit.Backend/Tenebit.Api/Program.cs || fail 'JWT validation does not reject unverified accounts'
grep -q '!user.IsEmailVerified' Tenebit.Backend/Tenebit.Application/Identity/AuthService.cs || fail 'sign-in flow does not enforce verified email'

# Public-IP privacy must be centralized at every capture point and have an executable retention worker.
[[ "$(grep -RIl 'PublicIpPrivacyPolicy.Capture' Tenebit.Backend/Tenebit.Application/Assignments/AssignmentService.cs Tenebit.Backend/Tenebit.Application/Assets/AssetService.cs | wc -l)" -eq 2 ]] || fail 'public IP capture bypasses privacy policy'
grep -q 'class PublicIpRetentionBackgroundService' Tenebit.Backend/Tenebit.Infrastructure/Services/PublicIpRetentionBackgroundService.cs || fail 'public IP retention worker missing'
grep -q 'RetentionCycle_RedactsExpiredStructuredIp_ButKeepsUnexpiredIp' Tenebit.Backend/Tenebit.Tests/Integration/PublicIpRetentionIntegrationTests.cs || fail 'public IP retention integration test missing'

# Periodic job coordination must not keep an explicit EF transaction open around external action I/O.
if grep -q 'BeginTransaction' Tenebit.Backend/Tenebit.Infrastructure/Services/PostgresJobLock.cs; then
  fail 'job gate again wraps action in a long DB transaction'
fi

# Billing distinguishes entitlement from a still-live provider subscription and uses persistent attempts.
grep -q 'HasLiveStripeSubscription' Tenebit.Backend/Tenebit.Domain/Subscriptions/OrganizationSubscription.cs || fail 'live Stripe subscription state missing'
grep -q 'GetOrCreateCheckoutAttempt' Tenebit.Backend/Tenebit.Domain/Subscriptions/OrganizationSubscription.cs || fail 'persistent checkout attempt missing'

# A ticket linked to an inspection must derive/validate the same asset identity.
grep -q 'inspection.AssetId != request.AssetId' Tenebit.Backend/Tenebit.Application/Assets/ServiceTicketService.cs || fail 'service ticket still accepts inspection from another asset'

printf '%s\n' 'AUD9 extended closure contracts: PASS'
