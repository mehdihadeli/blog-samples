#!/bin/bash

DB1_PORT=27018
DB2_PORT=27019
DB3_PORT=27020

# Data directory
DB1_DATA_DIR="${PWD}/data/mongodb1"
DB2_DATA_DIR="${PWD}/data/mongodb2"
DB3_DATA_DIR="${PWD}/data/mongodb3"

# Log directory
DB1_LOG_DIR="${PWD}/log/mongodb1"
DB2_LOG_DIR="${PWD}/log/mongodb2"
DB3_LOG_DIR="${PWD}/log/mongodb3"

REPLICA_SET="${REPLICA_SET_NAME:-mongo-replicaset}"

# Create Docker network
NETWORK_NAME="mongo-cluster-network"
docker network create $NETWORK_NAME || echo "Network $NETWORK_NAME already exists."

# https://stackoverflow.com/questions/50608301/docker-mounted-volume-adds-c-to-end-of-windows-path-when-translating-from-linux
# https://nickjanetakis.com/blog/setting-up-docker-for-windows-and-wsl-to-work-flawlessly#ensure-volume-mounts-work

docker run -i --rm -d \
--name "mongod1" \
--network $NETWORK_NAME \
-p ${DB1_PORT}:27017 \
-v "${DB1_DATA_DIR}:/data/db" \
-v "${DB1_LOG_DIR}:/var/log/mongodb" \
-v "${PWD}/scripts:/scripts" \
mongo:latest \
--noauth --replSet "$REPLICA_SET" --bind_ip localhost,mongod1 --port 27017

docker run -i --rm -d \
--name "mongod2" \
--network $NETWORK_NAME \
-p ${DB2_PORT}:27017 \
-v "${DB2_DATA_DIR}:/data/db" \
-v "${DB2_LOG_DIR}:/var/log/mongodb" \
mongo:latest \
--noauth --replSet "$REPLICA_SET" --bind_ip localhost,mongod2 --port 27017

docker run -i --rm -d \
--name "mongod3" \
--network $NETWORK_NAME \
-p ${DB3_PORT}:27017 \
-v "${DB3_DATA_DIR}:/data/db" \
-v "${DB3_LOG_DIR}:/var/log/mongodb" \
mongo:latest \
--noauth --replSet "$REPLICA_SET" --bind_ip localhost,mongod3  --port 27017

# Stream logs from all containers
docker logs -f mongod1 &
docker logs -f mongod2 &
docker logs -f mongod3 &

# Wait for background log processes
wait