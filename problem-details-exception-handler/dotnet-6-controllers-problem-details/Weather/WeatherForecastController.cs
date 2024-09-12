using DotNet6ProblemDetails.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace DotNet6ProblemDetails.Weather;

public class WeatherForecastController : ControllerBase
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

    [HttpGet("weatherforecast2")]
    public Task<ObjectResult> GetWeatherForecast2()
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
        // it is not a run time exception but should convert to problem details - `Problem` creates problem details objects with `ProblemDetailsFactory` in `ControllerBase`, in minimal apis `Problem` method don't use factory
        return Task.FromResult(
            Problem(detail: "This is a bad request", statusCode: StatusCodes.Status400BadRequest)
        );
    }

    [HttpGet("weatherforecast3")]
    public Task<ObjectResult> GetWeatherForecast3()
    {
        throw new BadRequestException("this is a bad request");

        var forecast = Enumerable
            .Range(1, 5)
            .Select(index => new WeatherForecast(
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]
            ))
            .ToArray();

        // https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-2.2#problem-details-for-error-status-codes-3
        // it is not a run time exception but should convert to problem details - `Problem` creates problem details objects with `ProblemDetailsFactory` in `ControllerBase`, in minimal apis `Problem` method don't use factory
        return Task.FromResult(
            Problem(detail: "This is a bad request", statusCode: StatusCodes.Status400BadRequest)
        );
    }
}
