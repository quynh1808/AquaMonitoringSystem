#!/usr/bin/env bash
set -euo pipefail

ARCHIVE="${1:?Cách dùng: ./Scripts/restore-mongodb.sh <file.archive.gz>}"
test -f "$ARCHIVE"

docker compose exec -T mongodb sh -lc '
  mongorestore \
    --username "$MONGO_INITDB_ROOT_USERNAME" \
    --password "$MONGO_INITDB_ROOT_PASSWORD" \
    --authenticationDatabase admin \
    --db "${MONGO_APP_DATABASE:-aquaculture_monitoring}" \
    --drop \
    --archive \
    --gzip
' < "$ARCHIVE"

echo "Đã phục hồi $ARCHIVE"
