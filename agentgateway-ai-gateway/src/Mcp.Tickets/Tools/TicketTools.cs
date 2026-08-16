using ModelContextProtocol.Server;

namespace Mcp.Tickets;

/// <summary>
/// Support ticket tool server. Behind the gateway these tools are exposed as
/// <c>tickets_*</c> and federated with the other MCP targets.
/// </summary>
[McpServerToolType]
public sealed class TicketTools
{
    private static readonly List<Ticket> Tickets =
    [
        new("T-1001", "Login returns 500", "open", "high", "alice", DateTimeOffset.UtcNow.AddDays(-1)),
        new("T-1002", "Invoice PDF not generated", "open", "normal", "bob", DateTimeOffset.UtcNow.AddHours(-5)),
        new("T-1003", "Feature request: dark mode", "closed", "low", "carol", DateTimeOffset.UtcNow.AddDays(-7)),
    ];

    /// <summary>
    /// List support tickets, optionally filtered by status.
    /// </summary>
    [McpServerTool(Name = "tickets_list")]
    public IReadOnlyList<Ticket> ListTickets(string? status = null)
    {
        return string.IsNullOrWhiteSpace(status)
            ? Tickets
            : Tickets.Where(t => t.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Create a new support ticket and return it.
    /// </summary>
    [McpServerTool(Name = "tickets_create")]
    public Ticket CreateTicket(string title, string description, string priority = "normal", string? reporter = null)
    {
        var ticket = new Ticket($"T-{1000 + Tickets.Count + 1}", title, "open", priority, reporter ?? "anonymous", DateTimeOffset.UtcNow)
        {
            Description = description,
        };
        Tickets.Add(ticket);
        return ticket;
    }

    /// <summary>
    /// Update the status of an existing ticket.
    /// </summary>
    [McpServerTool(Name = "tickets_update_status")]
    public Ticket? UpdateStatus(string ticketId, string status)
    {
        var ticket = Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket is null)
        {
            return null;
        }

        ticket.Status = status;
        return ticket;
    }
}

/// <summary>
/// A support ticket.
/// </summary>
public sealed class Ticket
{
    public Ticket(string id, string title, string status, string priority, string reporter, DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        Status = status;
        Priority = priority;
        Reporter = reporter;
        CreatedAt = createdAt;
    }

    public string Id { get; init; }
    public string Title { get; init; }
    public string? Description { get; set; }
    public string Status { get; set; }
    public string Priority { get; init; }
    public string Reporter { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}