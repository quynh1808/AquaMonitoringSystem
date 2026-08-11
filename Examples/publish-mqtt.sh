#!/usr/bin/env sh
set -eu
BROKER="${1:-localhost}"
TOPIC="${2:-aquaculture/farm_main/pond_01/sensors/do_01/telemetry}"
mosquitto_pub -h "$BROKER" -p 1883 -t "$TOPIC" -f "$(dirname "$0")/mqtt-payload.json"
