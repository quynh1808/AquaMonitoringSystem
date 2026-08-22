#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
test -f .env || { echo "Missing .env." >&2; exit 1; }
set -a
# shellcheck disable=SC1091
. ./.env
set +a
: "${POSTGRES_DB:?POSTGRES_DB is required}"
: "${POSTGRES_APP_USERNAME:?POSTGRES_APP_USERNAME is required}"
: "${POSTGRES_APP_PASSWORD:?POSTGRES_APP_PASSWORD is required}"

backup_dir="${1:-backups/postgresql}"
mkdir -p "$backup_dir"
chmod 700 "$backup_dir"
backup_file="$backup_dir/aquaops-$(date -u +%Y%m%dT%H%M%SZ).dump"
export PGPASSWORD="$POSTGRES_APP_PASSWORD"
trap 'unset PGPASSWORD' EXIT

# Custom format is compressed and supports selective/parallel restore. The secret is passed only
# to pg_dump's environment and the database port remains private to the Compose backend network.
docker compose exec -T -e PGPASSWORD postgresql \
  pg_dump --username "$POSTGRES_APP_USERNAME" --dbname "$POSTGRES_DB" \
  --format custom --no-owner --no-privileges >"$backup_file"
chmod 600 "$backup_file"
test -s "$backup_file" || { echo "Backup is empty." >&2; exit 1; }
echo "PostgreSQL backup created: $backup_file"
