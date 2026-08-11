#!/usr/bin/env bash
set -euo pipefail

BACKUP_ROOT="${BACKUP_ROOT:-./backups}"
STAMP="$(date +%Y%m%d-%H%M%S)"
DEST="$BACKUP_ROOT/$STAMP"

mkdir -p "$DEST"
docker compose exec -T mongodb sh -lc '
  mongodump \
    --username "$MONGO_INITDB_ROOT_USERNAME" \
    --password "$MONGO_INITDB_ROOT_PASSWORD" \
    --authenticationDatabase admin \
    --db "${MONGO_APP_DATABASE:-aquaculture_monitoring}" \
    --archive \
    --gzip
' > "$DEST/aquaculture_monitoring.archive.gz"

find "$BACKUP_ROOT" -mindepth 1 -maxdepth 1 -type d -mtime +"${BACKUP_RETENTION_DAYS:-30}" -exec rm -rf {} +
echo "Backup: $DEST/aquaculture_monitoring.archive.gz"
