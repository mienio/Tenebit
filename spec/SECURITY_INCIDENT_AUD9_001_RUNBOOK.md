# AUD9-001 capability credential incident closure

This runbook closes the **incident**, not only the code defect. The historical issue was that a raw public capability credential could appear in an HTTP request target and therefore in application/reverse-proxy logs. The current architecture uses browser fragments, an immediate POST exchange and a scoped HttpOnly cookie; `/api/public` request paths are also excluded from API request logging and nginx access logging is disabled.

## Required closure sequence

1. Deploy the revision containing migration `20260818181000_Audit9CapabilityIncidentFinalClosure`.
2. Run the migrator before starting normal API replicas. The migration revokes every assignment/offboarding/audit capability, unused password-reset token and unused e-mail-verification token that existed before deployment. It also quarantines pending security e-mails carrying those now-revoked credentials.
3. Regenerate/resend only links that are still required after the migration.
4. Remove local application/proxy logs and runtime/support artifacts that contain historical credential-bearing request targets. `scripts/purge-sensitive-runtime-artifacts.sh` handles the project tree.
5. Search centralized log stores, reverse-proxy/platform logs, support bundles and retained archives/backups. Delete, expire, quarantine or access-restrict affected copies according to the incident/legal retention decision. This external step cannot be proven from application source code.
6. Run the closure verifier with the deployed database and every filesystem/log-export directory available to the operator:

```bash
TENEBIT_INCIDENT_DB_URL='postgresql://...' \
TENEBIT_INCIDENT_SCAN_ROOTS='.:/path/to/exported-logs:/path/to/support-bundles' \
TENEBIT_EXTERNAL_LOGS_REVIEWED=true \
TENEBIT_BACKUPS_SUPPORT_REVIEWED=true \
bash scripts/close-aud9-001-incident.sh
```

The scanner reports **file names only**, never the matching credential or line.

## Closure evidence

AUD9-001 may be marked incident-closed only when all of the following are true:

- final revocation migration is present in the deployed database migration history;
- project/runtime/support scan returns zero credential-shaped legacy request targets;
- centralized/reverse-proxy log stores were reviewed;
- support bundles and retained backups containing historical logs were inventoried and handled under the incident retention decision;
- runtime CI secret-not-in-logs regression test remains green;
- newly generated links use the fragment -> POST exchange -> HttpOnly capability-session flow.

A source-code patch cannot retroactively delete copies already exported to an external SIEM, backup provider, support ticket or operator workstation. The two explicit attestation environment variables exist so this last operational responsibility cannot be silently mistaken for an automated code check.
