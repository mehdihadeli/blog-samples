#!/bin/bash

# Define paths to config files
CONFIG_DIR="./configs"
MONGOD1_CONF="$CONFIG_DIR/mongod1.conf"
MONGOD2_CONF="$CONFIG_DIR/mongod2.conf"
MONGOD3_CONF="$CONFIG_DIR/mongod3.conf"

# Data directory
DB1_DATA_DIR="./data/mongodb1"
DB2_DATA_DIR="./data/mongodb2"
DB3_DATA_DIR="./data/mongodb3"

# Log directory
DB1_LOG_DIR="./log/mongodb1"
DB2_LOG_DIR="./log/mongodb2"
DB3_LOG_DIR="./log/mongodb3"

# Create data and log directories if they do not exist
mkdir -p "$DB1_DATA_DIR" "$DB2_DATA_DIR" "$DB3_DATA_DIR"
mkdir -p "$DB1_LOG_DIR" "$DB2_LOG_DIR" "$DB3_LOG_DIR"

# Start MongoDB instances in the background
mongod --config "$MONGOD1_CONF" & MONGOD1_PID=$!

mongod --config "$MONGOD2_CONF" & MONGOD2_PID=$!

mongod --config "$MONGOD3_CONF" & MONGOD3_PID=$!

# Wait for MongoDB processes to finish
wait $MONGOD1_PID
wait $MONGOD2_PID
wait $MONGOD3_PID

echo "MongoDB instances started successfully."
