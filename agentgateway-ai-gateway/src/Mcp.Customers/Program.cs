using Mcp.Customers;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder
    .Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithTools<CustomerTools>();

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapMcp("/mcp");

app.Run();
