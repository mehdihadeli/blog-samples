# Wolverine Distributed Transaction Samples

This repository contains two implementations of the **Saga pattern** for distributed transactions using [Wolverine](https://wolverinefx.net/) and RabbitMQ — one using **orchestration** (central saga state machine) and one using **choreography** (event-driven coordination).

Both samples implement the same e-commerce order-payment flow:

1. Create an order (Pending status)
2. Process payment via a Payment service
3. Handle three outcomes: **success**, **failure with compensation**, or **timeout recovery**

## Structure

```
wolverine-distributed-transaction-sample/
├── orchestration/       # Orchestration-based saga (Wolverine Saga)
│   ├── src/             # Application projects
│   ├── tests/           # Integration tests with TestContainers
│   └── README.md
├── choreography/        # Choreography-based saga (event-driven)
│   ├── src/             # Application projects
│   ├── tests/           # Integration tests with TestContainers
│   └── README.md
└── README.md
```

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Run with Aspire

```bash
# Orchestration
cd orchestration
dotnet run --project src/Aspire/WolverineDistributed.AppHost

# Choreography (separate terminal)
cd choreography
dotnet run --project src/Aspire/Choreography.AppHost
```

### Run Tests

```bash
# Orchestration tests
cd orchestration
dotnet test tests/Services/OrderSaga/OrderSaga.IntegrationTests
dotnet test tests/Services/Payment/Payment.IntegrationTests

# Choreography tests
cd choreography
dotnet test tests/Services/Order/Order.IntegrationTests
dotnet test tests/Services/Payment/Payment.IntegrationTests
```

## Which Pattern Should I Use?

| Factor                   | Orchestration                          | Choreography                        |
| ------------------------ | -------------------------------------- | ----------------------------------- |
| **Workflow complexity**  | Complex (3+ steps, branching)          | Simple (linear, 2-3 services)       |
| **Flow visibility**      | Single saga class documents everything | Distributed across handlers         |
| **Timeout handling**     | Built-in (auto-cancel on completion)   | Manual (defensive check in handler) |
| **Coupling**             | Orchestrator knows all participants    | Services only know events           |
| **Testing**              | Unit test the saga class directly      | Test handler chains together        |
| **Team/tech boundaries** | Shared orchestration layer             | Polyglot, cross-team friendly       |

## Tech Stack

| Technology            | Version      |
| --------------------- | ------------ |
| .NET                  | 10.0         |
| WolverineFx           | 6.22.0       |
| RabbitMQ              | 4-management |
| PostgreSQL            | 17           |
| Aspire                | 9.5.2        |
| TestContainers        | 4.8.1        |
| xUnit                 | 3.2.2        |
| Entity Framework Core | 10.0.4       |

## Related Blog Post

For a detailed walkthrough of both patterns, see the accompanying article:

[Distributed Transactions with Wolverine: Saga Orchestration vs Choreography](https://mehdihadeli.github.io/blog/distributed-transaction-with-wolverine)
