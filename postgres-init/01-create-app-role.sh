#!/usr/bin/env bash
set -euo pipefail

# The official image runs this file only when postgres_data is empty. psql variables quote both
# identifiers and passwords safely, including values containing spaces or shell punctuation.
: "${POSTGRES_APP_USERNAME:?POSTGRES_APP_USERNAME is required}"
: "${POSTGRES_APP_PASSWORD:?POSTGRES_APP_PASSWORD is required}"

psql --set ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --set=app_user="$POSTGRES_APP_USERNAME" --set=app_password="$POSTGRES_APP_PASSWORD" <<-'EOSQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'app_user', :'app_password') \gexec
SELECT format('ALTER DATABASE %I OWNER TO %I', current_database(), :'app_user') \gexec
SELECT format('GRANT ALL ON SCHEMA public TO %I', :'app_user') \gexec
EOSQL
