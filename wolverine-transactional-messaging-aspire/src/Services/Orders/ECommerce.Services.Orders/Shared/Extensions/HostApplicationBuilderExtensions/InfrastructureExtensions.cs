using System.Text.Json;
using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Extensions;
using BuildingBlocks.Integration.Wolverine.Kafka;
using BuildingBlocks.Integration.Wolverine.RabbitMQ;
using ECommerce.Services.Shared.Contracts.Messaging;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Services.Orders.Shared.Extensions.HostApplicationBuilderExtensions;

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
            cfg.RegisterServicesFromAssembly(typeof(OrdersMetadata).Assembly);
        });
        builder.Services.AddValidatorsFromAssembly(typeof(OrdersMetadata).Assembly);
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

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

        switch (transport)
        {
            case MessagingTransportType.RabbitMq:
                builder.AddWolverineRabbitMq(
                    new WolverineRabbitMqRegistrationOptions
                    {
                        Common = new WolverineCommonOptions
                        {
                            DurableStorageConnectionString = connectionString,
                            HandlerAssemblies = [typeof(OrdersMetadata).Assembly],
                            Bus = new WolverineBusOptions
                            {
                                UseDurableLocalQueues = false,
                                UseDurableInboxOnAllListeners = useDurableInboxOnAllListeners,
                                UseEntityFrameworkCoreTransactions =
                                    busOptions.UseEntityFrameworkCoreTransactions,
                                UseNativeDeadLetterQueue = busOptions.UseNativeDeadLetterQueue,
                                DeadLetterQueueName = busOptions.DeadLetterQueueName,
                            },
                        },
                        RabbitMq = new WolverineRabbitMqOptions
                        {
                            ConnectionName = "rabbitmq",
                            ConnectionString = rabbitMqConnectionString,
                        },
                    },
                    rabbitMq => rabbitMq.ConfigureOrdersConsumeTopology()
                );
                break;

            case MessagingTransportType.Kafka:
                builder.AddWolverineKafka(
                    new WolverineKafkaRegistrationOptions
                    {
                        Common = new WolverineCommonOptions
                        {
                            DurableStorageConnectionString = connectionString,
                            HandlerAssemblies = [typeof(OrdersMetadata).Assembly],
                            Bus = new WolverineBusOptions
                            {
                                UseDurableLocalQueues = false,
                                UseDurableInboxOnAllListeners = useDurableInboxOnAllListeners,
                                UseEntityFrameworkCoreTransactions =
                                    busOptions.UseEntityFrameworkCoreTransactions,
                                UseNativeDeadLetterQueue = busOptions.UseNativeDeadLetterQueue,
                                DeadLetterQueueName = busOptions.DeadLetterQueueName,
                            },
                        },
                        Kafka = new WolverineKafkaOptions { ConnectionName = "kafka" },
                    },
                    kafka => kafka.ConfigureOrdersConsumeTopology()
                );
                break;
        }

        return builder;
    }
}
