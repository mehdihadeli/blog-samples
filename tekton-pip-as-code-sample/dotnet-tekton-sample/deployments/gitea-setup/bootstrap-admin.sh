#!/bin/bash
set -euo pipefail

source ./setup

su-exec "$USER" gitea migrate
users=$(su-exec "$USER" gitea admin user list)
if ! awk -v username="$GITEA_ADMIN_USERNAME" 'NR > 1 && $2 == username { found = 1 } END { exit !found }' <<< "$users"; then
    su-exec "$USER" gitea admin user create \
        --username "$GITEA_ADMIN_USERNAME" \
        --password "$GITEA_ADMIN_PASSWORD" \
        --email "$GITEA_ADMIN_EMAIL" \
        --admin \
        --must-change-password=false
fi

unset GITEA_ADMIN_PASSWORD
cd /app/gitea
exec su-exec "$USER" /usr/local/bin/gitea web