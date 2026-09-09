using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
var otlpProtocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
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
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithHttpHealthCheck("/health")
    .WithHttpCommand(
        "/reset-db",
        "Reset Database",
        commandOptions: new() { IconName = "DatabaseLightning" }
    );

if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    catalogDbManager.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint);

    if (!string.IsNullOrWhiteSpace(otlpProtocol))
    {
        catalogDbManager.WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);
    }
}

var catalogService = builder
    .AddProject<Projects.AspireShop_CatalogService>("catalogservice")
    .WithReference(catalogDb)
    .WaitFor(catalogDbManager)
    .WithHttpHealthCheck("/health", endpointName: "http");

if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    catalogService.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint);

    if (!string.IsNullOrWhiteSpace(otlpProtocol))
    {
        catalogService.WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);
    }
}

var basketService = builder
    .AddProject<Projects.AspireShop_BasketService>("basketservice")
    .WithReference(basketCache)
    .WaitFor(basketCache);

if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    basketService.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint);

    if (!string.IsNullOrWhiteSpace(otlpProtocol))
    {
        basketService.WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);
    }
}

var frontend = builder
    .AddProject<Projects.AspireShop_Frontend>("frontend")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", url => url.DisplayText = "Online Store (HTTPS)")
    .WithUrlForEndpoint("http", url => url.DisplayText = "Online Store (HTTP)")
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WithReference(basketService)
    .WithReference(catalogService)
    .WaitFor(catalogService);

if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    frontend.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint);

    if (!string.IsNullOrWhiteSpace(otlpProtocol))
    {
        frontend.WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);
    }
}

builder.Build().Run();
