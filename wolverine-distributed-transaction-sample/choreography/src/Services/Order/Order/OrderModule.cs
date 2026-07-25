using BuildingBlocks.Integration.Wolverine;
using BuildingBlocks.Integration.Wolverine.Configuration;
using Contracts.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Order.Orders.Features.CreatingOrder.v1;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace Order;

public static class OrderModule
{
    public const string OrderModulePrefixUri = "/api/v1/orders";

    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        // ── Database ──
        builder.AddOrderStorage();

        // ── Wolverine + RabbitMQ ──
        var transport = builder.Configuration.GetMessagingTransport();
        var connectionString =
            builder.Configuration.GetConnectionString("ordersdb")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ordersdb");

        builder.AddTransactionalWolverine(
            transport,
            cfg =>
            {
                cfg.ConfigureWolverine(opts =>
                {
                    // Publish OrderCreated event → Payment service listens
                    opts.PublishMessage<OrderCreated>()
                        .ToRabbitQueue(MessagingConstants.OrderEventsQueue);

                    // Listen for Payment responses
                    opts.ListenToRabbitQueue(MessagingConstants.PaymentEventsQueue)
                        .ListenerCount(1);

                    // Persist scheduled messages (OrderTimeoutCheck) via PostgreSQL
                    opts.PersistMessagesWithPostgresql(connectionString);
                    opts.UseEntityFrameworkCoreTransactions();
                });

                cfg.ScanHandlers(typeof(OrderModule).Assembly);
            }
        );

        return builder;
    }

    public static IEndpointRouteBuilder MapApplicationEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        var group = endpoints.MapGroup(OrderModulePrefixUri);
        group.MapCreateOrderEndpoint();
        return endpoints;
    }
}
