#!/bin/bash
# Run flash sale test in isolation (most impactful demo)
# Shows the difference between Optimistic and Distributed under high contention
# Run from the deployments directory:
#   cd deployments && bash k6/run-flash-sale.sh

set -e

COMPOSE_CMD="docker compose -f docker-compose.yaml -f docker-compose.k6.yml"

echo "==================================================="
echo " Flash Sale Demo - Optimistic vs Distributed"
echo "==================================================="
echo ""
echo "This test proves why distributed locks are needed for flash sales."
echo "Optimistic concurrency causes slow failures (retry cascade),"
echo "while Distributed lock gives fast rejection + steady processing."
echo ""
echo "Make sure infrastructure is running:"
echo "  docker compose -f docker-compose.yaml up -d"
echo ""

$COMPOSE_CMD run --rm k6-flash-sale

echo ""
echo "Done."
