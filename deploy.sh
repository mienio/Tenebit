#!/usr/bin/env bash
set -Eeuo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

DOMAIN="https://teneb.it"
STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUPS="./backups"
mkdir -p "$BACKUPS"

echo "=== TENEBIT DEPLOY ==="

echo "[1/5] Backup bazy..."
docker exec tenebit-db pg_dump -U tenebit -d tenebit 2>/dev/null | gzip > "$BACKUPS/db-$STAMP.sql.gz" \
  || echo "  WARN: backup bazy nie powiódł się (kontynuuję)"
find "$BACKUPS" -name 'db-*.sql.gz' -mtime +14 -delete 2>/dev/null || true

echo "[2/5] Build obrazów..."
docker compose build

echo "[3/5] Deploy (migracja + restart usług)..."
docker compose up -d

echo "  Czekam na gotowość backendu..."
for attempt in $(seq 1 40); do
  if curl -fsS "$DOMAIN/api/health" >/dev/null 2>&1; then
    echo "  Backend odpowiedział po $((attempt * 3))s"
    break
  fi
  sleep 3
done

echo "[4/5] Weryfikacja..."
HEALTH="FAIL"
FRONTEND_CHECK=""
for attempt in $(seq 1 10); do
  HEALTH=$(curl -fsS "$DOMAIN/api/health" 2>/dev/null || echo "FAIL")
  FRONTEND_CHECK=$(curl -fsS "$DOMAIN/" 2>/dev/null | head -20 | grep -E "Tenebit|assets/index|DOCTYPE" || echo "")
  if [[ $HEALTH == *"ok"* ]] && [[ -n "$FRONTEND_CHECK" ]]; then
    break
  fi
  sleep 3
done

echo "  Backend health: $HEALTH"
echo "  Frontend check: ${FRONTEND_CHECK:0:100}"

if [[ $HEALTH != *"ok"* ]] || [[ -z "$FRONTEND_CHECK" ]]; then
  echo -e "\n=== FAIL ==="
  echo "Backend logs:"
  docker logs --tail=50 tenebit-backend
  echo -e "\nFrontend logs:"
  docker logs --tail=20 tenebit-frontend
  exit 1
fi

echo "[5/5] Smoke test (rejestracja)..."
SMOKE_STAMP="$(date +%s)"
SMOKE_EMAIL="smoketest+${SMOKE_STAMP}@tenebit-internal.test"
SMOKE_PASSWORD="SmokeTest-${SMOKE_STAMP}-Pwd9!"
SMOKE_OK=1

READY="$(curl -fsS "$DOMAIN/api/health/ready" 2>&1)" || SMOKE_OK=0
[[ "$READY" == *'"status":"ready"'* ]] || SMOKE_OK=0

REGISTER_BODY="{\"organizationName\":\"Smoke Test ${SMOKE_STAMP}\",\"email\":\"${SMOKE_EMAIL}\",\"password\":\"${SMOKE_PASSWORD}\",\"displayName\":\"Smoke Test\",\"currency\":\"PLN\",\"acceptTerms\":true}"
REGISTER_RESPONSE="$(curl -fsS -X POST "$DOMAIN/api/auth/register" -H "Content-Type: application/json" -d "$REGISTER_BODY" 2>&1)" || SMOKE_OK=0
[[ "$REGISTER_RESPONSE" == *'"requiresEmailVerification"'* ]] || SMOKE_OK=0

if [ "$SMOKE_OK" != "1" ]; then
  echo -e "\n=== FAIL (smoke test) ==="
  echo "health/ready: $READY"
  echo "register: $REGISTER_RESPONSE"
  echo -e "\nBackend logs:"
  docker logs --tail=80 tenebit-backend
  exit 1
fi
echo "  OK (konto testowe: $SMOKE_EMAIL)"

docker exec -i tenebit-db psql -U tenebit -d tenebit -v ON_ERROR_STOP=1 \
  -v org_name="Smoke Test ${SMOKE_STAMP}" <<'CLEANUP_SQL' >/dev/null 2>&1 || echo "  WARN: sprzątanie smoke testu nie powiodło się (nieszkodliwe)"
BEGIN;
CREATE TEMP TABLE smoke_org ON COMMIT DROP AS
SELECT "Id" FROM tenebit.organizations WHERE "Name" = :'org_name';

DO $$
DECLARE
  target record;
  pass int;
BEGIN
  FOR pass IN 1..6 LOOP
    FOR target IN
      SELECT c.table_schema, c.table_name
      FROM information_schema.columns c
      JOIN information_schema.tables t
        ON t.table_schema = c.table_schema AND t.table_name = c.table_name
      WHERE c.column_name = 'OrganizationId'
        AND c.table_schema = 'tenebit'
        AND t.table_type = 'BASE TABLE'
        AND c.table_name <> 'organizations'
    LOOP
      BEGIN
        EXECUTE format(
          'DELETE FROM %I.%I WHERE "OrganizationId" IN (SELECT "Id" FROM smoke_org)',
          target.table_schema, target.table_name);
      EXCEPTION WHEN foreign_key_violation THEN
        NULL;
      END;
    END LOOP;
  END LOOP;
  DELETE FROM tenebit.organizations WHERE "Id" IN (SELECT "Id" FROM smoke_org);
END $$;
COMMIT;
CLEANUP_SQL

echo -e "\n=== SUCCESS ==="
echo "URL: $DOMAIN"
docker compose ps
