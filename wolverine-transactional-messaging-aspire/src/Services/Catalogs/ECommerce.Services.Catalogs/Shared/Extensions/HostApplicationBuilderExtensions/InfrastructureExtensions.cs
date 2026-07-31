using System.Text.Json;
using BuildingBlocks.Core;
using BuildingBlocks.Core.Extensions;
using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Extensions;
using BuildingBlocks.Integration.Wolverine.Kafka;
using BuildingBlocks.Integration.Wolverine.Kafka.Extensions;
using BuildingBlocks.Integration.Wolverine.RabbitMQ;
using BuildingBlocks.Integration.Wolverine.RabbitMQ.Extensions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Services.Catalogs.Shared.Extensions.HostApplicationBuilderExtensions;

public static class InfrastructureExtensions
{
    public static WebApplicationBuilder AddInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(CatalogsMetadata).Assembly);
        });
        builder.Services.AddValidatorsFromAssembly(typeof(CatalogsMetadata).Assembly);
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        var connectionString =
            builder.Configuration.GetConnectionString("catalogsdb")
            ?? throw new InvalidOperationException("Missing connection string 'catalogsdb'.");
        var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
        var wolverineOptions = builder.Configuration.BindOptions<WolverineBusOptions>(
            nameof(WolverineBusOptions)
        );

        switch (wolverineOptions.TransportType)
        {
            case MessagingTransportType.RabbitMq:
                // Both options below work identically for RabbitMQ and Kafka:
                //   OPTION 2 (auto): AutoConfigMessagesTopology=true triggers convention-driven
                //     auto-scan — no per-service topology file needed.
                //   OPTION 1 (manual): AutoConfigMessagesTopology=false (default), configure
                //     callback provides explicit per-service topology.
                builder.AddWolverineRabbitMq(
                    wolverineBusOptions =>
                    {
                        wolverineBusOptions.ConnectionName = "rabbitmq";
                        wolverineBusOptions.ConnectionString = rabbitMqConnectionString;
                        wolverineBusOptions.DurableStorageConnectionString = connectionString;
                    },
                    configure: wolverineOptions.AutoConfigMessagesTopology
                        ? null
                        : rabbitMq => rabbitMq.ConfigureCatalogsPublishTopology(),
                    assemblies: [typeof(CatalogsMetadata).Assembly]
                );
                break;

            case MessagingTransportType.Kafka:
                builder.AddWolverineKafka(
                    wolverineBusOptions =>
                    {
                        wolverineBusOptions.ConnectionName = "kafka";
                        wolverineBusOptions.DurableStorageConnectionString = connectionString;
                    },
                    configure: wolverineOptions.AutoConfigMessagesTopology
                        ? null
                        : kafka => kafka.ConfigureCatalogsPublishTopology(),
                    assemblies: [typeof(CatalogsMetadata).Assembly]
                );
                break;
        }

        return builder;
    }
}
