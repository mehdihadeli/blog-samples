#!/bin/bash

echo "Executing init replica set script."

REPLICA_SET="${REPLICA_SET_NAME:-mongo-replicaset}"

MEMBER_1="{\"_id\": 1, \"host\": \"mongod1:27017\", \"priority\": 2 }"
MEMBER_2="{\"_id\": 2, \"host\": \"mongod2:27017\", \"priority\": 0 }"
MEMBER_3="{\"_id\": 3, \"host\": \"mongod3:27017\", \"priority\": 0 }"

# Because we use healthcheck and test for the container we already are inside of 'mongod' container
mongosh --quiet --eval "
try {
  rs.status();
  print('Replica set already initialized');
} catch (err) {
  print('Replica set not yet initialized, attempting to initiate...');
  rs.initiate({
    _id: \"${REPLICA_SET}\",
    members: [
      ${MEMBER_1},
      ${MEMBER_2},
      ${MEMBER_3}
    ]
  });
}
"

# # mongosh using js configuration
# mongosh /scripts/init-replica-set.js