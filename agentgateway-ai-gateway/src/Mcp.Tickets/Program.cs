using Mcp.Tickets;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder
    .Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Stateless is the default and recommended mode for the 2026-07-28
        // Streamable HTTP wire format: no Mcp-Session-Id, no in-memory session
        // state, horizontal scaling without session affinity.
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithTools<TicketTools>();

var app = builder.Build();
app.MapDefaultEndpoints();

// Expose the Streamable HTTP MCP endpoint.
app.MapMcp("/mcp");

app.Run();
