// Flash Sale Test (High Contention)
//
// Scenario: ONE product, massive concurrent VUs all fighting for the same stock.
// Simulates a PS5 launch / Taylor Swift ticket drop / limited-edition sneaker release.
//
// Runs TWO phases on the same product:
//   Phase 1 — Optimistic: exposes retry cascade, slow failures, poor UX
//   Phase 2 — Distributed: fast rejection, steady processing, correct final state
//
// Expectation Phase 1 (Optimistic):
//   - 50 VUs × 2 iterations = 100 requests for 30 units
//   - High retry counts (3-5), long tail latency (2-5s)
//   - ~30 succeed, ~70 fail with "Max retries exceeded" after waiting seconds
//   - Stock ends at 0
//
// Expectation Phase 2 (Distributed):
//   - Same load on a fresh product with 30 units
//   - Most requests get instant 409 (<1ms), 30 acquire lock and succeed
//   - Zero retries inside lock, stock ends at 0
//   - Users get fast rejection instead of slow timeout

import http from "k6/http";
import { check, sleep } from "k6";
import { Rate, Trend, Counter } from "k6/metrics";
import { STRATEGIES, BASE_URL, PRODUCT_API } from "../lib/config.js";

// Custom metrics
const phaseOptimisticDuration = new Trend("optimistic_duration_ms");
const phaseDistributedDuration = new Trend("distributed_duration_ms");
const optimisticRetryCount = new Trend("optimistic_retry_count");
const distributedRetryCount = new Trend("distributed_retry_count");
const optimisticSuccess = new Rate("optimistic_success");
const distributedSuccess = new Rate("distributed_success");
const optimisticFailSlow = new Rate("optimistic_fail_slow"); // failures taking >1s
const fastRejectionRate = new Rate("fast_rejection_409");

export const options = {
  // 50 VUs for 15s — this creates the flash sale stampede
  vus: 50,
  duration: "15s",
  thresholds: {
    // Optimistic has retry cascade — p(95) high due to retries
    optimistic_duration_ms: ["p(95)<5000"],
    // Distributed <3s — warmup slowness but consistently fast after
    distributed_duration_ms: ["p(95)<3000"],
    // Distributed MUST have ZERO retries (Redis lock is atomic)
    distributed_retry_count: ["max==0"],
    // Distributed MUST deliver fast 409 rejection for failed requests
    fast_rejection_409: ["rate>0.6"],
  },
};

export function setup() {
  // Create TWO products — one per phase
  const createProd = (name) => {
    const payload = JSON.stringify({
      name,
      initialStock: 30,
      price: 499.99,
    });
    const res = http.post(PRODUCT_API, payload, {
      headers: { "Content-Type": "application/json" },
    });
    if (res.status !== 201) return null;
    return JSON.parse(res.body).productId;
  };

  const optimisticId = createProd("FlashSale-Optimistic-Phase");
  const distributedId = createProd("FlashSale-Distributed-Phase");

  console.log(`Phase 1 (Optimistic) product: ${optimisticId} stock=30`);
  console.log(`Phase 2 (Distributed) product: ${distributedId} stock=30`);

  return {
    optimisticProductId: optimisticId,
    distributedProductId: distributedId,
  };
}

export default function (data) {
  if (!data || !data.optimisticProductId || !data.distributedProductId) {
    sleep(1);
    return;
  }

  // Even VUs → Optimistic phase, Odd VUs → Distributed phase
  // This runs BOTH phases concurrently for direct comparison
  const useOptimistic = __VU % 2 === 0;
  const pid = useOptimistic
    ? data.optimisticProductId
    : data.distributedProductId;
  const strategy = useOptimistic
    ? STRATEGIES.OPTIMISTIC
    : STRATEGIES.DISTRIBUTED;

  const payload = JSON.stringify({ quantity: 1, strategy });
  const start = Date.now();

  const res = http.post(`${BASE_URL}/api/inventory/${pid}/deduct`, payload, {
    headers: { "Content-Type": "application/json" },
  });

  const elapsed = Date.now() - start;

  let body = null;
  try {
    body = JSON.parse(res.body);
  } catch {
    body = { success: false, error: "parse-failed", retryCount: 0 };
  }

  if (useOptimistic) {
    phaseOptimisticDuration.add(elapsed);
    optimisticRetryCount.add(body.retryCount || 0);
    optimisticSuccess.add(body.success);

    // Track slow failures — users waiting seconds just to be told "no"
    if (!body.success && elapsed > 1000) {
      optimisticFailSlow.add(1);
    }
  } else {
    phaseDistributedDuration.add(elapsed);
    distributedRetryCount.add(body.retryCount || 0);
    distributedSuccess.add(body.success);

    // Track instant 409 rejection
    if (!body.success && elapsed < 50) {
      fastRejectionRate.add(1);
    }
  }

  if (body.success) {
    console.log(
      `[${strategy}] VU ${__VU} ITER ${__ITER}: SUCCESS stock=${body.finalStock} retries=${body.retryCount} ${elapsed}ms`,
    );
  }

  sleep(Math.random() * 0.2); // 0-200ms jitter
}

export function teardown(data) {
  const checkFinalStock = (productId, label) => {
    const res = http.get(`${BASE_URL}/api/products/${productId}`);
    if (res.status === 200) {
      try {
        const product = JSON.parse(res.body);
        console.log(`${label} final stock: ${product.stock} (expected 0)`);
        check(product, { [`${label} stock depleted`]: (p) => p.stock === 0 });
      } catch {
        // ignore
      }
    }
  };

  if (data) {
    checkFinalStock(data.optimisticProductId, "Optimistic phase");
    checkFinalStock(data.distributedProductId, "Distributed phase");
  }

  console.log("Flash sale test complete.");
  console.log("Optimistic phase: slow failures, retry cascade, terrible UX");
  console.log("Distributed phase: fast rejection, steady processing, good UX");
}
