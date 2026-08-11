#!/usr/bin/env bash
set -euo pipefail

# One-time preparation for Ubuntu Server 24.04. The script intentionally does not install Docker;
# use Docker's official apt repository first, then run this script as the deployment user.
cd "$(dirname "$0")/.."
test -f .env || { echo "Missing .env (copy and edit .env.example first)." >&2; exit 1; }

set -a
# shellcheck disable=SC1091
. ./.env
set +a

: "${MQTT_USERNAME:?MQTT_USERNAME is required}"
: "${MQTT_PASSWORD:?MQTT_PASSWORD is required}"
: "${AQUAOPS_VERSION:?AQUAOPS_VERSION is required}"
: "${AQUAOPS_DOMAIN:?AQUAOPS_DOMAIN is required}"
: "${ACME_EMAIL:?ACME_EMAIL is required}"

mkdir -p secrets
touch secrets/mosquitto_passwords
chmod 600 secrets/mosquitto_passwords

# Generate the broker password file without committing clear-text credentials or hashes to Git.
docker run --rm \
  -e MQTT_USERNAME -e MQTT_PASSWORD \
  -v "$PWD/secrets:/work" \
  --entrypoint sh "${MOSQUITTO_IMAGE:-eclipse-mosquitto:2}" \
  -c 'mosquitto_passwd -b /work/mosquitto_passwords "$MQTT_USERNAME" "$MQTT_PASSWORD"'

docker compose config --quiet
echo "Ubuntu deployment prerequisites are ready. Run: ./scripts/deploy-ubuntu.sh $AQUAOPS_VERSION"
