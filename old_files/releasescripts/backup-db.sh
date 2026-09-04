#!/usr/bin/env bash
set -Eeuo pipefail

BASE="/opt/tenebit"
BACKUPS="$BASE/backups/db"
RETENTION_DAYS="${TENEBIT_BACKUP_RETENTION_DAYS:-14}"
STAMP="$(date +%Y%m%d-%H%M%S)"

# Real DB name on this server is "Tanebit", not "tenebit" — read it from .env instead of
# assuming it matches the app name (a hardcoded wrong name here made this a silent no-op).
POSTGRES_DB="$(grep -E '^POSTGRES_DB=' "$BASE/.env" 2>/dev/null | cut -d= -f2-)"
POSTGRES_DB="${POSTGRES_DB:-tenebit}"

mkdir -p "$BACKUPS"

echo "=== TENEBIT DB BACKUP ($STAMP, baza: $POSTGRES_DB) ==="
docker exec tenebit-db pg_dump -U postgres -d "$POSTGRES_DB" | gzip > "$BACKUPS/db-$STAMP.sql.gz"
echo "  Saved: $BACKUPS/db-$STAMP.sql.gz"

echo "  Cleaning up backups older than $RETENTION_DAYS days..."
find "$BACKUPS" -name 'db-*.sql.gz' -mtime "+$RETENTION_DAYS" -delete

echo "=== DONE ==="

# Schedule daily via cron, e.g.:
#   crontab -e
#   0 3 * * * /opt/tenebit/backup-db.sh >> /var/log/tenebit-backup.log 2>&1
