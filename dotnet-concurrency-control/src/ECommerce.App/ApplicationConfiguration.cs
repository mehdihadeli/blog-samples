using System.Text.Json.Serialization;
using ECommerce.Shared.Extensions.HostApplicationBuilderExtensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ECommerce;

// Module configuration — registers all e-commerce services.
// Follows Catalogs ApplicationConfiguration pattern.
public static class ApplicationConfiguration
{
    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        // Aspire service defaults: OpenTelemetry, health checks, service discovery
        builder.AddServiceDefaults();

        builder.AddStorage();
        builder.Services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ApplicationConfiguration).Assembly)
        );

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        // OpenAPI (built-in .NET 10 — no Swashbuckle)
        builder.Services.AddOpenApi();

        return builder;
    }
}
