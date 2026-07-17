namespace BuildingBlocks.DurableLocalQueue;

/// <summary>
/// EF Core entity representing a durable message stored in the outbox table.
/// Messages are written in the same transaction as domain changes and processed
/// asynchronously by <see cref="DurableCommandProcessor"/>.
/// </summary>
public sealed class DurableMessage
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Fully qualified CLR type name of the message payload.</summary>
    public string TypeName { get; init; } = default!;

    /// <summary>JSON-serialized message payload.</summary>
    public string Payload { get; init; } = default!;

    /// <summary>Status: Pending, Processing, Completed, Failed.</summary>
    public DurableMessageStatus Status { get; set; } = DurableMessageStatus.Pending;

    /// <summary>UTC timestamp when the message was enqueued.</summary>
    public DateTime EnqueuedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last processing attempt.</summary>
    public DateTime? LastAttemptAtUtc { get; set; }

    /// <summary>Number of processing attempts so far.</summary>
    public int RetryCount { get; set; }

    /// <summary>Error message from the last failed attempt, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>UTC timestamp when the message completed successfully.</summary>
    public DateTime? CompletedAtUtc { get; set; }
}

public enum DurableMessageStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
}
