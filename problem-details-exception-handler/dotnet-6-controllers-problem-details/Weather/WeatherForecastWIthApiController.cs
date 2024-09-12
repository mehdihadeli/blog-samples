using Microsoft.AspNetCore.Mvc;

namespace DotNet6ProblemDetails.Weather;

// https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-6.0#problem-details-for-error-status-codes-1
[ApiController]
public class WeatherForecastWIthApiController : ControllerBase
{
    private static readonly string[] Summaries =
    {
        "Freezing",
        "Bracing",
        "Chilly",
        "Cool",
        "Mild",
        "Warm",
        "Balmy",
        "Hot",
        "Sweltering",
        "Scorching",
    };

    [HttpGet("weatherforecast")]
    public Task<ConflictResult> GetWeatherForecast()
    {
        var forecast = Enumerable
            .Range(1, 5)
            .Select(index => new WeatherForecast(
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]
            ))
            .ToArray();

        // https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-2.2#problem-details-for-error-status-codes-3
        // when our controller has a `ApiController` attribute it uses `ClientErrorResultFilter` to generate problem details for status codes > 400 and when they are not exceptional with using CustomProblemDetialsFactory
        return Task.FromResult(Conflict());
    }
}
