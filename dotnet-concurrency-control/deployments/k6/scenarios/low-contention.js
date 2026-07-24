// Low Contention Test
//
// Scenario: Many different products, low contention per product.
// Each VU creates its own product and deducts from it with each strategy.
//
// Expectation:
//   - NoLock: silently corrupts data (oversells)
//   - LocalLock: works (single instance context)
//   - Optimistic: near 100% success, zero retries
//   - Distributed: near 100% success, but overkill
//
// This proves Optimistic is the right default for low-contention CRUD.

import { check, sleep } from "k6";
import { Rate, Trend } from "k6/metrics";
import { STRATEGIES } from "../lib/config.js";
import { createProduct, deductStock, getProductStock } from "../lib/helpers.js";

// Custom metrics
const deductionDuration = new Trend("deduction_duration_ms");
const retryCountMetric = new Trend("retry_count");
const oversellDetected = new Rate("oversell_detected");
const successRate = new Rate("strategy_success");

export const options = {
  // 20 VUs for 15s — each creates own product, low contention
  vus: 20,
  duration: "15s",
  thresholds: {
    http_req_duration: ["p(95)<1000"],
    strategy_success: ["rate>0.95"], // Optimistic + Distributed should be near 100%
    oversell_detected: ["rate<0.01"], // No oversells with Optimistic/Distributed
  },
};

export default function () {
  // Each VU works on its own product — zero contention
  const productId = createProduct(`LowContention-VU${__VU}`, 50, 29.99);
  if (!productId) {
    console.error(`VU ${__VU}: failed to create product`);
    return;
  }

  // Deduct 1 unit with each strategy
  const strategies = [
    STRATEGIES.NO_LOCK,
    STRATEGIES.LOCAL_LOCK,
    STRATEGIES.OPTIMISTIC,
    STRATEGIES.DISTRIBUTED,
  ];

  for (const strategy of strategies) {
    const result = deductStock(productId, 1, strategy);
    deductionDuration.add(result.elapsed);
    retryCountMetric.add(result.retryCount);

    const succeeded = result.success;
    successRate.add(succeeded);

    if (!succeeded) {
      console.log(
        `VU ${__VU}: ${strategy} failed — ${result.error} (stock check may be insufficient)`,
      );
    }

    sleep(0.3); // pace requests
  }

  // Verify final stock: should be 46 (50 - 4 deductions)
  // NoLock may cause more deduction than expected due to race
  const finalStock = getProductStock(productId);
  if (finalStock !== 46) {
    console.log(
      `VU ${__VU}: expected stock=46, got=${finalStock} — data corruption!`,
    );
    oversellDetected.add(1);
  } else {
    oversellDetected.add(0);
  }
}

export function teardown() {
  console.log("Low contention test complete.");
}
