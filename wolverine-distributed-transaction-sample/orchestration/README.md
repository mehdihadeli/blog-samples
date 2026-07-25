# Orchestration-Based Saga with Wolverine

This sample demonstrates the **orchestration-based Saga pattern** using Wolverine's built-in Saga support. A central `OrderSagaState` class coordinates the distributed transaction across the Order and Payment services.

## How It Works

1. `POST /api/v1/orders` creates an order (Pending) and starts the saga
2. Saga sends `ProcessPayment` to the Payment service via RabbitMQ
3. Saga schedules `OrderTimeout` (30-second delay) as a safety net
4. Payment service processes and responds with `PaymentProcessed` or `PaymentFailed`
5. Saga handles the response — confirms the order or cancels with compensation
6. If neither response arrives in 30s, `OrderTimeout` fires and cancels the order

## Architecture

```
┌──────────────┐    HTTP     ┌──────────────────┐
│   Client     │ ──────────► │  OrderSaga API   │
└──────────────┘             │  (Saga + Outbox) │
                             │  ASP.NET Core     │
                             └────────┬─────────┘
                                      │
                          ┌───────────┴───────────┐
                          │                        │
                    Publish                   Schedule
                 ProcessPayment             OrderTimeout
                          │                        │
                          ▼                        ▼
                  ┌──────────────┐         ┌──────────────┐
                  │  RabbitMQ    │         │  PostgreSQL  │
                  │ payment-req. │         │ (durable     │
                  └──────┬───────┘         │  scheduled)  │
                         │                 └──────────────┘
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
```

## Messages

| Message            | Direction      | Queue                       | Description                  |
| ------------------ | -------------- | --------------------------- | ---------------------------- |
| `ProcessPayment`   | Saga → Payment | `payment-requests`          | Command to process payment   |
| `PaymentProcessed` | Payment → Saga | `order-payment-responses`   | Payment succeeded            |
| `PaymentFailed`    | Payment → Saga | `order-payment-responses`   | Payment declined             |
| `OrderTimeout`     | Self           | Scheduled (30s, PostgreSQL) | Timeout check if no response |

## Key Components

- **`OrderSagaState`** — Wolverine Saga state machine handling `Start`, `PaymentProcessed`, `PaymentFailed`, `OrderTimeout`
- **`Order`** — Domain model with `Create()`, `Confirm()`, `Cancel()` enforcing valid state transitions
- **`ProcessPaymentHandler`** — Payment worker simulating 80/20 success/failure gateway
- **`OrderDbContext`** — EF Core context for order storage
- **Wolverine outbox/inbox** — Durable message delivery guarantees exactly-once execution

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
orchestration/
├── Directory.Build.props
├── global.json
├── orchestration.slnx
├── src/
│   ├── Aspire/
│   │   ├── WolverineDistributed.AppHost/        # Aspire orchestration
│   │   └── WolverineDistributed.ServiceDefaults/ # Service defaults
│   ├── BuildingBlocks/
│   │   └── BuildingBlocks.Integration.Wolverine/ # Shared Wolverine config
│   ├── Shared/
│   │   └── Contracts/                            # Message contracts
│   └── Services/
│       ├── OrderSaga/                            # Order + Saga service
│       │   ├── OrderSaga/                        # Domain + handlers
│       │   │   ├── Orders/
│       │   │   │   ├── Models/Order.cs
│       │   │   │   └── Features/
│       │   │   │       ├── CreatingOrder/v1/     # Endpoint + command
│       │   │   │       ├── GettingOrderById/v1/  # Query endpoint
│       │   │   │       └── ProcessingOrderPayment/v1/ # Saga state
│       │   │   └── Shared/Data/OrderDbContext.cs
│       │   └── OrderSaga.Api/                    # ASP.NET host
│       └── Payment/                              # Payment worker
│           ├── Payment/
│           │   └── Payments/Features/ProcessingPayment/v1/
│           └── Payment.Api/
└── tests/
    ├── Directory.Build.props
    ├── Shared/Tests.Shared/                      # Test infrastructure
    └── Services/
        ├── OrderSaga/OrderSaga.IntegrationTests/ # Saga integration tests
        └── Payment/Payment.IntegrationTests/     # Payment health checks
```

## Running

```bash
# Run with Aspire
dotnet run --project src/Aspire/WolverineDistributed.AppHost

# Run tests
dotnet test tests/Services/OrderSaga/OrderSaga.IntegrationTests
dotnet test tests/Services/Payment/Payment.IntegrationTests
```

## Tests

- **`CreateOrderTests`** — Verifies the full order creation flow: POST /api/v1/orders, checks order persisted with Pending status
- **`SagaStateTests`** — Tests saga state transitions: happy path (confirmed), compensation (cancelled), timeout (timedOut)
- Tests use **TestContainers** to spin up real PostgreSQL and RabbitMQ containers
- **Respawn** resets database state between test runs
