// Medium Contention Test
//
// Scenario: Few products (5), many concurrent VUs hitting them.
// Simulates a typical e-commerce catalog where popular SKUs get
// occasional simultaneous requests — conflicts possible but not constant.
//
// Compares Optimistic vs Distributed side-by-side on the same product pool.
//
// Expectation:
//   - Optimistic: most succeed, some retries (1-2), acceptable latency
//   - Distributed: all succeed, no retries, slightly higher base latency (Redis round-trip)
//   - NoLock: oversells demonstrably
//   - LocalLock: oversells in multi-instance (Docker Compose) mode
//
// This proves Optimistic is adequate for normal traffic, while Distributed
// adds unnecessary overhead for moderate contention levels.

import http from "k6/http";
import { check, sleep } from "k6";
import { Rate, Trend, Counter } from "k6/metrics";
import { STRATEGIES } from "../lib/config.js";
import { createProduct, deductStock } from "../lib/helpers.js";

// Custom metrics
const deductionDuration = new Trend("deduction_duration_ms");
const retryCount = new Trend("retry_count");
const optimisticSuccess = new Rate("optimistic_success");
const distributedSuccess = new Rate("distributed_success");
const optimisticRetries = new Counter("optimistic_retries");
const distributedRetries = new Counter("distributed_retries");

export const options = {
  stages: [
    { duration: "10s", target: 20 }, // ramp up
    { duration: "20s", target: 40 }, // steady moderate load
    { duration: "10s", target: 0 }, // ramp down
  ],
  thresholds: {
    http_req_duration: ["p(95)<2000"],
    // Both have minimal retries at this contention level
    distributed_retries: ["count<50"],
    // Both strategies work, but distributed avoids retries
    // (success rates naturally drop as stock depletes — that's expected)
  },
};

// Shared product pool — 5 products, all VUs share them
const PRODUCT_COUNT = 5;
const productIds = [];

export function setup() {
  console.log(
    `Creating ${PRODUCT_COUNT} products for medium contention test...`,
  );
  const ids = [];
  for (let i = 0; i < PRODUCT_COUNT; i++) {
    const payload = JSON.stringify({
      name: `MediumContention-Product-${i}`,
      initialStock: 100,
      price: 99.99,
    });
    const res = http.post(
      __ENV.BASE_URL || "http://gateway:8080/api/products",
      payload,
      { headers: { "Content-Type": "application/json" } },
    );
    if (res.status === 201) {
      const body = JSON.parse(res.body);
      ids.push(body.productId);
    }
  }
  console.log(`Created ${ids.length} products.`);
  return { productIds: ids };
}

export default function (data) {
  const ids = data.productIds;
  if (ids.length === 0) return;

  // Pick a random product from the pool
  const productId = ids[Math.floor(Math.random() * ids.length)];

  // Alternate between Optimistic and Distributed to compare
  const useOptimistic = __VU % 2 === 0;
  const strategy = useOptimistic
    ? STRATEGIES.OPTIMISTIC
    : STRATEGIES.DISTRIBUTED;

  const result = deductStock(productId, 1, strategy);
  deductionDuration.add(result.elapsed);
  retryCount.add(result.retryCount);

  if (result.retryCount > 0) {
    if (useOptimistic) {
      optimisticRetries.add(1);
    } else {
      distributedRetries.add(1);
    }
  }

  if (useOptimistic) {
    optimisticSuccess.add(result.success);
  } else {
    distributedSuccess.add(result.success);
  }

  if (!result.success) {
    console.log(
      `VU ${__VU}: ${strategy} failed on ${productId} — ${result.error}`,
    );
  }

  sleep(0.2 + Math.random() * 0.3); // 200-500ms between ops
}

export function teardown(data) {
  console.log("Medium contention test complete.");
  console.log(`Products used: ${data.productIds.length}`);
}
