using ECommerce.Services.Orders.Products.Features.ConsumingProductCreated.v1;
using ECommerce.Services.Orders.Shared.Data;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tests.Shared.Fixtures;
using Xunit;

namespace ECommerce.Services.Orders.IntegrationTests.Products.Features.ConsumingProductCreated.v1;

public class ErrorQueueTests(OrdersSharedFixture sharedFixture)
    : OrdersIntegrationTestBase(sharedFixture)
{
    protected override string MessagingTransport => "rabbitmq";

    [Fact]
    public async Task FaultyRabbitMqMessage_ShouldBeMovedToErrorQueue_WhenConsumerFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var _ = Factory.CreateClient();

        var envelope = MessageEnvelope.Create(
            new ProductCreatedV1(
                Guid.NewGuid(),
                "faulty-product-created",
                "Faulty Basket",
                9.99m,
                DateTime.UtcNow
            )
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => InvokeProductCreatedConsumerAsync(envelope, cancellationToken)
        );

        Assert.Equal(
            "Intentional consumer failure for retry and dead-letter tests.",
            exception.Message
        );
    }

    [Fact]
    public async Task FaultyRabbitMqMessage_ShouldNotPersistImportedProduct_WhenConsumerFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var _ = Factory.CreateClient();

        var envelope = MessageEnvelope.Create(
            new ProductCreatedV1(
                Guid.NewGuid(),
                "faulty-product-created",
                "Faulty Basket",
                9.99m,
                DateTime.UtcNow
            )
        );

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => InvokeProductCreatedConsumerAsync(envelope, cancellationToken)
        );

        var importedCount = await ExecuteDbContextAsync<OrdersDbContext, int>(dbContext =>
            dbContext.ImportedProducts.CountAsync(
                x => x.Id == envelope.Message.ProductId,
                cancellationToken
            )
        );

        Assert.Equal(0, importedCount);
    }

    private async Task InvokeProductCreatedConsumerAsync(
        MessageEnvelope<ProductCreatedV1> envelope,
        CancellationToken cancellationToken
    )
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var consumer = scope.ServiceProvider.GetRequiredService<ProductCreatedConsumer>();
        var context = Mock.Of<MassTransit.ConsumeContext<MessageEnvelope<ProductCreatedV1>>>(x =>
            x.Message == envelope && x.CancellationToken == cancellationToken
        );

        await consumer.Consume(context);
    }
}
