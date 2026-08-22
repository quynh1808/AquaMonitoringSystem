#!/usr/bin/env bash
set -euo pipefail

# Local operator recovery. The password is read without terminal echo and passed only to the
# short-lived recovery container; it is never written to .env, command history or application log.
cd "$(dirname "$0")/.."
test -f .env || { echo "Missing .env." >&2; exit 1; }
docker compose ps --status running --services | grep -qx postgresql || {
  echo "PostgreSQL service is not running." >&2
  exit 1
}

read -r -p "Administrator username [admin]: " AQUAOPS_RESET_USERNAME
AQUAOPS_RESET_USERNAME="${AQUAOPS_RESET_USERNAME:-admin}"
read -r -s -p "New password (at least 12 characters): " AQUAOPS_RESET_PASSWORD
echo
read -r -s -p "Confirm new password: " confirmation
echo

if [[ ${#AQUAOPS_RESET_PASSWORD} -lt 12 ]]; then
  echo "Password must contain at least 12 characters." >&2
  exit 1
fi
if [[ "$AQUAOPS_RESET_PASSWORD" != "$confirmation" ]]; then
  echo "Password confirmation does not match." >&2
  exit 1
fi

export AQUAOPS_RESET_USERNAME AQUAOPS_RESET_PASSWORD
trap 'unset AQUAOPS_RESET_PASSWORD confirmation' EXIT
docker compose run --rm --no-deps \
  -e AQUAOPS_RESET_USERNAME \
  -e AQUAOPS_RESET_PASSWORD \
  aquaops reset-admin-password

echo "Administrator password reset completed. You can sign in with the new password."
