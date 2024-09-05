#!/bin/bash

REPLICA_SET="${REPLICA_SET_NAME:-mongo-replicaset}"

MEMBER_1="{\"_id\": 1, \"host\": \"mongod1\", \"priority\": 2 }"
MEMBER_2="{\"_id\": 2, \"host\": \"mongod2\", \"priority\": 0 }"
MEMBER_3="{\"_id\": 3, \"host\": \"mongod3\", \"priority\": 0 }"

# mongosh using eval
docker exec -it mongod1 mongosh --eval "
rs.initiate({
  _id: \"${REPLICA_SET}\",
  members: [
    ${MEMBER_1},
    ${MEMBER_2},
    ${MEMBER_3}
  ]
});
" 

# mongosh using js
# docker exec -it mongod1 mongosh /scripts/init-replica-set.js