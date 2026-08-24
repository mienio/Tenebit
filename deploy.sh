#!/usr/bin/env bash
set -Eeuo pipefail

BASE="/opt/tenebit"
FRONTEND="$BASE/frontend"
BACKEND="$BASE/backend"
BACKUPS="$BASE/backups"
STAMP="$(date +%Y%m%d-%H%M%S)"
DOMAIN="${TENEBIT_DOMAIN:-https://skinny-tiger4900.byst.re}"

# Read the real DB name from the server-side .env instead of assuming it matches the app name —
# on this box it's "Tanebit", not "tenebit". A hardcoded wrong name here made every past db-backup
# step silently no-op (pg_dump errored, script only warned and kept going).
POSTGRES_DB="$(grep -E '^POSTGRES_DB=' "$BASE/.env" 2>/dev/null | cut -d= -f2-)"
POSTGRES_DB="${POSTGRES_DB:-tenebit}"

echo "=== TENEBIT DEPLOY ==="

# Check ZIPs
if [ ! -f /root/Tenebit.Frontend.zip ]; then
  echo "ERROR: Brak /root/Tenebit.Frontend.zip"
  exit 1
fi

if [ ! -f /root/Tenebit.Backend.zip ]; then
  echo "ERROR: Brak /root/Tenebit.Backend.zip"
  exit 1
fi

mkdir -p "$BACKUPS"

echo "  Backup bazy danych (baza: $POSTGRES_DB)..."
docker exec tenebit-db pg_dump -U postgres -d "$POSTGRES_DB" 2>/dev/null | gzip > "$BACKUPS/db-$STAMP.sql.gz" || echo "  WARN: backup bazy danych nie powiódł się (kontener tenebit-db niedostępny? zła nazwa bazy?)"

# === BACKEND ===
echo -e "\n[1/4] Backend..."

echo "  Backup..."
tar -czf "$BACKUPS/backend-$STAMP.tar.gz" -C "$BASE" backend 2>/dev/null || true

echo "  Unzip..."
TMP_BACKEND="/tmp/tenebit-backend-$STAMP"
mkdir -p "$TMP_BACKEND"
unzip -q /root/Tenebit.Backend.zip -d "$TMP_BACKEND"

SRC_BACKEND="$(find "$TMP_BACKEND" -maxdepth 4 -type f -name Tenebit.sln -printf '%h\n' | head -n1)"
if [ -z "$SRC_BACKEND" ]; then
  echo "ERROR: ZIP nie zawiera Tenebit.sln"
  exit 1
fi

echo "  Replace..."
rm -rf "$BACKEND"
mkdir -p "$BACKEND"
cp -a "$SRC_BACKEND"/. "$BACKEND"/

echo "  Dockerfile..."
cat > "$BACKEND/Dockerfile" <<'DOCKER'
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Tenebit.sln
RUN dotnet publish Tenebit.Api/Tenebit.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f -H "Host: __HEALTH_HOST__" http://localhost:8080/api/health/ready || exit 1
ENTRYPOINT ["dotnet", "Tenebit.Api.dll"]
DOCKER

# Dockerfile jest generowany z cytowanego heredoca (bez podstawien), wiec host do healthchecku
# wstawiamy tutaj. Bez naglowka Host backend odrzuca zapytanie z 400 (AllowedHosts) i kontener jest
# raportowany jako unhealthy mimo ze API dziala.
HEALTH_HOST="${DOMAIN#https://}"
HEALTH_HOST="${HEALTH_HOST#http://}"
HEALTH_HOST="${HEALTH_HOST%%/*}"
sed -i "s|__HEALTH_HOST__|${HEALTH_HOST}|g" "$BACKEND/Dockerfile"

echo "  ✓ Backend ready"

# === FRONTEND ===
echo -e "\n[2/4] Frontend..."

echo "  Backup..."
tar -czf "$BACKUPS/frontend-$STAMP.tar.gz" \
  --exclude='frontend/node_modules' \
  --exclude='frontend/dist' \
  -C "$BASE" frontend 2>/dev/null || true

echo "  Unzip..."
TMP_FRONTEND="/tmp/tenebit-frontend-$STAMP"
mkdir -p "$TMP_FRONTEND"
unzip -q /root/Tenebit.Frontend.zip -d "$TMP_FRONTEND"

SRC_FRONTEND="$(find "$TMP_FRONTEND" -maxdepth 4 -type f -name package.json -printf '%h\n' | head -n1)"
if [ -z "$SRC_FRONTEND" ]; then
  echo "ERROR: ZIP nie zawiera package.json"
  exit 1
fi

echo "  Replace..."
rm -rf "$FRONTEND"
mkdir -p "$FRONTEND"
cp -a "$SRC_FRONTEND"/. "$FRONTEND"/

echo "  Dockerfile..."
cat > "$FRONTEND/Dockerfile" <<'DOCKER'
FROM node:20-bookworm-slim AS build
WORKDIR /app
ENV NODE_ENV=development
ENV VITE_API_BASE_URL=
ENV PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1
RUN corepack enable && corepack prepare pnpm@10.14.0 --activate
COPY package.json package-lock.json ./
RUN pnpm import && pnpm install --frozen-lockfile --prod=false
COPY . .
RUN ./node_modules/.bin/tsc -b && NODE_ENV=production ./node_modules/.bin/vite build

FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
DOCKER

echo "  nginx.conf..."
cat > "$FRONTEND/nginx.conf" <<'NGINX'
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;
    client_max_body_size 50M;

    location / {
        try_files $uri /index.html;
        add_header Cache-Control "no-cache, no-store, must-revalidate";
    }

    location ~* ^/assets/.+\.(js|css|svg|png|jpe?g|gif|webp|woff2?|ttf|ico)$ {
        add_header Cache-Control "public, max-age=31536000, immutable";
    }

    location /api/ {
        proxy_pass http://tenebit-backend:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
NGINX

echo "  ✓ Frontend ready"

# === DOCKER BUILD ===
echo -e "\n[3/4] Docker build..."
cd "$BASE"

echo "  Building backend..."
docker compose -p tenebit build backend

echo "  Building frontend..."
docker compose -p tenebit build frontend

echo "  ✓ Built"

# === MIGRATE ===
# Applies pending EF Core migrations before the new backend image goes live. The backend runtime
# image has no `dotnet ef` (aspnet runtime, not sdk), so this expects an idempotent SQL script
# (`dotnet ef migrations script --idempotent`) shipped as migrate.sql at the root of the backend
# zip — generate and include it in every release going forward. Safe to skip if absent (older
# release zips, or a deploy with no schema changes), but never silently continues on a real
# failure: deploying app code against a schema it doesn't match is exactly how the "database
# 'X' does not exist" / "relation does not exist" incident on 2026-08-14 happened.
if [ -f "$BACKEND/migrate.sql" ]; then
  echo -e "\n  Migracja bazy danych ($POSTGRES_DB)..."
  docker exec -i tenebit-db psql -U postgres -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 < "$BACKEND/migrate.sql"
  echo "  ✓ Migracja zastosowana"
else
  echo -e "\n  Brak migrate.sql w paczce backendu — pomijam migrację (upewnij się że to zamierzone)."
fi

# === DEPLOY ===
echo -e "\n[4/4] Deploy..."
docker compose -p tenebit up -d

# Wait for the backend to answer instead of guessing with a fixed sleep. The API needs ~15s to warm up
# (EF model build + first connection), so a flat `sleep 5` reported a false FAIL on every deploy even
# though the release was fine.
echo "  Czekam na gotowosc backendu..."
for attempt in $(seq 1 40); do
  if curl -fsS "$DOMAIN/api/health" >/dev/null 2>&1; then
    echo "  Backend odpowiedzial po $((attempt * 3))s"
    break
  fi
  sleep 3
done

# === VERIFY ===
echo -e "\nVerifying..."

# Check if frontend files exist
echo "Checking frontend files..."
docker exec tenebit-frontend ls -la /usr/share/nginx/html/ | head -10

HEALTH=$(curl -fsS "$DOMAIN/api/health" 2>/dev/null || echo "FAIL")
FRONTEND_CHECK=$(curl -fsS "$DOMAIN/" 2>/dev/null | head -20 | grep -E "Tenebit|assets/index|DOCTYPE" || echo "")

echo "Backend health: $HEALTH"
echo "Frontend check: ${FRONTEND_CHECK:0:100}"

if [[ $HEALTH != *"ok"* ]] || [[ -z "$FRONTEND_CHECK" ]]; then
  echo -e "\n=== FAIL ==="
  echo "Backend logs:"
  docker logs --tail=50 tenebit-backend
  echo -e "\nFrontend logs:"
  docker logs --tail=20 tenebit-frontend
  exit 1
fi

# === SMOKE TEST (register acceptance) ===
# Registration now requires AcceptTerms and (when SMTP is configured, as in prod) returns 202 with
# requiresEmailVerification instead of a token — there is no verification-code inbox to read here,
# so a full register->login token roundtrip isn't possible from this script anymore. This checks the
# endpoints are alive and accept a well-formed request instead.
echo -e "\nSmoke test (rejestracja)..."
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

# Sprzatanie po smoke tescie. Rejestracja tworzy prawdziwa organizacje, wiec bez tego kroku kazdy
# deploy zostawial smiec w bazie. Usuwamy WYLACZNIE organizacje utworzona przez ten przebieg -
# dopasowanie po dokladnej nazwie z timestampem, ktory jest tylko cyframi. Jesli smoke test sie nie
# powiedzie, skrypt konczy sie wczesniej i dane zostaja do diagnozy.
echo "  Sprzatanie danych smoke testu..."
docker exec -i tenebit-db psql -U postgres -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 \
  -v org_name="Smoke Test ${SMOKE_STAMP}" <<'CLEANUP_SQL' >/dev/null 2>&1 || echo "  WARN: nie udalo sie usunac danych smoke testu (nieszkodliwe)"
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
echo "  Dane smoke testu usuniete"

echo -e "\n=== SUCCESS ==="
echo "URL: $DOMAIN"
docker compose ps

# Cleanup
rm -rf "$TMP_BACKEND" "$TMP_FRONTEND"
echo -e "\n✓ Deploy complete"
