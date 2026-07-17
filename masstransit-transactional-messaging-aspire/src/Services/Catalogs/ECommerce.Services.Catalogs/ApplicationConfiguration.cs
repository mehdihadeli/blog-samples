using BuildingBlocks.DurableLocalQueue;
using BuildingBlocks.Integration.MassTransit.Configuration;
using BuildingBlocks.Integration.MassTransit.Kafka;
using BuildingBlocks.Integration.MassTransit.Options;
using BuildingBlocks.Integration.MassTransit.RabbitMQ;
using ECommerce.Services.Catalogs.Products.Features.CreatingProduct.v1;
using ECommerce.Services.Catalogs.Products.Features.ProjectingProductReadModel.v1;
using ECommerce.Services.Catalogs.Shared.Contracts;
using ECommerce.Services.Catalogs.Shared.Data;
using ECommerce.Services.Catalogs.Shared.Extensions.HostApplicationBuilderExtensions;
using ECommerce.Services.Catalogs.Shared.ReadModels;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.InternalCommands;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using ECommerce.Services.Shared.Contracts.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        var options = new MassTransitOptions
        {
            DurableStorageConnectionString = connectionString,
            RabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq"),
            KafkaConnectionString = builder.Configuration.GetConnectionString("kafka"),
            Bus = new MassTransitBusOptions { UseBusOutbox = true, UsePostCommitMediator = true },
        };

        switch (transport)
        {
            case MessagingTransportType.RabbitMq:
                builder.Services.AddMassTransitRabbitMq<CatalogsDbContext>(
                    options,
                    new MassTransitRabbitMqRegistrationOptions
                    {
                        ConfigureTransport = (_, cfg) =>
                        {
                            cfg.PublishToRabbitQueue<MessageEnvelope<ProductCreatedV1>>(
                                MessagingConstants.ProductCreatedQueue
                            );
                        },
                    },
                    builder.Environment
                );
                break;
            case MessagingTransportType.Kafka:
                builder.Services.AddMassTransitKafka<CatalogsDbContext>(
                    options,
                    new MassTransitKafkaRegistrationOptions
                    {
                        ConfigureRider = rider =>
                        {
                            MassTransit.KafkaProducerRegistrationExtensions.AddProducer<
                                MessageEnvelope<ProductCreatedV1>
                            >(rider, MessagingConstants.ProductCreatedTopic);
                        },
                        ConfigurePublisher = services =>
                        {
                            services.AddKafkaMessagePublisher<MessageEnvelope<ProductCreatedV1>>();
                        },
                    },
                    builder.Environment
                );
                break;
        }

        // Register the durable local queue — replaces the mediator-based IInternalCommandBus
        // with a Wolverine-style durable outbox table + background processor.
        // MUST be after MassTransit registration to override its IInternalCommandBus.
        builder.Services.AddDurableLocalQueue<CatalogsDbContext>();

        // Register the handler for the ProjectProductReadModel internal command.
        // This handler is invoked by the DurableCommandProcessor when it polls the outbox table.
        builder.Services.AddDurableCommandHandler<ProjectProductReadModel>(
            async (command, sp, ct) =>
            {
                var repository = sp.GetRequiredService<IProductReadRepository>();
                await repository.UpsertAsync(
                    new ProductReadModel(
                        command.ProductId,
                        command.Code,
                        command.Name,
                        command.Price,
                        command.CreatedAtUtc
                    ),
                    ct
                );
            }
        );

        return builder;
    }

    public static IEndpointRouteBuilder MapApplicationEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        var group = endpoints.MapGroup(CatalogModulePrefixUri);
        group.MapCreateProductEndpoint();
        return endpoints;
    }
}
