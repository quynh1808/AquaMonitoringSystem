#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
test -f .env || { echo "Missing .env (copy and edit .env.example first)." >&2; exit 1; }

set -a
# shellcheck disable=SC1091
. ./.env
set +a

previous_revision="$(git rev-parse HEAD 2>/dev/null || true)"
previous_version="${AQUAOPS_VERSION:-}"
requested_version="${1:-$previous_version}"
: "${requested_version:?Pass an image tag, for example sha-<git-sha>.}"
: "${AQUAOPS_BIND_ADDRESS:?Set AQUAOPS_BIND_ADDRESS in .env.}"

AQUAOPS_HTTP_PORT="${AQUAOPS_HTTP_PORT:-8080}"
if [[ ! "$AQUAOPS_HTTP_PORT" =~ ^[0-9]+$ ]] || (( AQUAOPS_HTTP_PORT < 1 || AQUAOPS_HTTP_PORT > 65535 )); then
  echo "AQUAOPS_HTTP_PORT must be an integer from 1 to 65535." >&2
  exit 1
fi

# Only permit registry-safe tags before modifying the local secrets/configuration file.
if [[ ! "$requested_version" =~ ^[A-Za-z0-9._-]+$ ]]; then
  echo "Invalid image tag: $requested_version" >&2
  exit 1
fi

set_image_version() {
  if grep -q '^AQUAOPS_VERSION=' .env; then
    sed -i "s/^AQUAOPS_VERSION=.*/AQUAOPS_VERSION=$1/" .env
  else
    printf '\nAQUAOPS_VERSION=%s\n' "$1" >> .env
  fi
}

if [[ "$requested_version" != "$previous_version" ]]; then
  set_image_version "$requested_version"
fi

git fetch --tags origin
git pull --ff-only
# Reload local deployment values after updating the selected image tag.
set -a
# shellcheck disable=SC1091
. ./.env
set +a
docker compose config --quiet
docker compose pull
docker compose up -d --remove-orphans

# Validate the host-published HTTP endpoint. 0.0.0.0 is a bind wildcard, so health checks use
# loopback; a specific LAN address is checked directly.
health_host="$AQUAOPS_BIND_ADDRESS"
if [[ "$health_host" == "0.0.0.0" ]]; then
  health_host="127.0.0.1"
fi
health_url="http://${health_host}:${AQUAOPS_HTTP_PORT}/health/ready"
if ! curl --fail --silent --show-error --retry 12 --retry-delay 5 --retry-connrefused "$health_url" >/dev/null; then
  echo "Readiness failed for $requested_version." >&2
  if [[ -n "$previous_version" && "$previous_version" != "$requested_version" ]]; then
    echo "Rolling the application image back to $previous_version." >&2
    set_image_version "$previous_version"
    set -a
    # shellcheck disable=SC1091
    . ./.env
    set +a
    docker compose up -d aquaops
  fi
  exit 1
fi

echo "Deployment $requested_version is ready at http://${AQUAOPS_BIND_ADDRESS}:${AQUAOPS_HTTP_PORT}. Previous revision: ${previous_revision:-unknown}"
docker compose ps
