#!/usr/bin/env bash
set -Eeuo pipefail

BACKUP_FILE="${1:-}"
FORCE="${2:-}"

if [ -z "$BACKUP_FILE" ]; then
  echo "Użycie: ./restore-db.sh <ścieżka-do-backupu.sql.gz> [--force]"
  echo
  echo "Dostępne backupy:"
  ls -1t /opt/tenebit/backups/db/*.sql.gz 2>/dev/null | head -10
  ls -1t /opt/tenebit/backups/db-*.sql.gz 2>/dev/null | head -10
  exit 1
fi

if [ ! -f "$BACKUP_FILE" ]; then
  echo "ERROR: Plik nie istnieje: $BACKUP_FILE"
  exit 1
fi

if [ "$FORCE" != "--force" ]; then
  echo "UWAGA: To NADPISZE całą bazę 'tenebit' danymi z: $BACKUP_FILE"
  echo "Wszystkie zmiany od czasu tego backupu zostaną utracone."
  read -r -p "Wpisz 'tak' aby kontynuować: " CONFIRM
  if [ "$CONFIRM" != "tak" ]; then
    echo "Przerwano."
    exit 1
  fi
fi

echo "=== TENEBIT DB RESTORE ==="

echo "  Zatrzymuję backend, żeby nie pisał do bazy w trakcie przywracania..."
docker stop tenebit-backend 2>/dev/null || true

echo "  Przywracam bazę z $BACKUP_FILE..."
gunzip -c "$BACKUP_FILE" | docker exec -i tenebit-db psql -U postgres -d tenebit

echo "  Uruchamiam backend..."
docker start tenebit-backend 2>/dev/null || true

echo "=== DONE ==="
echo "Sprawdź stan: curl -s http://localhost:8080/api/health/ready"
