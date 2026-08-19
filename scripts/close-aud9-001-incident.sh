#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"

cat <<'MSG'
AUD9-001 closure sequence:
  1. Deploy this revision and run the database migrator first.
  2. This script removes local runtime/build artifacts from the project tree.
  3. The verifier then requires the final migration plus explicit review of external logs/backups.

Before running this command, remove or access-restrict historical copies in centralized proxy/app log stores,
support bundles and backup locations according to your incident-retention decision. Do not paste raw tokens into tickets.
MSG

"$root/scripts/purge-sensitive-runtime-artifacts.sh" "$root"
exec "$root/scripts/verify-aud9-001-incident-closure.sh"
