using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Extensions;
using BuildingBlocks.Integration.Wolverine.Kafka;
using BuildingBlocks.Integration.Wolverine.RabbitMQ;
using ECommerce.Services.Catalogs.Products.Features.CreatingProduct.v1;
using ECommerce.Services.Catalogs.Products.Features.GettingProductById.v1;
using ECommerce.Services.Catalogs.Products.Features.GettingProductReadModels.v1;
using ECommerce.Services.Catalogs.Shared.Extensions.HostApplicationBuilderExtensions;
using ECommerce.Services.Shared.Contracts.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Wolverine;

namespace ECommerce.Services.Catalogs;

public static class ApplicationConfiguration
{
    public const string CatalogModulePrefixUri = "/api/v1/catalogs";

    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.AddStorage();
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
                            HandlerAssemblies = [typeof(ApplicationConfiguration).Assembly],
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
                            HandlerAssemblies = [typeof(ApplicationConfiguration).Assembly],
                            Bus = new WolverineBusOptions
                            {
                                UseDurableLocalQueues = busOptions.UseDurableLocalQueues,
                                UseEntityFrameworkCoreTransactions =
                                    busOptions.UseEntityFrameworkCoreTransactions,
                            },
                        },
                        Kafka = new WolverineKafkaOptions { ConnectionName = "kafka" },
                    },
                    kafka =>
                        kafka.Publish<ECommerce.Services.Shared.Contracts.MessageEnvelope.MessageEnvelope<ECommerce.Services.Shared.Contracts.IntegrationEvents.ProductCreatedV1>>(
                            MessagingConstants.ProductCreatedTopic
                        )
                );
                break;
        }

        return builder;
    }

    public static IEndpointRouteBuilder MapApplicationEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        var group = endpoints.MapGroup(CatalogModulePrefixUri);

        group.MapCreateProductEndpoint();
        group.MapGetProductByIdEndpoint();
        group.MapGetProductReadModelsEndpoint();

        return endpoints;
    }
}
