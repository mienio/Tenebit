#!/usr/bin/env bash
set -Eeuo pipefail

BASE="/opt/tenebit"
BACKUPS="$BASE/backups/db"
RETENTION_DAYS="${TENEBIT_BACKUP_RETENTION_DAYS:-14}"
STAMP="$(date +%Y%m%d-%H%M%S)"

mkdir -p "$BACKUPS"

echo "=== TENEBIT DB BACKUP ($STAMP) ==="
docker exec tenebit-db pg_dump -U postgres tenebit | gzip > "$BACKUPS/db-$STAMP.sql.gz"
echo "  Saved: $BACKUPS/db-$STAMP.sql.gz"

echo "  Cleaning up backups older than $RETENTION_DAYS days..."
find "$BACKUPS" -name 'db-*.sql.gz' -mtime "+$RETENTION_DAYS" -delete

echo "=== DONE ==="

# Schedule daily via cron, e.g.:
#   crontab -e
#   0 3 * * * /opt/tenebit/backup-db.sh >> /var/log/tenebit-backup.log 2>&1
