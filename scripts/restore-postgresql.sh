#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
test -f .env || { echo "Missing .env." >&2; exit 1; }
backup_file="${1:?Usage: ./scripts/restore-postgresql.sh path/to/backup.dump}"
test -f "$backup_file" || { echo "Backup not found: $backup_file" >&2; exit 1; }
set -a
# shellcheck disable=SC1091
. ./.env
set +a
: "${POSTGRES_DB:?POSTGRES_DB is required}"
: "${POSTGRES_APP_USERNAME:?POSTGRES_APP_USERNAME is required}"
: "${POSTGRES_APP_PASSWORD:?POSTGRES_APP_PASSWORD is required}"

read -r -p "Restore will replace objects in '$POSTGRES_DB'. Type RESTORE to continue: " confirmation
[[ "$confirmation" == "RESTORE" ]] || { echo "Cancelled."; exit 1; }
export PGPASSWORD="$POSTGRES_APP_PASSWORD"
trap 'unset PGPASSWORD' EXIT
docker compose stop aquaops
docker compose exec -T -e PGPASSWORD postgresql \
  pg_restore --username "$POSTGRES_APP_USERNAME" --dbname "$POSTGRES_DB" \
  --clean --if-exists --no-owner --no-privileges <"$backup_file"
docker compose up -d aquaops
echo "Restore completed. Verify /health/ready and operational totals before accepting the database."
