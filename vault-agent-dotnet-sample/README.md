# Vault Agent .NET Sample

```bash
project/
├── deployments/
│   ├── docker-compose.infrastructure.yml
│   ├── docker-compose.apps.yml
│   ├── docker-compose.apps.dev.yml
│   ├── infrastructure/
│   │   ├── postgres/init.sql
│   │   └── vault/configure.sh
│   └── agent-config/
│       ├── orders/
│       │   ├── vault-agent.hcl
│       │   └── appsettings.ctmpl
│       └── users/
│           ├── vault-agent.hcl
│           └── appsettings.ctmpl
├── services/
│   ├── Contracts/Contracts.csproj
│   ├── OrdersApi/
│   │   ├── Dockerfile
│   │   ├── OrdersApi.csproj
│   │   └── Program.cs
│   └── UsersApi/
│       ├── Dockerfile
│       ├── UsersApi.csproj
│       └── Program.cs
└── vault-agent-dotnet-sample.slnx
```

In deployments directory:

```bash
docker compose -f deployments/docker-compose.infrastructure.yml up -d
docker compose -f deployments/docker-compose.apps.dev.yml up -d
```

The checked-in development compose file starts Vault, RabbitMQ, PostgreSQL, and the Vault Agent sidecars.
Run `OrdersApi` and `UsersApi` locally from `services/` so they can read the rendered files from their `vault-secrets` folders.
