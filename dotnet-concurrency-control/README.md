# Concurrency Control in .NET: From Local Locks to Distributed Systems

A hands-on e-commerce API demonstrating four concurrency control strategies in the same codebase. Each strategy is selectable per request via a single enum parameter. Built with .NET 10, Vertical Slice Architecture, MediatR, EF Core + PostgreSQL, and Redis RedLock.

## Project Structure

```
src/ECommerce.App/
├── Inventory/
│   ├── ConcurrencyStrategy.cs          # NoLock | LocalLock | Optimistic | Distributed
│   └── Features/DeductingStock/v1/
│       ├── DeductStock.cs              # Handler implementing all 4 strategies
│       └── DeductStockEndpoints.cs     # Minimal API endpoint
├── Orders/
│   ├── Models/Order.cs
│   └── Features/
│       ├── PlacingOrder/v1/
│       └── GettingOrder/v1/
├── Products/
│   ├── Models/Product.cs
│   └── Features/
│       ├── CreatingProduct/v1/
│       ├── GettingProduct/v1/
│       └── ListingProducts/v1/
└── Shared/
    ├── Contracts/
    │   ├── IDistributedLockManager.cs  # Abstraction for distributed locking
    │   ├── IOrderStore.cs
    │   └── IProductStore.cs
    └── Data/
        ├── ECommerceDbContext.cs       # EF Core DbContext (PostgreSQL)
        ├── EfProductStore.cs           # PostgreSQL-backed product store
        ├── EfOrderStore.cs             # PostgreSQL-backed order store
        ├── InventoryStore.cs           # In-memory product store (dev)
        ├── InMemoryOrderStore.cs       # In-memory order store (dev)
        ├── RedisDistributedLockManager.cs  # RedLock via DistributedLock.Redis
        └── InMemoryDistributedLockManager.cs  # In-memory fallback (dev)

src/ECommerce.AppHost/
├── AppHost.cs                     # Aspire orchestrator: Postgres + Redis + 3 API replicas
├── appsettings.json               # Aspire dashboard logging config
└── Properties/launchSettings.json # Dashboard URLs

src/ECommerce.ServiceDefaults/
└── Extensions.cs                  # Shared Aspire defaults: OpenTelemetry, health checks, service discovery

tests/ECommerce.IntegrationTests/
├── ECommerceIntegrationTestBase.cs      # Test base with multi-instance factory helpers
├── IntegrationTestCollection.cs
├── Fixtures/
│   ├── ECommerceSharedFixture.cs        # Starts Postgres + Redis containers
│   ├── PostgresContainerFixture.cs      # PostgreSQL 17 Testcontainer
│   └── RedisContainerFixture.cs         # Redis 7.4 Testcontainer for RedLock tests
├── Inventory/Features/DeductingStock/v1/
│   └── DeductStockTests.cs             # Single-instance + multi-instance concurrency tests
└── Products/Features/CreatingProduct/v1/
    └── CreateProductTests.cs
```

## Strategies

| Strategy        | How It Works                                       | Works Best When                          | Breaks When                                     |
| --------------- | -------------------------------------------------- | ---------------------------------------- | ----------------------------------------------- |
| **NoLock**      | Read, modify, write — no synchronization           | Read-only endpoints, benchmarks          | Any concurrent write — data corrupts            |
| **LocalLock**   | `lock()` / `SemaphoreSlim` around critical section | Single-instance apps, background workers | Second instance deploys — each has its own lock |
| **Optimistic**  | Version check + retry on conflict                  | Multi-instance, low-contention CRUD      | Flash sales — retry cascade spikes latency      |
| **Distributed** | Redis RedLock external coordinator                 | High-contention inventory, cross-service | Redis down — degrades to 409 gracefully         |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for running tests or the app with Postgres/Redis)

## Quick Start

### Option A: Run with .NET Aspire (recommended)

```bash
# Start everything — Postgres, Redis, 3 API instances, Aspire Dashboard
dotnet run --project src/ECommerce.AppHost/ECommerce.AppHost.csproj
```

This launches:

- PostgreSQL 17 container
- Redis Stack 7.4 container with Redis Commander
- 3 API instances (independent processes, each with its own DI container)
- Aspire Dashboard at `https://localhost:17252` with real-time telemetry, logs, metrics, and traces

### Option B: Run standalone (Docker infra + single API)

