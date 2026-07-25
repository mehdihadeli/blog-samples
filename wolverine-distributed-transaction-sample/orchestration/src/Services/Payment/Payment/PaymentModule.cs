using BuildingBlocks.Integration.Wolverine;
using BuildingBlocks.Integration.Wolverine.Configuration;
using Contracts.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Payment;

public static class PaymentModule
{
    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        var transport = builder.Configuration.GetMessagingTransport();

        builder.AddTransactionalWolverine(transport, cfg =>
        {
            cfg.ConfigureWolverine(opts =>
            {
                // Listen for payment requests from OrderSaga
                opts.ListenToRabbitQueue(MessagingConstants.PaymentRequestsQueue)
                    .ListenerCount(1);

                // Publish responses back to OrderSaga
                opts.PublishMessage<PaymentProcessed>()
                    .ToRabbitQueue(MessagingConstants.OrderPaymentResponsesQueue);
                opts.PublishMessage<PaymentFailed>()
                    .ToRabbitQueue(MessagingConstants.OrderPaymentResponsesQueue);
            });

            cfg.ScanHandlers(typeof(PaymentModule).Assembly);
        });

        return builder;
    }
}
