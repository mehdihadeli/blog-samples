# MassTransit Transactional Messaging with Aspire

This sample shows how to build a small e-commerce system with MassTransit, .NET Aspire, PostgreSQL, MongoDB, and a selectable RabbitMQ or Kafka transport.

It demonstrates:

- transactional publish with the MassTransit EF bus outbox
- transactional consumer processing with consumer outbox and inbox state
- retry and delayed redelivery policies
- MassTransit error transport after retry exhaustion
- post-commit internal processing for a MongoDB read model

## Solution layout

- `src/Aspire/ECommerce.AppHost`: Aspire orchestration for PostgreSQL, MongoDB, RabbitMQ, and Kafka.
- `src/BuildingBlocks/BuildingBlocks.Integration.MassTransit`: reusable MassTransit registration, transport selection, and publishing abstractions.
- `src/BuildingBlocks/BuildingBlocks.Persistence.EfCore.Postgres`: PostgreSQL `DbContext` registration helper.
- `src/BuildingBlocks/BuildingBlocks.Persistence.Mongo`: MongoDB registration helper.
- `src/Services/Catalogs`: write-side service plus MongoDB read-model projection.
- `src/Services/Orders`: downstream consumer service.
- `src/Services/Shared`: contracts, transport constants, and message envelope.

## Validation

```bash
dotnet build masstransit-transactional-messaging-aspire.slnx
```
