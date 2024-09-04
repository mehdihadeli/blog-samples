#!/bin/bash

DB1_PORT=27018
DB2_PORT=27019
DB3_PORT=27020

# Data directory
DB1_DATA_DIR="./data/mongodb1"
DB2_DATA_DIR="./data/mongodb2"
DB3_DATA_DIR="./data/mongodb3"

# Log directory
DB1_LOG_DIR="./log/mongodb1"
DB2_LOG_DIR="./log/mongodb2"
DB3_LOG_DIR="./log/mongodb3"

REPLICA_SET="${REPLICA_SET_NAME:-mongo-replicaset}"

# Create data and log directories if they do not exist
mkdir -p "$DB1_DATA_DIR" "$DB2_DATA_DIR" "$DB3_DATA_DIR"
mkdir -p "$DB1_LOG_DIR" "$DB2_LOG_DIR" "$DB3_LOG_DIR"

mongod --noauth --dbpath ${DB1_DATA_DIR}  --port ${DB1_PORT} --bind_ip "localhost,mongod1" --replSet "$REPLICA_SET" & MONGOD1_PID=$! # --logpath ${DB1_LOG_DIR}/mongod.log
mongod --noauth --dbpath ${DB2_DATA_DIR}  --port ${DB2_PORT} --bind_ip "localhost,mongod2" --replSet "$REPLICA_SET" & MONGOD2_PID=$! # --logpath ${DB2_LOG_DIR}/mongod.log
mongod --noauth --dbpath ${DB3_DATA_DIR}  --port ${DB3_PORT} --bind_ip "localhost,mongod3" --replSet "$REPLICA_SET" & MONGOD3_PID=$! # --logpath ${DB3_LOG_DIR}/mongod.log

# Wait for MongoDB processes to finish
wait $MONGOD1_PID
wait $MONGOD2_PID
wait $MONGOD3_PID