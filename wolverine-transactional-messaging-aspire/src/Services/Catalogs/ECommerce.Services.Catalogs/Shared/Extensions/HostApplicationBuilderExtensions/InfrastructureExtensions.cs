using System.Text.Json;
using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Extensions;
using BuildingBlocks.Integration.Wolverine.Kafka;
using BuildingBlocks.Integration.Wolverine.RabbitMQ;
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

        var transport = builder.Configuration.GetMessagingTransport();
        var connectionString =
            builder.Configuration.GetConnectionString("catalogsdb")
            ?? throw new InvalidOperationException("Missing connection string 'catalogsdb'.");
        var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
        var busOptions =
            builder.Configuration.GetSection("Wolverine").Get<WolverineBusOptions>()
            ?? new WolverineBusOptions();

        switch (transport)
        {
            case MessagingTransportType.RabbitMq:
                builder.AddWolverineRabbitMq(
                    new WolverineRabbitMqRegistrationOptions
                    {
                        Common = new WolverineCommonOptions
                        {
                            DurableStorageConnectionString = connectionString,
                            HandlerAssemblies = [typeof(CatalogsMetadata).Assembly],
                            Bus = new WolverineBusOptions
                            {
                                UseDurableLocalQueues = busOptions.UseDurableLocalQueues,
                                UseEntityFrameworkCoreTransactions =
                                    busOptions.UseEntityFrameworkCoreTransactions,
                            },
                        },
                        RabbitMq = new WolverineRabbitMqOptions
                        {
                            ConnectionName = "rabbitmq",
                            ConnectionString = rabbitMqConnectionString,
                        },
                    },
                    rabbitMq => rabbitMq.ConfigureCatalogsPublishTopology()
                );
                break;

            case MessagingTransportType.Kafka:
                builder.AddWolverineKafka(
                    new WolverineKafkaRegistrationOptions
                    {
                        Common = new WolverineCommonOptions
                        {
                            DurableStorageConnectionString = connectionString,
                            HandlerAssemblies = [typeof(CatalogsMetadata).Assembly],
                            Bus = new WolverineBusOptions
                            {
                                UseDurableLocalQueues = busOptions.UseDurableLocalQueues,
                                UseEntityFrameworkCoreTransactions =
                                    busOptions.UseEntityFrameworkCoreTransactions,
                            },
                        },
                        Kafka = new WolverineKafkaOptions { ConnectionName = "kafka" },
                    },
                    kafka => kafka.ConfigureCatalogsPublishTopology()
                );
                break;
        }

        return builder;
    }
}