```bash
# 1. Start infrastructure
cd deployments
docker compose up -d

# 2. Run the API
dotnet run --project src/ECommerce.App/ECommerce.App.csproj
```

The API starts on `http://localhost:5262` with Swagger UI at `/swagger`.

### 3. Create a product

```bash
curl -s -X POST http://localhost:5262/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Gaming Laptop","initialStock":100,"price":1499.99}'
```

### 4. Test each strategy

```bash
# Replace $ID with the productId from step 3

# NoLock — no protection, fast but unsafe under concurrency
curl -s -X POST "http://localhost:5262/api/inventory/$ID/deduct" \
  -H "Content-Type: application/json" \
  -d '{"quantity":10,"strategy":"NoLock"}'

# LocalLock — safe on single instance, breaks on scale-out
curl -s -X POST "http://localhost:5262/api/inventory/$ID/deduct" \
  -H "Content-Type: application/json" \
  -d '{"quantity":10,"strategy":"LocalLock"}'

# Optimistic — version check + retry, works across instances
curl -s -X POST "http://localhost:5262/api/inventory/$ID/deduct" \
  -H "Content-Type: application/json" \
  -d '{"quantity":10,"strategy":"Optimistic"}'

# Distributed — Redis RedLock, coordinates across instances
curl -s -X POST "http://localhost:5262/api/inventory/$ID/deduct" \
  -H "Content-Type: application/json" \
  -d '{"quantity":10,"strategy":"Distributed"}'
```

### 5. Run tests

```bash
# All tests (pulls Docker images on first run)
dotnet test

# Single-instance concurrency tests
dotnet test --filter "FullyQualifiedName~DeductStockTests"

# Multi-instance distributed tests (3 simulated instances sharing Postgres+Redis)
dotnet test --filter "FullyQualifiedName~Deduct_Distributed_MultiInstance"
```

The integration tests use Testcontainers to start real PostgreSQL and Redis containers. No manual infra setup needed.

## Key Design Decisions

- **Strategy per request**: clients pick the strategy via the request body. Same endpoint, same handler, different guarantees.
- **3-layer approach**: distributed lock (fast rejection) + optimistic version check (safety net) + database CHECK constraint (physical guarantee).
- **DI wiring**: two independent toggles — `ConnectionStrings:Postgres` switches between in-memory and EF Core stores; `ConnectionStrings:Redis` switches between in-memory and RedLock distributed locking.
- **Multi-instance tests**: `CreateSimulatedInstanceAsync()` spins up independent `WebApplicationFactory` instances sharing the same Postgres and Redis backends, replicating Kubernetes pod behavior in tests.

## Architecture

The project follows **Vertical Slice Architecture**. Each use case (DeductStock, PlaceOrder, CreateProduct) is a self-contained slice with its own request, handler, and endpoint. Slices share abstractions through interfaces in `Shared/Contracts/` but never import each other's internals. Concurrency strategy is just a parameter to the handler, not a cross-cutting concern.

## Technologies

| Component           | Technology                                              |
| ------------------- | ------------------------------------------------------- |
| Runtime             | .NET 10                                                 |
| API                 | Minimal APIs + OpenAPI (built-in)                       |
| CQRS                | MediatR                                                 |
| ORM                 | Entity Framework Core + Npgsql (PostgreSQL)             |
| Distributed Locking | DistributedLock.Redis (RedLock)                         |
| In-memory Store     | ConcurrentDictionary-based (dev/demo)                   |
| Orchestration       | .NET Aspire AppHost (Postgres + Redis + 3 API replicas) |
| Telemetry           | OpenTelemetry via Aspire ServiceDefaults + Dashboard    |
| Integration Testing | Testcontainers (PostgreSQL + Redis), xUnit v3           |
| Load Testing        | k6 (Grafana) — 4 scenarios for strategy validation      |

## .NET Aspire Integration

