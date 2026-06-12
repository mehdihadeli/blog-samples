# Company.Billing.Api Copilot Instructions

## Architecture

- Controllers stay thin.
- Business logic belongs in application services.

## Critical Rules

- API errors must use the shared problem-details builder.
- Contract types come from `Company.Billing.Contracts`.

## Tests

- Run `dotnet test tests/Company.Billing.Api.UnitTests`
- Run `dotnet test tests/Company.Billing.Api.IntegrationTests`
