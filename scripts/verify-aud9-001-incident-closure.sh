#!/usr/bin/env bash
set -euo pipefail

: "${TENEBIT_INCIDENT_DB_URL:?Set TENEBIT_INCIDENT_DB_URL to a libpq PostgreSQL URL/conninfo for the deployed database.}"
: "${TENEBIT_EXTERNAL_LOGS_REVIEWED:?Set TENEBIT_EXTERNAL_LOGS_REVIEWED=true only after central/reverse-proxy log stores were searched and historical copies were removed or access-restricted under the incident retention decision.}"
: "${TENEBIT_BACKUPS_SUPPORT_REVIEWED:?Set TENEBIT_BACKUPS_SUPPORT_REVIEWED=true only after support bundles/backups containing the exposed logs were inventoried and handled under the incident retention decision.}"

[[ "$TENEBIT_EXTERNAL_LOGS_REVIEWED" == "true" ]] || { echo 'External log review attestation is not true.' >&2; exit 4; }
[[ "$TENEBIT_BACKUPS_SUPPORT_REVIEWED" == "true" ]] || { echo 'Backup/support review attestation is not true.' >&2; exit 4; }
command -v psql >/dev/null 2>&1 || { echo 'psql is required for incident closure verification.' >&2; exit 3; }

migration='20260818181000_Audit9CapabilityIncidentFinalClosure'
applied="$(psql "$TENEBIT_INCIDENT_DB_URL" -X -A -t -v ON_ERROR_STOP=1 -c \
  "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '$migration';")"
[[ "$applied" == "1" ]] || { echo 'Final AUD9-001 credential-revocation migration is not applied.' >&2; exit 5; }

# Scan project/runtime/support roots. Colon-separated paths are accepted; default is the project root.
scan_roots_raw="${TENEBIT_INCIDENT_SCAN_ROOTS:-.}"
IFS=':' read -r -a scan_roots <<< "$scan_roots_raw"
"$(cd "$(dirname "$0")" && pwd)/scan-sensitive-runtime-artifacts.sh" "${scan_roots[@]}"

printf '%s\n' 'AUD9-001 INCIDENT CLOSURE VERIFICATION: PASS'
printf '%s\n' "- final revocation migration applied: $migration"
printf '%s\n' '- local/runtime/support credential-pattern scan: PASS'
printf '%s\n' '- external/central log review: operator attested'
printf '%s\n' '- backups/support bundle review: operator attested'
printf '%s\n' '- raw credentials are intentionally not printed by this verifier'
