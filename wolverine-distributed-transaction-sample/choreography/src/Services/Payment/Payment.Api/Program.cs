using Microsoft.Extensions.Hosting;
using Payment;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddApplicationServices();

var app = builder.Build();
app.MapGet("/", () => Results.Ok(new { service = nameof(PaymentModule), status = "running" }));
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
