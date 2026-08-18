#!/usr/bin/env bash
set -euo pipefail
: "${TENEBIT_SOURCE_DB_URL:?set TENEBIT_SOURCE_DB_URL}"
: "${TENEBIT_RESTORE_DB_URL:?set TENEBIT_RESTORE_DB_URL to a disposable database}"
: "${TENEBIT_API_DLL:?set TENEBIT_API_DLL to the published Tenebit.Api.dll}"
command -v pg_dump >/dev/null
command -v pg_restore >/dev/null
command -v psql >/dev/null
command -v dotnet >/dev/null
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
dump="$tmp/tenebit.dump"
pg_dump --format=custom --no-owner --no-acl --dbname="$TENEBIT_SOURCE_DB_URL" --file="$dump"
pg_restore --clean --if-exists --no-owner --no-acl --dbname="$TENEBIT_RESTORE_DB_URL" "$dump"
psql "$TENEBIT_RESTORE_DB_URL" -v ON_ERROR_STOP=1 -c 'SELECT count(*) FROM tenebit.organization_users' >/dev/null
# Reuse the operator-provided Auth__FieldEncryption__* key ring. Nothing here prints plaintext.
ConnectionStrings__TenebitDb="$TENEBIT_RESTORE_DB_URL" Database__AutoCreate=false Seed__Enabled=false \
  dotnet "$TENEBIT_API_DLL" --verify-encrypted-data
printf '%s\n' 'Backup restore + encrypted-data/key-ring verification: PASS'
