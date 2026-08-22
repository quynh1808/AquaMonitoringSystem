# PostgreSQL container initialization

This directory contains one-time initialization scripts consumed by the official PostgreSQL image.
`01-create-app-role.sh` creates the least-privileged application login and transfers ownership of
the AMS database to it. Scripts run only when the `postgres_data` volume is empty; changing an
existing password therefore requires an explicit PostgreSQL `ALTER ROLE`, not editing `.env` alone.

No password is stored in this directory. Compose injects it from the uncommitted `.env` file.
