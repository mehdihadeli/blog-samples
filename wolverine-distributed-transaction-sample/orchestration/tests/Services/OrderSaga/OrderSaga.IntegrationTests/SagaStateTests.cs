using Contracts.Messages;
using Microsoft.EntityFrameworkCore;
using OrderSaga.Orders.Features.CreatingOrder.v1;
using OrderSaga.Orders.Features.ProcessingOrderPayment.v1;
using OrderSaga.Orders.Models;
using OrderSaga.Shared.Data;
using Shouldly;
using Wolverine;

namespace OrderSaga.IntegrationTests;

/// <summary>
/// Pure saga state machine tests — validate the domain logic of OrderSagaState
/// directly without any external infrastructure (no HTTP, no RabbitMQ).
///
/// These test the saga's Start/Handle methods in isolation using a real
/// OrderDbContext backed by the TestContainers PostgreSQL.
/// </summary>
public sealed class SagaStateTests(OrderSagaSharedFixture sharedFixture)
    : OrderSagaIntegrationTestBase(sharedFixture)
{
    [Fact]
    public void start_saga_should_return_outgoing_messages()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        // Act
        var (saga, outgoing) = OrderSagaState.Start(new StartOrder(orderId));

        // Assert — saga state
        saga.ShouldNotBeNull();
        saga.Id.ShouldBe(orderId);

        // Assert — outgoing messages
        var messages = outgoing.ToList();
        messages.ShouldContain(m => m is ProcessPayment);
        messages.ShouldContain(m => m is OrderTimeout);

        var paymentMsg = messages.OfType<ProcessPayment>().Single();
        paymentMsg.OrderId.ShouldBe(orderId);

        var timeoutMsg = messages.OfType<OrderTimeout>().Single();
        timeoutMsg.OrderId.ShouldBe(orderId);
    }

    [Fact]
    public async Task handle_payment_processed_should_confirm_order_and_complete_saga()
    {
        // Arrange — create order first
        var orderId = Guid.NewGuid();
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            db.Orders.Add(Order.Create("Confirm Test", 100m));
            await db.SaveChangesAsync();

            var order = await db.Orders.FirstAsync();
            orderId = order.Id;
            order.Status.ShouldBe(OrderStatus.Pending);
        });

        // Act — simulate saga handling PaymentProcessed
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            var saga = new OrderSagaState { Id = orderId };
            var processed = new PaymentProcessed(orderId, "TXN-test-123");

            saga.Handle(processed, db);
            await db.SaveChangesAsync();
        });

        // Assert
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            var order = await db.Orders.FindAsync(orderId);
            order.ShouldNotBeNull();
            order.Status.ShouldBe(OrderStatus.Confirmed);
        });
    }

    [Fact]
    public async Task handle_payment_failed_should_cancel_order_and_complete_saga()
    {
        // Arrange — create order first
        var orderId = Guid.NewGuid();
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            var order = Order.Create("Cancel Test", 100m);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        });

        // Act — simulate saga handling PaymentFailed
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            var saga = new OrderSagaState { Id = orderId };
            var failed = new PaymentFailed(orderId, "Insufficient funds");

            saga.Handle(failed, db);
            await db.SaveChangesAsync();
        });

        // Assert
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            var order = await db.Orders.FindAsync(orderId);
            order.ShouldNotBeNull();
            order.Status.ShouldBe(OrderStatus.Cancelled);
        });
    }

    [Fact]
    public async Task handle_order_timeout_should_timeout_order_and_complete_saga()
    {
        // Arrange — create order first
        var orderId = Guid.NewGuid();
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            var order = Order.Create("Timeout Test", 100m);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        });

        // Act — simulate saga handling OrderTimeout
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            var saga = new OrderSagaState { Id = orderId };
            var timeout = new OrderTimeout(orderId);

            saga.Handle(timeout, db);
            await db.SaveChangesAsync();
        });

        // Assert
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            var order = await db.Orders.FindAsync(orderId);
            order.ShouldNotBeNull();
            order.Status.ShouldBe(OrderStatus.TimedOut);
        });
    }

    [Fact]
    public async Task confirm_already_confirmed_order_should_throw()
    {
        // Arrange
        var order = Order.Create("Double Confirm", 100m);
        order.Confirm();
        order.Status.ShouldBe(OrderStatus.Confirmed);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => order.Confirm());
    }

    [Fact]
    public async Task cancel_already_confirmed_order_should_throw()
    {
        // Arrange
        var order = Order.Create("Cancel Confirmed", 100m);
        order.Confirm();
        order.Status.ShouldBe(OrderStatus.Confirmed);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => order.Cancel("any reason"));
    }
}
