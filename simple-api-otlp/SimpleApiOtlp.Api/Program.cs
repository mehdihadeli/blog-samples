using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Redirect("/telemetry"));

app.MapGet(
    "/telemetry",
    (ILogger<Program> logger) =>
    {
        using var activity = Telemetry.ActivitySource.StartActivity("simple-api-otlp.request");
        activity?.SetTag("sample.endpoint", "/telemetry");
        activity?.SetTag("sample.transport", "http");

        logger.LogInformation(
            "Handled telemetry sample request at {TimestampUtc}",
            DateTimeOffset.UtcNow
        );

        return Results.Ok(
            new TelemetryResponse(
                "Telemetry emitted to configured OTLP endpoint.",
                builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "not-configured",
                builder.Configuration["OTEL_EXPORTER_OTLP_PROTOCOL"] ?? "grpc"
            )
        );
    }
);

app.MapDefaultEndpoints();

app.Run();

internal static class Telemetry
{
    internal static readonly ActivitySource ActivitySource = new("SimpleApiOtlp.Api");
}

internal sealed record TelemetryResponse(string Message, string OtlpEndpoint, string OtlpProtocol);