This project ships with a full [.NET Aspire](https://learn.microsoft.com/dotnet/aspire) setup that orchestrates all infrastructure and API instances as a single `dotnet run` command.

### What Aspire Provides

| Feature              | Benefit                                                                        |
| -------------------- | ------------------------------------------------------------------------------ |
| **Orchestration**    | Single `dotnet run` starts Postgres, Redis, and 3 API replicas                 |
| **Dashboard**        | Real-time telemetry: distributed traces, metrics, structured logs              |
| **Service Defaults** | OpenTelemetry, health checks, and service discovery shared across all projects |
| **Container Mgmt**   | Postgres 17 and Redis 7.4 containers auto-provisioned with health checks       |
| **Scale-out**        | 3 API replicas as independent processes — validates LocalLock breakage locally |

### Project Structure

```
src/ECommerce.AppHost/       # Aspire orchestrator — Postgres + Redis + 3 API replicas + YARP gateway
src/ECommerce.ServiceDefaults/  # Shared OpenTelemetry, health checks, resilience
```

### Running

```bash
# Start the entire system (no Docker Compose needed for infra)
dotnet run --project src/ECommerce.AppHost/ECommerce.AppHost.csproj
```

The Aspire Dashboard opens at the URL shown in the console. From the dashboard you can:

- View live distributed traces for each API request across all 3 instances
- Inspect structured logs per instance
- Monitor HTTP metrics (request rate, error rate, latency)
- Open Redis Commander to inspect the RedLock keys
- Open Swagger UI for any API instance
- See the `DbUpdateConcurrencyException` crashes when testing LocalLock across replicas

### LocalLock Multi-Instance Validation

With the AppHost running (3 replicas), send concurrent LocalLock requests:

```bash
# Send 5 concurrent LocalLock requests
for i in $(seq 1 5); do
  curl -s -X POST "http://localhost:PORT/api/inventory/$ID/deduct" \
    -H "Content-Type: application/json" \
    -d '{"quantity":1,"strategy":"LocalLock"}' &
done
wait
```

Check the Aspire Dashboard for `DbUpdateConcurrencyException` traces — proof that `lock()` is process-scoped and invisible to the other 2 replicas.

For the complete multi-instance experience with load balancing, use the Docker Compose setup instead, which includes the YARP gateway.

## Load Testing with k6

This project ships with k6 performance tests that validate each concurrency strategy under realistic load patterns. Tests live in `deployments/k6/scenarios/`.

### Prerequisites

- [Docker Compose](https://docs.docker.com/compose/) (infrastructure must be running)
- Infrastructure started: `docker compose -f deployments/docker-compose.yaml up -d`

### Test Scenarios

| Scenario               | File                              | What It Proves                                                                                                                                                                                    |
| ---------------------- | --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Low Contention**     | `scenarios/low-contention.js`     | 20 VUs, each on their own product. Optimistic achieves >95% success with zero retries. NoLock silently corrupts.                                                                                  |
| **Medium Contention**  | `scenarios/medium-contention.js`  | 20-40 VUs sharing 5 products. Optimistic >90% success (some retries), Distributed >99% (no retries). Shows when each is appropriate.                                                              |
| **Flash Sale**         | `scenarios/flash-sale.js`         | 50 VUs fighting for 30 units of ONE product. Side-by-side: Optimistic phase has slow failures (retry cascade, 2-5s latency), Distributed phase has fast 409 rejection (<1ms) + steady processing. |
| **LocalLock Breakage** | `scenarios/locallock-breakage.js` | 30 VUs, 1 product, LocalLock only. With 3 API instances (Docker Compose), stock goes negative — proves `lock()` is process-scoped and invisible across instances.                                 |

### Running Tests

```bash
# Run all tests sequentially
bash k6/run-all.sh

# Run individual test
docker compose -f deployments/docker-compose.yaml -f deployments/docker-compose.k6.yml run --rm k6-flash-sale

# Available targets: k6-low-contention, k6-medium-contention, k6-flash-sale, k6-locallock-breakage
```

### What to Look For

**Flash sale test (most impactful demo):**

- **Optimistic** phase: most requests take 1-5s, many fail with "Max retries exceeded". The handler wastes time on retries that can never succeed because stock is already gone.
- **Distributed** phase: 60%+ of requests get instant 409 rejection (<50ms). The 30 that acquire the lock succeed cleanly with no retries.

**LocalLock breakage test:**

- With 3 API replicas (Docker Compose), stock goes negative. Each instance's `lock _localLock` is independent, so all 3 write concurrently.
- Compare: run with 1 API instance → stock stays at 0 (correct).

**Key insight**: The flash sale test directly demonstrates the architecture decision to use distributed locks for high-contention operations and optimistic concurrency for normal traffic. The right strategy depends on contention level, not just correctness.
