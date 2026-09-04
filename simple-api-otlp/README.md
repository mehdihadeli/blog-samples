# Simple API OTLP Sample

Minimal sample for sending Aspire-style OpenTelemetry data to an OTLP collector without an AppHost.

## Projects

- `SimpleApiOtlp.Api`: small ASP.NET Core Web API.
- `SimpleApiOtlp.ServiceDefaults`: Aspire ServiceDefaults project created from `dotnet new aspire-servicedefaults`.

## What this sample shows

- `builder.AddServiceDefaults()` wires OpenTelemetry, health checks, resilience, and service discovery.
- `OTEL_EXPORTER_OTLP_ENDPOINT` controls where telemetry is exported.
- Calling `/telemetry` emits:
  - an inbound ASP.NET Core trace span
  - one custom nested activity span
  - one structured log entry
  - built-in ASP.NET Core and runtime metrics

## Run

Start an OTLP collector listening on `localhost:4317`, then run:

```bash
dotnet run --project SimpleApiOtlp.Api
```

Open:

- `http://localhost:5136/telemetry`
- `http://localhost:5136/health`
- `http://localhost:5136/alive`

## OTLP configuration

The API uses these launch profile values:

```json
"OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317",
"OTEL_EXPORTER_OTLP_PROTOCOL": "grpc"
```

Override them from the shell if you want a different collector endpoint.
