#!/bin/sh
set -e

echo "Waiting for Vault to be ready..."
for i in $(seq 1 20); do
    if vault status >/dev/null 2>&1; then
        echo "Vault is ready!"
        break
    fi
    echo "Attempt $i: Vault not ready yet..."
    sleep 3
done

echo "Enabling secrets engines..."
vault secrets enable database 2>/dev/null || true
vault secrets enable rabbitmq 2>/dev/null || true
vault auth enable approle 2>/dev/null || true

# --- Postgres ---
echo "Configuring Postgres..."
vault write database/config/postgres-db \
    plugin_name=postgresql-database-plugin \
    allowed_roles="orders-role,users-role" \
    connection_url="postgresql://{{username}}:{{password}}@postgres:5432/postgres?sslmode=disable" \
    username="vault_admin" \
    password="vaultpass"

vault write database/roles/orders-role db_name=postgres-db \
    creation_statements="CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}' INHERIT; GRANT ALL ON DATABASE orders_db TO \"{{name}}\"; GRANT ALL ON SCHEMA public TO \"{{name}}\";" \
    default_ttl="5m" max_ttl="1h"

vault write database/roles/users-role db_name=postgres-db \
    creation_statements="CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}' INHERIT; GRANT ALL ON DATABASE users_db TO \"{{name}}\"; GRANT ALL ON SCHEMA public TO \"{{name}}\";" \
    default_ttl="5m" max_ttl="1h"

# --- RabbitMQ ---
echo "Configuring RabbitMQ..."
# The RabbitMQ secrets engine requires explicit username and password fields
vault write rabbitmq/config/connection \
    connection_uri="http://admin:admin@rabbitmq:15672" \
    username="admin" \
    password="admin"

vault write rabbitmq/roles/orders-role \
    vhosts='{"/":{"write": ".*", "read": ".*", "configure": ".*"}}' \
    default_ttl="5m" max_ttl="1h"

vault write rabbitmq/roles/users-role \
    vhosts='{"/":{"write": ".*", "read": ".*", "configure": ".*"}}' \
    default_ttl="5m" max_ttl="1h"

# --- Policies ---
echo "Creating policies..."
vault policy write orders-policy - <<EOF
path "database/creds/orders-role" { capabilities = ["read"] }
path "rabbitmq/creds/orders-role" { capabilities = ["read"] }
EOF

vault policy write users-policy - <<EOF
path "database/creds/users-role" { capabilities = ["read"] }
path "rabbitmq/creds/users-role" { capabilities = ["read"] }
EOF

# --- AppRoles ---
echo "Creating AppRoles..."
vault write auth/approle/role/orders token_policies="orders-policy" token_ttl=1h
vault write auth/approle/role/users token_policies="users-policy" token_ttl=1h

# --- Generate credentials ---
echo "Generating credentials..."
mkdir -p /vault/credentials

vault read -field=role_id auth/approle/role/orders/role-id > /vault/credentials/orders-role-id
vault write -f -field=secret_id auth/approle/role/orders/secret-id > /vault/credentials/orders-secret-id
vault read -field=role_id auth/approle/role/users/role-id > /vault/credentials/users-role-id
vault write -f -field=secret_id auth/approle/role/users/secret-id > /vault/credentials/users-secret-id

# Verify files were written
echo "Verifying credential files..."
for f in orders-role-id orders-secret-id users-role-id users-secret-id; do
    if [ -s "/vault/credentials/$f" ]; then
        echo "  $f: OK ($(wc -c < /vault/credentials/$f) bytes)"
    else
        echo "  $f: FAILED - file is empty or missing!"
        exit 1
    fi
done

echo "Vault configured successfully!"