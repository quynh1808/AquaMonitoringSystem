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
: "${AQUAOPS_BIND_ADDRESS:?AQUAOPS_BIND_ADDRESS is required}"

if [[ "$MQTT_USERNAME" == *:* ]]; then
  echo "MQTT_USERNAME must not contain a colon." >&2
  exit 1
fi

AQUAOPS_HTTP_PORT="${AQUAOPS_HTTP_PORT:-8080}"
if [[ ! "$AQUAOPS_HTTP_PORT" =~ ^[0-9]+$ ]] || (( AQUAOPS_HTTP_PORT < 1 || AQUAOPS_HTTP_PORT > 65535 )); then
  echo "AQUAOPS_HTTP_PORT must be an integer from 1 to 65535." >&2
  exit 1
fi

mkdir -p secrets
rm -f secrets/mosquitto_passwords
touch secrets/mosquitto_passwords

# Generate the broker password file without committing clear-text credentials or hashes to Git.
# Values are passed explicitly so the command also works when Docker was installed with sudo.
# The broker runs as the image's `mosquitto` account and must be able to read the bind-mounted file.
docker run --rm \
  -e MQTT_USERNAME="$MQTT_USERNAME" \
  -e MQTT_PASSWORD="$MQTT_PASSWORD" \
  -v "$PWD/secrets:/work" \
  --entrypoint sh "${MOSQUITTO_IMAGE:-eclipse-mosquitto:2}" \
  -c 'test -n "$MQTT_USERNAME" &&
      mosquitto_passwd -b /work/mosquitto_passwords "$MQTT_USERNAME" "$MQTT_PASSWORD" &&
      chown "$(id -u mosquitto):$(id -g mosquitto)" /work/mosquitto_passwords &&
      chmod 640 /work/mosquitto_passwords'

docker compose config --quiet
echo "LAN HTTP deployment prerequisites are ready. Run: ./scripts/deploy-ubuntu.sh $AQUAOPS_VERSION"
