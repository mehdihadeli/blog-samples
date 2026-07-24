// LocalLock Multi-Instance Breakage Test
//
// Scenario: ONE product, LocalLock strategy, multiple concurrent VUs.
// In a single-instance deployment, LocalLock protects correctly.
// With multiple instances (Docker Compose with 3 API replicas), each
// instance has its own lock object → concurrent writes → breakage.
//
// Breakage manifests as:
//   a) Unhandled DbUpdateConcurrencyException → 500 HTML (parse-failed)
//   b) Data inconsistency (final stock != 0 when 30 deducted from 30)
//   c) Potential overselling (stock goes negative, rarer with EF concurrency token)
//
// Expectation:
//   - 30 VUs × 1 deduct each on stock=30 → result should be stock=0 with 0 errors
//   - But LocalLock per-instance means failures at every level
//   - The test proves lock _localLock is process-scoped and useless for
//     multi-instance deployments (Kubernetes, Docker Compose, load-balanced)
//
// This is the k6 equivalent of the integration test:
//   Deduct_LocalLock_MultiInstance_Oversells

import http from "k6/http";
import { check, sleep } from "k6";
import { Counter, Rate } from "k6/metrics";
import { STRATEGIES, BASE_URL, PRODUCT_API } from "../lib/config.js";

const apiCrashes = new Counter("local_lock_api_crashes");
const successRate = new Rate("local_lock_success");

export const options = {
  // 30 VUs, each deducts once from the same product
  vus: 30,
  iterations: 30,
  thresholds: {
    // LocalLock breaks across instances — expect API crashes (500 HTML)
    local_lock_api_crashes: ["count>0"],
  },
};

export function setup() {
  // Create one product with 30 units
  const payload = JSON.stringify({
    name: "LocalLock-Breakage-Test",
    initialStock: 30,
    price: 99.99,
  });

  const res = http.post(PRODUCT_API, payload, {
    headers: { "Content-Type": "application/json" },
  });

  if (res.status !== 201) {
    console.error("Failed to create product:", res.status, res.body);
    return null;
  }

  const body = JSON.parse(res.body);
  console.log(`Product created: ${body.productId} with stock=30`);

  // Record initial stock
  const getRes = http.get(`${BASE_URL}/api/products/${body.productId}`);
  if (getRes.status === 200) {
    try {
      const p = JSON.parse(getRes.body);
      console.log(`Initial stock confirmed: ${p.stock}`);
    } catch {
      // ignore
    }
  }

  return { productId: body.productId };
}

export default function (data) {
  if (!data || !data.productId) {
    sleep(1);
    return;
  }

  // ALL VUs use LocalLock on the SAME product — this is the test
  const payload = JSON.stringify({
    quantity: 1,
    strategy: STRATEGIES.LOCAL_LOCK,
  });

  const res = http.post(
    `${BASE_URL}/api/inventory/${data.productId}/deduct`,
    payload,
    { headers: { "Content-Type": "application/json" } },
  );

  let body = null;
  try {
    body = JSON.parse(res.body);
  } catch {
    body = { success: false, error: "parse-failed" };
    apiCrashes.add(1);
  }

  successRate.add(body.success);

  // If we're in multi-instance mode (Docker Compose with 3 replicas),
  // some requests will succeed even though stock should be exhausted
  // because multiple instances write concurrently
  if (body.success) {
    console.log(
      `VU ${__VU}: LocalLock reported SUCCESS — stock=${body.finalStock}`,
    );
  } else {
    console.log(
      `VU ${__VU}: LocalLock rejected — stock=${body.finalStock} error=${body.error}`,
    );
  }
}

export function teardown(data) {
  if (!data || !data.productId) return;

  // Check final stock — should be 0 with proper global locking (30-30=0)
  // Any deviation proves breakage: overselling (stock<0), data loss (stock>0),
  // or API crashes (detected via apiCrashes counter)
  const res = http.get(`${BASE_URL}/api/products/${data.productId}`);
  if (res.status === 200) {
    try {
      const product = JSON.parse(res.body);
      console.log(`Final stock: ${product.stock}`);
      console.log(
        product.stock === 0
          ? "Stock correct — but only if running single-instance."
          : `PROOF: LocalLock broke! Expected stock=0, got stock=${product.stock}. Per-instance locks cause data corruption.`,
      );

      check(product, {
        "LocalLock breakage detected": (p) => p.stock !== 0,
      });
    } catch {
      // ignore
    }
  }

  console.log("LocalLock breakage test complete.");
  console.log(
    "Expected result: API crashes (500 HTML from DbUpdateConcurrencyException).",
  );
  console.log("Proof: any deviation from 30/30 success with stock=0.");
}
