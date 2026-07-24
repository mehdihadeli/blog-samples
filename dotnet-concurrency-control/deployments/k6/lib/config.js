// Base configuration for k6 tests
// Point to YARP gateway when running via Docker Compose, or local dev server

const ENV = __ENV.TARGET_ENV || "docker";

const CONFIG = {
  docker: {
    baseUrl: "http://gateway:8080",
    productApi: "/api/products",
    inventoryApi: "/api/inventory",
    ordersApi: "/api/orders",
  },
  local: {
    baseUrl: "http://host.docker.internal:5262",
    productApi: "/api/products",
    inventoryApi: "/api/inventory",
    ordersApi: "/api/orders",
  },
};

const cfg = CONFIG[ENV] || CONFIG.docker;

export const BASE_URL = cfg.baseUrl;
export const PRODUCT_API = `${cfg.baseUrl}${cfg.productApi}`;
export const INVENTORY_API = `${cfg.baseUrl}${cfg.inventoryApi}`;
export const ORDERS_API = `${cfg.baseUrl}${cfg.ordersApi}`;

// Threshold defaults
export const THRESHOLDS = {
  // 95% of requests should complete under this
  httpReqDuration: ["p(95)<500"],
  // Error rate should be under this for strategies that should work
  errorRate: ["rate<0.05"],
  // No failed checks
  checkFailureRate: ["rate<0.01"],
};

// Strategy enum values sent in request body
export const STRATEGIES = {
  NO_LOCK: "NoLock",
  LOCAL_LOCK: "LocalLock",
  OPTIMISTIC: "Optimistic",
  DISTRIBUTED: "Distributed",
};
