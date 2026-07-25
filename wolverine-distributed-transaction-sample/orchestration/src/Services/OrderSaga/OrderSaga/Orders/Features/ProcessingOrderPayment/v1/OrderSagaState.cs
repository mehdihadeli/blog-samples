using Contracts.Messages;
using Microsoft.EntityFrameworkCore;
using OrderSaga.Orders.Features.CreatingOrder.v1;
using OrderSaga.Orders.Models;
using OrderSaga.Shared.Data;
using Wolverine;

namespace OrderSaga.Orders.Features.ProcessingOrderPayment.v1;

/// <summary>
/// Wolverine Saga coordinating distributed transaction across OrderSaga + Payment.
///
/// Flow:
///   StartOrder → [saga starts, sends ProcessPayment, schedules OrderTimeout]
///       ↓
///   PaymentProcessed → [order confirmed, saga completes]
///       OR
///   PaymentFailed → [order cancelled (compensation), saga completes]
///       OR
///   OrderTimeout → [order timed out (compensation), saga completes]
/// </summary>
public sealed class OrderSagaState : Saga
{
    public Guid Id { get; set; }

    // ── Start: fire outgoing messages ────────────────────────────────

    public static (OrderSagaState, OutgoingMessages) Start(
        StartOrder command)
    {
        var saga = new OrderSagaState { Id = command.OrderId };

        var outgoing = new OutgoingMessages
        {
            new ProcessPayment(command.OrderId, 0), // amount resolved from DB in real flow
            new OrderTimeout(command.OrderId),
        };

        return (saga, outgoing);
    }

    // ── Happy path: payment succeeded ─────────────────────────────────

    public void Handle(
        PaymentProcessed processed,
        OrderDbContext dbContext)
    {
        var order = dbContext.Orders.Single(o => o.Id == processed.OrderId);
        order.Confirm();
        MarkCompleted();
    }

    // ── Compensation: payment failed ──────────────────────────────────

    public void Handle(
        PaymentFailed failed,
        OrderDbContext dbContext)
    {
        var order = dbContext.Orders.Single(o => o.Id == failed.OrderId);
        order.Cancel(failed.Reason);
        MarkCompleted();
    }

    // ── Compensation: timeout ─────────────────────────────────────────

    public void Handle(
        OrderTimeout timeout,
        OrderDbContext dbContext)
    {
        var order = dbContext.Orders.Single(o => o.Id == timeout.OrderId);
        order.Cancel("timeout");
        MarkCompleted();
    }
}

/// <summary>
/// Timeout — fired if payment takes longer than 30 seconds.
/// </summary>
public sealed record OrderTimeout(Guid OrderId) : TimeoutMessage(TimeSpan.FromSeconds(30));
