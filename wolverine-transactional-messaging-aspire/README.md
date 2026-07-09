# Wolverine Transactional Messaging with Aspire

This sample shows how to build a small e-commerce system with Wolverine, .NET Aspire, PostgreSQL, MongoDB, and a selectable RabbitMQ or Kafka transport.

The solution keeps the structure intentionally close to the larger `food-delivery-microservices` codebase, but trims the building blocks down to the pieces this sample actually uses:

- two microservices: `Catalogs` and `Orders`
- shared contracts and a reusable `MessageEnvelope<T>`
- building blocks only for Wolverine, PostgreSQL, and MongoDB
- durable outbox, durable inbox, and durable local processing
- shared test data plus focused unit, startup, and vertical-slice integration tests

## What the sample demonstrates

`Catalogs` is the write-side service.

- It stores products in PostgreSQL.
- It publishes `ProductCreatedV1` through Wolverine.
- It sends an internal durable command to project a MongoDB read model after commit.

`Orders` is the downstream consumer.

- It listens to the same event from RabbitMQ or Kafka.
- It uses Wolverine durable inbox semantics backed by PostgreSQL.
- It writes an imported product record to its own PostgreSQL database.

## Solution layout

- `src/Aspire/ECommerce.AppHost`: Aspire orchestration for PostgreSQL, MongoDB, RabbitMQ, and Kafka.
- `src/BuildingBlocks/BuildingBlocks.Integration.Wolverine*`: reusable Wolverine event bus, durable persistence, and broker transport configuration.
- `src/BuildingBlocks/BuildingBlocks.Persistence.EfCore.Postgres`: PostgreSQL `DbContext` registration helper.
- `src/BuildingBlocks/BuildingBlocks.Persistence.Mongo`: MongoDB registration helper.
- `src/Services/Catalogs`: write-side service plus MongoDB read-model projection.
- `src/Services/Orders`: consumer service with durable inbox processing.
- `src/Services/Shared`: integration events, internal commands, transport constants, and message envelope.
- `tests/Shared/Tests.Shared`: common test fixtures and transport-aware integration test base classes.
- `tests/Services/Catalogs`: startup coverage plus feature tests that mirror the `Catalogs` slices.
- `tests/Services/Orders`: startup coverage plus feature tests that mirror the `Orders` slices.

## Message flow

1. `Catalogs` receives `POST /api/v1/catalogs/products`.
2. The service opens an EF Core transaction and enrolls Wolverine's outbox in the same `DbContext`.
3. The product write model is stored in PostgreSQL.
4. `Catalogs` publishes `MessageEnvelope<ProductCreatedV1>` to RabbitMQ or Kafka.
5. `Catalogs` also sends `ProjectProductReadModel` to a durable local Wolverine queue.
6. After the transaction commits, Wolverine flushes durable outgoing work.
7. The local handler upserts the MongoDB read model.
8. `Orders` consumes `ProductCreatedV1` with durable inbox protection and writes an imported product record.

## Transport selection

Supported transports:

- `rabbitmq`
- `kafka`

Default transport in both APIs is `rabbitmq`.

The AppHost reads `Messaging__Transport`, provisions only the selected broker, and forwards the same value to both services.

Git Bash example:

```bash
cd /d/blog-writing/samples/wolverine-transactional-messaging-aspire
Messaging__Transport=kafka dotnet run --project src/Aspire/ECommerce.AppHost/ECommerce.AppHost.csproj
```

Use `rabbitmq` instead of `kafka` to switch back.

If you run the APIs directly instead of through Aspire, set `Messaging:Transport` in both API project `appsettings.json` files.

## Useful endpoints

Catalogs base path: `/api/v1/catalogs`

- `POST /products`
- `GET /products/{id}`
- `GET /products/read-model`
- `GET /products/read-model/{id}`

Orders base path: `/api/v1/orders`

- `GET /products`
- `GET /products/{id}`

When running with Aspire, use the base URLs shown in the Aspire dashboard for `catalogs-api` and `orders-api`.

## Validation

Build the full sample:

```bash
dotnet build wolverine-transactional-messaging-aspire.slnx
```

Run all tests:

```bash
dotnet test wolverine-transactional-messaging-aspire.slnx --no-build
```

The current test suite covers:

- envelope metadata creation
- MongoDB read-model projection handler behavior
- downstream consumer upsert behavior
- startup wiring for both RabbitMQ and Kafka transports
- vertical-slice integration tests kept in the same feature-style folders as the application code

The integration tests use one shared base per service, keep both broker fixtures available, and switch the active transport by overriding `Messaging:Transport` plus the matching connection string. That keeps the test layout aligned with the application slices instead of splitting the suite into RabbitMQ-only and Kafka-only class hierarchies.

## Reference docs

When reading the sample beside the article, these Wolverine docs are the most relevant:

- Durable messaging: <https://wolverinefx.io/guide/durability/>
- Durable local queues: <https://wolverinefx.io/guide/durability/durable-local-queues>
- EF Core transactional middleware: <https://wolverinefx.io/guide/durability/efcore>
- RabbitMQ transport: <https://wolverinefx.io/guide/messaging/transports/rabbitmq>
- Kafka transport: <https://wolverinefx.io/guide/messaging/transports/kafka>
