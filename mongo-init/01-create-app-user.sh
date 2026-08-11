#!/usr/bin/env bash
set -euo pipefail

: "${MONGO_APP_DATABASE:=aquaculture_monitoring}"
: "${MONGO_APP_USERNAME:=aquaops_app}"
: "${MONGO_APP_PASSWORD:?MONGO_APP_PASSWORD must be set}"

mongosh --quiet \
  --username "$MONGO_INITDB_ROOT_USERNAME" \
  --password "$MONGO_INITDB_ROOT_PASSWORD" \
  --authenticationDatabase admin \
  --eval '
    const target = db.getSiblingDB(process.env.MONGO_APP_DATABASE || "aquaculture_monitoring");
    const username = process.env.MONGO_APP_USERNAME || "aquaops_app";
    const password = process.env.MONGO_APP_PASSWORD;
    if (!target.getUser(username)) {
      target.createUser({
        user: username,
        pwd: password,
        roles: [{ role: "readWrite", db: target.getName() }]
      });
    }
  '
