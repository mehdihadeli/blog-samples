# Aspire Shop

![Screenshot of the product catalog in the Aspire Shop sample](./images/aspireshop-frontend-light.png#gh-light-mode-only)
![Screenshot of the product catalog in the Aspire Shop sample](./images/aspireshop-frontend-dark.png#gh-dark-mode-only)

Browse a product catalog served by the Catalog service and add items to a cart held by the Basket service.

![Screenshot of the shopping cart in the Aspire Shop sample](./images/aspireshop-cart-light.png#gh-light-mode-only)
![Screenshot of the shopping cart in the Aspire Shop sample](./images/aspireshop-cart-dark.png#gh-dark-mode-only)

The app consists of four .NET services:

- **AspireShop.Frontend**: This is an ASP.NET Core Blazor app that displays a paginated catalog of products and allows users to add products to a shopping cart.
- **AspireShop.CatalogService**: This is an HTTP API that provides access to the catalog of products stored in a PostgreSQL database.
- **AspireShop.CatalogDbManager**: This is an HTTP API that manages the initialization and updating of the catalog database.
- **AspireShop.BasketService**: This is a gRPC service that provides access to the shopping cart stored in Redis.

The app also includes a .NET class library project, **AspireShop.ServiceDefaults**, that contains the code-based defaults used by the .NET service projects.

## Prerequisites

- [Aspire development environment](https://aspire.dev/get-started/prerequisites/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Running the app

If using the Aspire CLI, run `aspire run` from this directory.

If using VS Code, open this directory as a workspace and launch the `AspireShop.AppHost` project using either the Aspire or C# debuggers.

If using Visual Studio, open the solution file `AspireShop.slnx` and launch/debug the `AspireShop.AppHost` project.

If using the .NET CLI, run `dotnet run` from the `AspireShop.AppHost` directory.

## How telemetry flows in this sample

This repository uses two different runtime surfaces:

- The **Aspire AppHost** runs the .NET services and the Aspire Dashboard on the host machine.
- The **Docker Compose stack** under `deployments/docker-compose` runs the observability infrastructure: the OpenTelemetry Collector, Prometheus, Tempo, Loki, Grafana, MinIO, Elasticsearch, and Kibana.

### Default Aspire behavior

When you start the app through `AspireShop.AppHost` without overriding `OTEL_EXPORTER_OTLP_ENDPOINT`, Aspire injects an OTLP endpoint for each service at runtime and points it to the Aspire Dashboard. In that mode, telemetry flows directly to the dashboard:

`service -> Aspire Dashboard`

That is why Aspire can show logs, traces, and metrics even when no collector is involved.

### Collector-routed behavior

When you set `OTEL_EXPORTER_OTLP_ENDPOINT` to the local Collector, for example `http://localhost:4317`, each service sends telemetry to the Collector instead of directly to the dashboard:

`service -> Collector`

From there, the Collector fans telemetry out to the configured backends:

- traces -> Tempo and Aspire Dashboard
- metrics -> Prometheus and Aspire Dashboard
- logs -> Loki, Elasticsearch, and Aspire Dashboard

That means telemetry is **not** sent twice from the service process. The service sends once to the Collector, and the Collector forwards copies to the downstream systems.

### How the Collector reaches the Aspire Dashboard

The Aspire Dashboard is not defined as a Docker Compose service because it is started by the AppHost on the host machine. The Collector container reaches that host process through `host.docker.internal`.

The default Docker Compose settings assume the AppHost is using its HTTP profile, so the Collector exports to:

- `ASPIRE_OTLP_ENDPOINT=http://host.docker.internal:16223`
- `ASPIRE_INSECURE=true`

If you run the AppHost with its HTTPS profile instead, override those values before starting Compose:

- `ASPIRE_OTLP_ENDPOINT=https://host.docker.internal:16224`
- `ASPIRE_INSECURE=false`

## Send test telemetry through the OTEL Collector

The local Collector listens on:

- `localhost:4317` for OTLP/gRPC
- `localhost:4318` for OTLP/HTTP

The examples below use `curl` against OTLP/HTTP on port `4318`.

### 1. Send a test trace to OTLP HTTP `4318`

```bash
curl -X POST http://localhost:4318/v1/traces -H "Content-Type: application/json" -d '{"resourceSpans":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"manual-trace-check"}}]},"scopeSpans":[{"scope":{"name":"readme-check"},"spans":[{"traceId":"MDEyMzQ1Njc4OWFiY2RlZg==","spanId":"MDEyMzQ1Njc=","name":"manual-span","kind":"SPAN_KIND_SERVER","startTimeUnixNano":"1725235200000000000","endTimeUnixNano":"1725235201000000000","status":{"code":"STATUS_CODE_OK"}}]}]}]}'
```

### 2. Send a test metric to OTLP HTTP `4318`

```bash
curl -X POST http://localhost:4318/v1/metrics -H "Content-Type: application/json" -d '{"resourceMetrics":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"manual-metric-check"}}]},"scopeMetrics":[{"scope":{"name":"readme-check"},"metrics":[{"name":"manual_demo_gauge","description":"Manual OTLP metric check","unit":"1","gauge":{"dataPoints":[{"asInt":"1","timeUnixNano":"1725235201000000000","attributes":[{"key":"source","value":{"stringValue":"readme-check"}}]}]}}]}]}]}'
```

### 3. Send a test log to OTLP HTTP `4318`

```bash
curl -X POST http://localhost:4318/v1/logs -H "Content-Type: application/json" -d '{"resourceLogs":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"manual-log-check"}}]},"scopeLogs":[{"scope":{"name":"readme-check"},"logRecords":[{"timeUnixNano":"1725235202000000000","severityNumber":9,"severityText":"INFO","body":{"stringValue":"manual log from curl"},"attributes":[{"key":"source","value":{"stringValue":"readme-check"}}]}]}]}]}'
```

## Verify each backend received telemetry

Wait a few seconds after sending test data, then run these checks.

### Loki log query

```bash
curl -u admin:admin "http://localhost:3000/api/datasources/proxy/uid/loki-uid/loki/api/v1/query_range?query=%7Bservice_name%3D%22manual-log-check%22%7D&limit=20"
```

You should see a stream with `service_name="manual-log-check"` and the log body `manual log from curl`.

### Tempo trace query

Tempo is only reachable inside Docker Compose, so query it through Grafana's datasource proxy:

```bash
curl -u admin:admin "http://localhost:3000/api/datasources/proxy/uid/tempo-uid/api/search?tags=service.name%3Dmanual-trace-check"
```

If Tempo indexed the span, response should include a trace for `manual-trace-check`.

### Prometheus metric query

```bash
curl "http://localhost:9090/api/v1/query?query=manual_demo_gauge%7Bservice_name%3D%22manual-metric-check%22%7D"
```

You should see metric samples scraped from the Collector's Prometheus exporter on port `8889`.

### Grafana datasource health

```bash
curl -u admin:admin http://localhost:3000/api/datasources
```

You should see provisioned datasources for Prometheus, Loki, and Tempo. That confirms Grafana is wired to same backends the Collector exports to.

If you also want to test OTLP/gRPC, keep `4317` for SDKs or `grpcurl`. For raw `curl`, use `4318`.
