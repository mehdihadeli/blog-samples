const LOCAL_HOST = process.env.HOST || 'localhost';
const REPLICA_SET_NAME = process.env.REPLICA_SET_NAME || 'mongo-replicaset';

const DB1_PORT = process.env.DB1_PORT || 27018;
const DB2_PORT = process.env.DB2_PORT || 27019;
const DB3_PORT = process.env.DB3_PORT || 27020;

const members = [
    { _id: 1, host: `${LOCAL_HOST}:${DB1_PORT}`, priority: 2 },
    { _id: 2, host: `${LOCAL_HOST}:${DB2_PORT}`, priority: 0 },
    { _id: 3, host: `${LOCAL_HOST}:${DB3_PORT}`, priority: 0 }
];

const config = {
    _id: REPLICA_SET_NAME,
    members: members
};

rs.initiate(config);
