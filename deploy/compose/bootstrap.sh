#!/bin/sh
set -eu
umask 077
export PGPASSFILE="$(mktemp)"
trap 'rm -f "$PGPASSFILE"' EXIT
for file in postgres-admin-password identity-migrator-password identity-api-password; do
    if [ ! -r "/run/secrets/$file" ] || [ ! -s "/run/secrets/$file" ]; then
        echo "Missing, unreadable or empty secret file: $file" >&2
        exit 1
    fi
done
# The generated passwords are hexadecimal, so no pgpass delimiter escaping is needed.
printf '%s:5432:*:shipwise_admin:%s\n' "$PGHOST" "$(cat /run/secrets/postgres-admin-password)" > "$PGPASSFILE"
psql -X --no-password --username shipwise_admin --dbname postgres --set ON_ERROR_STOP=1 --file /bootstrap/bootstrap.sql
