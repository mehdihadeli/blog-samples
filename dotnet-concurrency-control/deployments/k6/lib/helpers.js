import http from "k6/http";
import { check, sleep } from "k6";
import { SharedArray } from "k6/data";
import { BASE_URL, PRODUCT_API, INVENTORY_API } from "./config.js";

// ── Product creation ──────────────────────────────────────

// Create a single product, return its ID
export function createProduct(name, initialStock, price) {
  const payload = JSON.stringify({
    name: name || `Test-Product-${__VU}-${__ITER}`,
    initialStock: initialStock || 1000,
    price: price || 49.99,
  });

  const res = http.post(PRODUCT_API, payload, {
    headers: { "Content-Type": "application/json" },
    tags: { operation: "create_product" },
  });

  check(res, {
    "create product status 201": (r) => r.status === 201,
  });

  // Response body has productId
  try {
    const body = JSON.parse(res.body);
    return body.productId;
  } catch {
    return null;
  }
}

// Create N products in sequence, return array of IDs
export function createProducts(count, baseName) {
  const ids = [];
  for (let i = 0; i < count; i++) {
    const id = createProduct(`${baseName || "Perf"}-${i}`, 1000, 49.99);
    if (id) ids.push(id);
    sleep(0.05); // small gap to avoid overwhelming startup
  }
  return ids;
}

// ── Stock deduction ───────────────────────────────────────

// Deduct stock using specified strategy
// Returns { success, status, body, elapsed }
export function deductStock(productId, quantity, strategy) {
  const payload = JSON.stringify({
    quantity: quantity || 1,
    strategy: strategy || "Optimistic",
  });

  const res = http.post(`${INVENTORY_API}/${productId}/deduct`, payload, {
    headers: { "Content-Type": "application/json" },
    tags: { strategy: strategy || "Optimistic" },
  });

  let body = null;
  try {
    body = JSON.parse(res.body);
  } catch {
    body = { success: false, error: "parse-failed" };
  }

  return {
    success: body.success,
    status: res.status,
    body,
    elapsed: body.elapsedMs || 0,
    retryCount: body.retryCount || 0,
    error: body.error || null,
  };
}

// ── Check helpers ─────────────────────────────────────────

// Verify a deduction was correct: success=true, expected status
export function checkDeductSuccess(result, strategyName) {
  return check(result.body, {
    [`${strategyName} success=true`]: (b) => b.success === true,
  });
}

// Verify a deduction was correctly rejected
export function checkDeductRejected(result, strategyName) {
  return check(result.body, {
    [`${strategyName} rejected (409)`]: (b) => b.success === false,
  });
}

// ── Data integrity ────────────────────────────────────────

// Get current stock for a product
export function getProductStock(productId) {
  const res = http.get(`${BASE_URL}/api/products/${productId}`, {
    tags: { operation: "get_product" },
  });
  if (res.status !== 200) return -1;
  try {
    return JSON.parse(res.body).stock;
  } catch {
    return -1;
  }
}
