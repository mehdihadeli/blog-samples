using ECommerce.Services.Orders.Shared.Data;
using ECommerce.Services.Shared.Contracts.Messaging;
using Tests.Shared.TestBase;

namespace ECommerce.Services.Orders.IntegrationTests;

[Xunit.Collection(IntegrationTestCollection.Name)]
public abstract class OrdersIntegrationTestBase : IntegrationTestBase<Program, OrdersSharedFixture>
{
    protected OrdersIntegrationTestBase(OrdersSharedFixture sharedFixture)
        : base(sharedFixture) { }

    protected override string MessagingTransport => "rabbitmq";

    protected override async Task ResetStateAsync()
    {
        if (string.Equals(MessagingTransport, "kafka", StringComparison.OrdinalIgnoreCase))
        {
            await Kafka.EnsureTopicsAsync(
                [
                    MessagingConstants.ProductCreatedTopic,
                    MessagingConstants.OrdersProductsDeadLetterTopic,
                ]
            );
        }

        await ExecuteDbContextAsync<OrdersDbContext>(_ => Task.CompletedTask);
    }

    protected Task ExecuteOrdersDbContextAsync(Func<OrdersDbContext, Task> action) =>
        ExecuteDbContextAsync(action);
}
