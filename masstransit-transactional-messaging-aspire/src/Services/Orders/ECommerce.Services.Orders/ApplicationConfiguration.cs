using BuildingBlocks.Integration.MassTransit.Configuration;
using BuildingBlocks.Integration.MassTransit.Kafka;
using BuildingBlocks.Integration.MassTransit.Options;
using BuildingBlocks.Integration.MassTransit.RabbitMQ;
using ECommerce.Services.Orders.Products.Features.ConsumingProductCreated.v1;
using ECommerce.Services.Orders.Shared.Data;
using ECommerce.Services.Orders.Shared.Extensions.HostApplicationBuilderExtensions;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using ECommerce.Services.Shared.Contracts.Messaging;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

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
        var options = new MassTransitOptions
        {
            DurableStorageConnectionString = connectionString,
            RabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq"),
            KafkaConnectionString = builder.Configuration.GetConnectionString("kafka"),
            Bus = new MassTransitBusOptions { UseConsumerOutbox = true, UseErrorTransport = true },
        };
        var kafkaBusOptions = new MassTransitBusOptions
        {
            UseConsumerOutbox = false,
            UseBusOutbox = options.Bus.UseBusOutbox,
            UsePostCommitMediator = options.Bus.UsePostCommitMediator,
            UseErrorTransport = options.Bus.UseErrorTransport,
            ErrorQueueName = options.Bus.ErrorQueueName,
            UseEnvelopePublisher = options.Bus.UseEnvelopePublisher,
            Retry = new MassTransitRetryOptions
            {
                MaximumAttempts = options.Bus.Retry.MaximumAttempts,
                UseDelayedRedelivery = false,
                DelayedRedeliveryIntervals = options.Bus.Retry.DelayedRedeliveryIntervals,
            },
        };

        switch (transport)
        {
            case MessagingTransportType.RabbitMq:
                builder.Services.AddMassTransitRabbitMq<OrdersDbContext>(
                    options,
                    new MassTransitRabbitMqRegistrationOptions
                    {
                        ConfigureBus = x =>
                        {
                            x.AddConsumer<
                                ProductCreatedConsumer,
                                ProductCreatedConsumerDefinition
                            >();
                        },
                        ConfigureTransport = (context, cfg) =>
                        {
                            cfg.ReceiveEndpointWithPolicies<
                                OrdersDbContext,
                                ProductCreatedConsumer
                            >(
                                context,
                                options.Bus,
                                MessagingConstants.ProductCreatedQueue,
                                configureEndpoint: endpoint =>
                                {
                                    endpoint.SetQueueArgument(
                                        "x-dead-letter-exchange",
                                        MessagingConstants.OrdersProductsDeadLetterExchange
                                    );
                                    endpoint.SetQueueArgument(
                                        "x-dead-letter-routing-key",
                                        MessagingConstants.OrdersProductsDeadLetterQueue
                                    );
                                    endpoint.BindDeadLetterQueue(
                                        MessagingConstants.OrdersProductsDeadLetterExchange,
                                        MessagingConstants.OrdersProductsDeadLetterQueue,
                                        configure =>
                                        {
                                            configure.RoutingKey =
                                                MessagingConstants.OrdersProductsDeadLetterQueue;
                                        }
                                    );
                                }
                            );
                        },
                    },
                    builder.Environment
                );
                break;
            case MessagingTransportType.Kafka:
                builder.Services.AddMassTransitKafka<OrdersDbContext>(
                    options,
                    new MassTransitKafkaRegistrationOptions
                    {
                        ConfigureBus = x =>
                        {
                            x.AddConsumer<
                                ProductCreatedConsumer,
                                ProductCreatedConsumerDefinition
                            >();
                            x.AddConsumer<KafkaDeadLetterForwardingConsumer>();
                        },
                        ConfigureRider = rider =>
                        {
                            rider.AddConsumer<ProductCreatedConsumer>();
                            rider.AddConsumer<KafkaDeadLetterForwardingConsumer>();
                            rider.AddProducer<MessageEnvelope<ProductCreatedV1>>(
                                MessagingConstants.OrdersProductsDeadLetterTopic
                            );
                        },
                        ConfigureTransport = (context, kafka) =>
                        {
                            kafka.TopicEndpointWithPolicies<
                                OrdersDbContext,
                                MessageEnvelope<ProductCreatedV1>,
                                ProductCreatedConsumer
                            >(
                                context,
                                kafkaBusOptions,
                                MessagingConstants.ProductCreatedTopic,
                                MessagingConstants.OrdersProductsConsumerGroup
                            );
                        },
                    },
                    builder.Environment
                );
                break;
        }

        return builder;
    }

    public static IEndpointRouteBuilder MapApplicationEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapGroup(OrdersModulePrefixUri);
        return endpoints;
    }
}

public sealed class KafkaDeadLetterForwardingConsumer(
    ITopicProducer<MessageEnvelope<ProductCreatedV1>> deadLetterProducer
) : IConsumer<Fault<MessageEnvelope<ProductCreatedV1>>>
{
    public Task Consume(ConsumeContext<Fault<MessageEnvelope<ProductCreatedV1>>> context)
    {
        return deadLetterProducer.Produce(context.Message.Message, context.CancellationToken);
    }
}
