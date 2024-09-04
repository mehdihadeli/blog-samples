#!/bin/bash

DB1_PORT=27018
DB2_PORT=27019
DB3_PORT=27020

LOCAL_HOST="${HOST:-localhost}"
REPLICA_SET="${REPLICA_SET_NAME:-mongo-replicaset}"

MEMBER_1="{\"_id\": 1, \"host\": \"${LOCAL_HOST}:${DB1_PORT}\", \"priority\": 2 }"
MEMBER_2="{\"_id\": 2, \"host\": \"${LOCAL_HOST}:${DB2_PORT}\", \"priority\": 0 }"
MEMBER_3="{\"_id\": 3, \"host\": \"${LOCAL_HOST}:${DB3_PORT}\", \"priority\": 0 }"

# # mongosh using eval
# mongosh "mongodb://${LOCAL_HOST}:${DB1_PORT}" --eval "
# rs.initiate({
#   _id: '${REPLICA_SET}',
#   members: [
#     ${MEMBER_1},
#     ${MEMBER_2},
#     ${MEMBER_3}
#   ]
# });
# "

# mongosh using js
mongosh "mongodb://${LOCAL_HOST}:${DB1_PORT}" ./scripts/init-replica-set.js