# Platform Workspace

## Repositories

| Repo                        | Stack               | Responsibility                          |
| --------------------------- | ------------------- | --------------------------------------- |
| `Company.Billing.Contracts` | .NET class library  | Shared DTOs and package versioning      |
| `Company.Billing.Api`       | ASP.NET Core        | Billing endpoints and validation        |
| `Company.Jobs.Worker`       | .NET Worker Service | Async processing and queue consumers    |
| `Company.Admin.Angular`     | Angular             | Internal admin UI                       |
| `Company.Infrastructure`    | Bicep / Terraform   | App config, queues, storage, deployment |

## Ownership Rules

- Request and response models live in `Company.Billing.Contracts`
- Breaking contract changes require version notes before release
- API validation rules belong in `Company.Billing.Api`
- Queue payload compatibility must be checked in `Company.Jobs.Worker`

## Cross-Repo Change Order

1. Contracts
2. API
3. Worker consumers
4. Angular UI
5. Infra only if settings, secrets, queues, or deployment behavior changed
