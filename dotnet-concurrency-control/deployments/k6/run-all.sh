#!/bin/bash
# Run all k6 performance tests sequentially
# Run this from the deployments directory:
#   cd deployments && bash k6/run-all.sh

set -e

COMPOSE_CMD="docker compose -f docker-compose.yaml -f docker-compose.k6.yml"

echo "==================================================="
echo " Concurrency Control - k6 Performance Tests"
echo "==================================================="
echo ""
echo "Make sure infrastructure is running:"
echo "  docker compose -f docker-compose.yaml up -d"
echo ""

echo "[1/4] Low Contention Test (20 VUs, 15s)"
echo " Expectation: Optimistic >95% success, near-zero retries"
echo ""
$COMPOSE_CMD run --rm k6-low-contention || echo "WARNING: Low contention test reported issues."
echo ""

echo "[2/4] Medium Contention Test (ramp 20-40 VUs, 40s)"
echo " Expectation: Optimistic >90%, Distributed >99%"
echo ""
$COMPOSE_CMD run --rm k6-medium-contention || echo "WARNING: Medium contention test reported issues."
echo ""

echo "[3/4] Flash Sale Test (50 VUs, 15s, single product)"
echo " Expectation: Optimistic slow failures, Distributed fast rejection"
echo ""
$COMPOSE_CMD run --rm k6-flash-sale || echo "WARNING: Flash sale test reported issues."
echo ""

echo "[4/4] LocalLock Breakage Test (30 VUs, 1 product)"
echo " Expectation: Overselling detected with multiple instances"
echo ""
$COMPOSE_CMD run --rm k6-locallock-breakage || echo "WARNING: LocalLock breakage test reported issues."
echo ""

echo "==================================================="
echo " All tests complete."
echo "==================================================="
echo ""
echo "To run individual tests:"
echo "  $COMPOSE_CMD run --rm k6-low-contention"
echo "  $COMPOSE_CMD run --rm k6-medium-contention"
echo "  $COMPOSE_CMD run --rm k6-flash-sale"
echo "  $COMPOSE_CMD run --rm k6-locallock-breakage"
echo ""
echo "To view results:"
echo "  docker compose -f docker-compose.yaml logs api-1 api-2 api-3"
echo ""
