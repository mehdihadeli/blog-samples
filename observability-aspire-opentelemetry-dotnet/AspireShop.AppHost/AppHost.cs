using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var otlpEndpoint =
    Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";
var otlpProtocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL") ?? "grpc";
var postgresImageTag = builder.Configuration["Infrastructure:Postgres:ImageTag"] ?? "16.4-alpine";
var redisImage = builder.Configuration["Infrastructure:Redis:Image"] ?? "redis/redis-stack";
var redisImageTag = builder.Configuration["Infrastructure:Redis:ImageTag"] ?? "7.4.0-v0";

var postgres = builder
    .AddPostgres("postgres")
    .WithImageTag(postgresImageTag)
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

if (builder.ExecutionContext.IsRunMode)
{
    // Data volumes don't work on ACA for Postgres so only add when running
    postgres.WithDataVolume();
}

var catalogDb = postgres.AddDatabase("catalogdb");

var basketCache = builder
    .AddRedis("basketcache")
    .WithImage(redisImage)
    .WithImageTag(redisImageTag)
    .WithDataVolume()
    .WithRedisCommander();

var catalogDbManager = builder
    .AddProject<Projects.AspireShop_CatalogDbManager>("catalogdbmanager")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol)
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithHttpHealthCheck("/health")
    .WithHttpCommand(
        "/reset-db",
        "Reset Database",
        commandOptions: new() { IconName = "DatabaseLightning" }
    );

var catalogService = builder
    .AddProject<Projects.AspireShop_CatalogService>("catalogservice")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol)
    .WithReference(catalogDb)
    .WaitFor(catalogDbManager)
    .WithHttpHealthCheck("/health");

var basketService = builder
    .AddProject<Projects.AspireShop_BasketService>("basketservice")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol)
    .WithReference(basketCache)
    .WaitFor(basketCache);

builder
    .AddProject<Projects.AspireShop_Frontend>("frontend")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol)
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", url => url.DisplayText = "Online Store (HTTPS)")
    .WithUrlForEndpoint("http", url => url.DisplayText = "Online Store (HTTP)")
    .WithHttpHealthCheck("/health")
    .WithReference(basketService)
    .WithReference(catalogService)
    .WaitFor(catalogService);

builder.Build().Run();
