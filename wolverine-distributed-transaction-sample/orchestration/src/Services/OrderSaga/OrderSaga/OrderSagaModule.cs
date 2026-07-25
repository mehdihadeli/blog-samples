using BuildingBlocks.Integration.Wolverine;
using BuildingBlocks.Integration.Wolverine.Configuration;
using Contracts.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OrderSaga.Orders.Features.CreatingOrder.v1;
using OrderSaga.Orders.Features.GettingOrderById.v1;
using OrderSaga.Shared.Extensions.HostApplicationBuilderExtensions;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace OrderSaga;

public static class OrderSagaModule
{
    public const string OrderSagaModulePrefixUri = "/api/v1/orders";

    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        // ── Database ──
        builder.AddOrderSagaStorage();

        // ── Wolverine + RabbitMQ ──
        var transport = builder.Configuration.GetMessagingTransport();
        var connectionString = builder.Configuration.GetConnectionString("ordersdb")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ordersdb");

        builder.AddTransactionalWolverine(transport, cfg =>
        {
            cfg.ConfigureWolverine(opts =>
            {
                // Publish ProcessPayment → Payment service
                opts.PublishMessage<ProcessPayment>()
                    .ToRabbitQueue(MessagingConstants.PaymentRequestsQueue);

                // Listen for Payment responses
                opts.ListenToRabbitQueue(MessagingConstants.OrderPaymentResponsesQueue)
                    .ListenerCount(1);

                // Durable saga storage via Postgresql
                opts.PersistMessagesWithPostgresql(connectionString);
                opts.UseEntityFrameworkCoreTransactions();
            });

            cfg.ScanHandlers(typeof(OrderSagaModule).Assembly);
        });

        return builder;
    }

    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(OrderSagaModulePrefixUri);
        group.MapCreateOrderEndpoint();
        group.MapGetOrderEndpoint();
        return endpoints;
    }
}
