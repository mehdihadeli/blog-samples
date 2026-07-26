var builder = DistributedApplication.CreateBuilder(args);

// ── PostgreSQL ──────────────────────────────────────────────
// Shared database for all API instances.
// Credentials match docker-compose.yaml and appsettings.json.
// Use AddPostgres with explicit userName/password parameters so
// the Aspire health check connection string matches the container's
// POSTGRES_USER (both use the UserNameReference on the resource).
var pgUser = builder.AddParameter("pg-user", "ecommerce");
var pgPassword = builder.AddParameter("pg-password", "ecommerce", secret: true);

var postgres = builder
    .AddPostgres("PostgresServer", userName: pgUser, password: pgPassword)
    .WithImage("postgres:17")
    .WithEnvironment("POSTGRES_DB", "ecommerce")
    .WithEnvironment("PGUSER", "ecommerce")
    .WithEnvironment("PGPASSWORD", "ecommerce");

// Database name "Postgres" matches ConnectionStrings:Postgres key in appsettings
var postgresDb = postgres.AddDatabase("Postgres");

// ── Redis ───────────────────────────────────────────────────
// Shared RedLock coordinator for distributed lock strategy.
var redis = builder.AddRedis("Redis").WithImage("redis/redis-stack:7.4.0-v0").WithRedisCommander();

// ── 3 API instances (simulates Kubernetes pod scale-out) ────
// Each instance is an independent process with its own DI container.
// They share Postgres + Redis — exactly like production.
// LocalLock strategy breaks here because each instance has its own lock.
var api = builder
    .AddProject<Projects.ECommerce_App>("ecommerce-api")
    .WithHttpEndpoint(name: "http")
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReplicas(3);

// ── YARP Gateway ───────────────────────────────────────────────
// Uses the ECommerce.Gateway project with Aspire service discovery.
// Routes all traffic to API replicas via load-balanced cluster.
// WithReference(api) injects the API endpoint URL via service discovery,
// so the Gateway connects to the actual API replicas not Docker hostnames.
var gateway = builder
    .AddProject<Projects.ECommerce_Gateway>("ecommerce-gateway")
    .WithHttpEndpoint(name: "http", port: 8080)
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
