#!/usr/bin/env bash
set -Eeuo pipefail

BASE="/opt/tenebit"
FRONTEND="$BASE/frontend"
BACKEND="$BASE/backend"
BACKUPS="$BASE/backups"
STAMP="$(date +%Y%m%d-%H%M%S)"
DOMAIN="${TENEBIT_DOMAIN:-https://skinny-tiger4900.byst.re}"

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

echo "  Backup bazy danych..."
docker exec tenebit-db pg_dump -U postgres tenebit 2>/dev/null | gzip > "$BACKUPS/db-$STAMP.sql.gz" || echo "  WARN: backup bazy danych nie powiódł się (kontener tenebit-db niedostępny?)"

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
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tenebit.Api.dll"]
DOCKER

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
RUN corepack enable && corepack prepare pnpm@10.14.0 --activate
COPY package.json package-lock.json ./
RUN pnpm import && pnpm install --frozen-lockfile --prod=false
COPY . .
RUN ./node_modules/.bin/tsc -b && ./node_modules/.bin/vite build

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
        try_files $uri $uri/ /index.html;
        add_header Cache-Control "no-cache, no-store, must-revalidate";
    }

    location /assets/ {
        add_header Cache-Control "public, max-age=31536000, immutable";
    }

    location /api/ {
        proxy_pass http://backend:8080;
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

# === DEPLOY ===
echo -e "\n[4/4] Deploy..."
docker compose -p tenebit up -d

sleep 5

# === VERIFY ===
echo -e "\nVerifying..."

# Check if frontend files exist
echo "Checking frontend files..."
docker exec tenebit-frontend ls -la /usr/share/nginx/html/ | head -10

HEALTH=$(curl -fsS "$DOMAIN/api/health" 2>/dev/null || echo "FAIL")
FRONTEND_CHECK=$(curl -fsS "$DOMAIN/" 2>/dev/null | head -20 | grep -E "Tenebit|assets/index|DOCTYPE" || echo "")

echo "Backend health: $HEALTH"
echo "Frontend check: ${FRONTEND_CHECK:0:100}"

if [[ $HEALTH == *"ok"* ]] && [[ -n "$FRONTEND_CHECK" ]]; then
  echo -e "\n=== SUCCESS ==="
  echo "URL: $DOMAIN"
  docker compose ps
else
  echo -e "\n=== FAIL ==="
  echo "Backend logs:"
  docker logs --tail=50 tenebit-backend
  echo -e "\nFrontend logs:"
  docker logs --tail=20 tenebit-frontend
  exit 1
fi

# Cleanup
rm -rf "$TMP_BACKEND" "$TMP_FRONTEND"
echo -e "\n✓ Deploy complete"
