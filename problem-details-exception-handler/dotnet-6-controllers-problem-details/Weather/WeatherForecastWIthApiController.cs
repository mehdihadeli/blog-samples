using DotNet6ProblemDetails.Core.Exceptions;
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

    [HttpGet("api-controller/return-error-object")]
    public async Task<ActionResult<WeatherForecast[]>> ReturnErrorObject()
    {
        var forecast = Enumerable
            .Range(1, 5)
            .Select(index => new WeatherForecast(
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]
            ))
            .ToArray();

        if (forecast.Length > 100)
        {
            return forecast;
        }

        // https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-2.2#problem-details-for-error-status-codes-3
        // when our controller has a `ApiController` attribute it uses `ClientErrorResultFilter` to generate problem details for status codes > 400 and when they are not exceptional with using CustomProblemDetialsFactory
        return Conflict();
    }

    [HttpGet("api-controller/throw-badrequest-exception")]
    public async Task<ActionResult<WeatherForecast[]>> ThrowBadRequest()
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

        return forecast;
    }
}
