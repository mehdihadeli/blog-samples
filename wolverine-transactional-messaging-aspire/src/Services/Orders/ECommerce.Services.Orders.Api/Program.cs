using ECommerce.Services.Orders;
using ECommerce.Services.Orders.Shared.Extensions.WebApplicationExtensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationServices();

var app = builder.Build();

app.UseDefaultServices();
await app.UseInfrastructureAsync();
app.MapApplicationEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
