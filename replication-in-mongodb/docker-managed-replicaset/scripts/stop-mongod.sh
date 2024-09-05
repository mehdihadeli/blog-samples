#!/bin/bash

echo "Stopping all MongoDB instances..."
docker stop mongod1 mongod2 mongod3
echo "All MongoDB instances stopped."