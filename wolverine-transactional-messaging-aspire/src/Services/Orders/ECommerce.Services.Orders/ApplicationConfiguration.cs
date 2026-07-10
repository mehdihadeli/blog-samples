using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Extensions;
using BuildingBlocks.Integration.Wolverine.Kafka;
using BuildingBlocks.Integration.Wolverine.RabbitMQ;
using ECommerce.Services.Orders.Products.Features.GettingImportedProductById.v1;
using ECommerce.Services.Orders.Products.Features.GettingImportedProducts.v1;
using ECommerce.Services.Orders.Shared.Extensions.HostApplicationBuilderExtensions;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using ECommerce.Services.Shared.Contracts.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Wolverine;

namespace ECommerce.Services.Orders;

public static class ApplicationConfiguration
{
    public const string OrdersModulePrefixUri = "/api/v1/orders";

    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.AddStorage();
        var transport = builder.Configuration.GetMessagingTransport();

        var connectionString =
            builder.Configuration.GetConnectionString("ordersdb")
            ?? throw new InvalidOperationException("Missing connection string 'ordersdb'.");
        var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
        var busOptions =
            builder.Configuration.GetSection("Wolverine").Get<WolverineBusOptions>()
            ?? new WolverineBusOptions();
        var useDurableInboxOnAllListeners =
            builder
                .Configuration.GetSection("Wolverine")
                .GetValue<bool?>(nameof(WolverineBusOptions.UseDurableInboxOnAllListeners)) ?? true;

        busOptions.DeadLetterQueueName ??= MessagingConstants.DeadLetterQueueName;

        builder.Host.AddWolverineMessaging(
            new WolverineIntegrationOptions
            {
                DurableStorageConnectionString = connectionString,
                RabbitMqConnectionString = rabbitMqConnectionString,
                Bus = new WolverineBusOptions
                {
                    ConfigureRabbitMqTopology = transport == MessagingTransportType.RabbitMq,
                    UseDurableLocalQueues = false,
                    UseDurableInboxOnAllListeners = useDurableInboxOnAllListeners,
                    UseEntityFrameworkCoreTransactions =
                        busOptions.UseEntityFrameworkCoreTransactions,
                    UseNativeDeadLetterQueue = busOptions.UseNativeDeadLetterQueue,
                    DeadLetterQueueName = busOptions.DeadLetterQueueName,
                },
            },
            options =>
            {
                options.Discovery.IncludeAssembly(typeof(ApplicationConfiguration).Assembly);

                switch (transport)
                {
                    case MessagingTransportType.RabbitMq:
                        options.UseRabbitMqTransport(
                            "rabbitmq",
                            rabbitMq =>
                                rabbitMq
                                    .ListenToRabbitQueueTransport(
                                        MessagingConstants.ProductCreatedQueue,
                                        busOptions
                                    )
                                    .DefaultIncomingMessage<MessageEnvelope<ProductCreatedV1>>(),
                            busOptions
                        );
                        break;

                    case MessagingTransportType.Kafka:
                        options.UseKafkaTransport(
                            "kafka",
                            kafka =>
                                kafka
                                    .ListenToKafkaTopicTransport(
                                        MessagingConstants.ProductCreatedTopic,
                                        MessagingConstants.OrdersProductsConsumerGroup,
                                        busOptions
                                    )
                                    .DefaultIncomingMessage<MessageEnvelope<ProductCreatedV1>>(),
                            busOptions
                        );
                        break;
                }
            }
        );

        return builder;
    }

    public static IEndpointRouteBuilder MapApplicationEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        var group = endpoints.MapGroup(OrdersModulePrefixUri);

        group.MapGetImportedProductsEndpoint();
        group.MapGetImportedProductByIdEndpoint();

        return endpoints;
    }
}
