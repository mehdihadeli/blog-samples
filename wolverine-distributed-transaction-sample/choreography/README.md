# Choreography-Based Saga (Event-Driven)

This sample demonstrates the **choreography-based Saga pattern** using pure event-driven coordination with Wolverine message handlers — no central saga state machine. Each service reacts to events and publishes its own events.

## How It Works

1. `POST /api/v1/orders` creates an order (Pending), publishes `OrderCreated`, and schedules `OrderTimeoutCheck`
2. Payment service listens to `OrderCreated`, processes payment, publishes `PaymentProcessed` or `PaymentFailed`
3. Order service listens to:
   - `PaymentProcessed` → confirms the order (forward compensation)
   - `PaymentFailed` → cancels the order (rollback compensation)
   - `OrderTimeoutCheck` → if still Pending, cancels with "timeout"

No saga state table. No central coordinator. Each handler checks order status independently (idempotent).

## Architecture

```
┌──────────────┐    HTTP     ┌──────────────────┐
│   Client     │ ──────────► │   Order API      │
└──────────────┘             │  ASP.NET Core     │
                             └────────┬─────────┘
                                      │
                         ┌────────────┴────────────┐
                         │                         │
                   Publish                   Schedule
                OrderCreated              OrderTimeoutCheck
                         │                         │
                         ▼                         ▼
                  ┌──────────────┐        ┌──────────────┐
                  │  RabbitMQ    │        │  PostgreSQL  │
                  │ order-events │        │ (durable     │
                  └──────┬───────┘        │  scheduled)  │
                         │                └──────────────┘
                         ▼
                  ┌──────────────┐
                  │  Payment     │
                  │  Service     │
                  └──────┬───────┘
                         │
                  ┌──────┴──────┐
                  │              │
         PaymentProcessed   PaymentFailed
                  │              │
                  ▼              ▼
           Confirm Order   Cancel Order
                              ▲
                              │
                    OrderTimeoutCheck
                    (defensive noop if done)
```

## Messages

| Message             | Direction   | Queue            | Description                              |
| ------------------- | ----------- | ---------------- | ---------------------------------------- |
| `OrderCreated`      | Order → Pay | `order-events`   | New order created, details included      |
| `PaymentProcessed`  | Pay → Order | `payment-events` | Payment succeeded                        |
| `PaymentFailed`     | Pay → Order | `payment-events` | Payment declined                         |
| `OrderTimeoutCheck` | Self        | Scheduled (30s)  | Timeout check — noop if already resolved |

## Key Components

- **`CreateOrderEndpoint`** — Creates order, publishes `OrderCreated`, schedules `OrderTimeoutCheck`
- **`PaymentProcessedHandler`** — Confirms order if still Pending (idempotent)
- **`PaymentFailedHandler`** — Cancels order with reason if still Pending (idempotent)
- **`OrderTimeoutCheckHandler`** — Cancels with "timeout" if still Pending (defensive idempotent)
- **`OrderCreatedHandler`** (Payment) — Simulates payment gateway, publishes 80/20 success/failure
- **No saga state** — No `Saga` base class, no saga tables in PostgreSQL

## Key Differences from Orchestration

| Aspect               | Orchestration (Wolverine Saga) | Choreography (This Sample)       |
| -------------------- | ------------------------------ | -------------------------------- |
| **State management** | Central saga state in DB       | Each handler checks status       |
| **Flow visibility**  | Single saga class              | Distributed across handlers      |
| **Timeout handling** | Built-in, auto-cancel          | Manual defensive check           |
| **Idempotency**      | Saga executes once per state   | `if (status != Pending) return;` |
| **Coupling**         | Orchestrator knows all         | Services know only events        |

## Tech Stack

| Technology            | Version |
| --------------------- | ------- |
| .NET                  | 10.0    |
| WolverineFx           | 6.22.0  |
| RabbitMQ              | 4       |
| PostgreSQL            | 17      |
| Aspire                | 9.5.2   |
| Entity Framework Core | 10.0.4  |
| TestContainers        | 4.8.1   |
| xUnit                 | 3.2.2   |

## Project Structure

```
choreography/
├── Directory.Build.props
├── global.json
├── choreography.slnx
├── src/
│   ├── Aspire/
│   │   ├── Choreography.AppHost/                # Aspire orchestration
│   │   └── Choreography.ServiceDefaults/         # Service defaults
│   ├── BuildingBlocks/
│   │   └── BuildingBlocks.Integration.Wolverine/ # Shared Wolverine config
│   ├── Shared/
│   │   └── Contracts/                            # Message contracts
│   └── Services/
│       ├── Order/                                # Order service (no saga)
│       │   ├── Order/                            # Domain + handlers
│       │   │   ├── Orders/
│       │   │   │   ├── Models/Order.cs
│       │   │   │   └── Features/
│       │   │   │       ├── CreatingOrder/v1/         # Endpoint
│       │   │   │       ├── ConfirmingOrder/v1/        # PaymentProcessed handler
│       │   │   │       ├── CancellingOrder/v1/        # PaymentFailed handler
│       │   │   │       └── HandlingOrderTimeout/v1/   # Timeout handler
│       │   │   └── Shared/Data/OrderDbContext.cs
│       │   └── Order.Api/                        # ASP.NET host
│       └── Payment/                              # Payment worker
│           ├── Payment/
│           │   └── Payments/Features/ProcessingPayment/v1/
│           └── Payment.Api/
└── tests/
    ├── Directory.Build.props
    ├── Shared/Tests.Shared/                      # Test infrastructure
    └── Services/
        ├── Order/Order.IntegrationTests/         # Order integration tests
        └── Payment/Payment.IntegrationTests/     # Payment health checks
```

## Running

```bash
# Run with Aspire
dotnet run --project src/Aspire/Choreography.AppHost

# Run tests
dotnet test tests/Services/Order/Order.IntegrationTests
dotnet test tests/Services/Payment/Payment.IntegrationTests
```

## Tests

- **`CreateOrderTests`** — Full end-to-end test: creates order via API, verifies it's persisted with Pending status
- **`HealthCheckTests`** — Simple Payment service health verification
- Tests use **TestContainers** to spin up real PostgreSQL and RabbitMQ containers
