# Company.Jobs.Worker Copilot Instructions

## Architecture

- Message handlers live under `src/Handlers/`.
- Retry-safe logic belongs in services, not in the transport layer.

## Critical Rules

- Handlers must be idempotent.
- Queue contracts come from shared packages, not local duplicate types.

## Tests

- Run `dotnet test tests/Company.Jobs.Worker.UnitTests`
