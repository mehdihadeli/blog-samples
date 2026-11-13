var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("healthy")))
    .WithName("GetHealth");

app.MapGet("/weatherforecast", () => TypedResults.Ok(new ForecastResponse[]
{
    new(1, -5, "Freezing"),
    new(2, 0, "Cold"),
    new(3, 10, "Cool"),
    new(4, 20, "Mild"),
    new(5, 30, "Warm")
})).WithName("GetWeatherForecast");

app.Run();

public sealed record HealthResponse(string Status);

public sealed record ForecastResponse(int Day, int TemperatureC, string Summary)
{
    public int TemperatureF => 32 + TemperatureC * 9 / 5;
}

public partial class Program { }