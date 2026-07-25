using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Hosting;
using Order;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationServices();

builder.Services.AddOpenApi();
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/", () => Results.Ok(new { service = nameof(OrderModule), status = "running" }));
app.MapApplicationEndpoints();
app.MapDefaultEndpoints();

// Auto-create database on startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Order.Shared.Data.OrderDbContext>();
    await db.Database.EnsureCreatedAsync();
}

await app.RunAsync();

public partial class Program;
