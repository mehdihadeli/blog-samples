using System.Text.Json;
using ECommerce.Services.Orders;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationServices();
builder.Services.AddOpenApi();
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new { service = OrdersMetadata.ModuleName, status = "running" }));
app.MapApplicationEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
