using DotNet7CustomizeProblemDetails.Core.Exceptions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DotNet7CustomizeProblemDetails.Weather;

internal static class GetWeatherForecastEndpoint
{
    static readonly string[] Summaries =
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

    internal static RouteHandlerBuilder MapGetWeatherForecastEndpoint(
        this IEndpointRouteBuilder app
    )
    {
        return app.MapGet("/weatherforecast", Handle).WithName("GetWeatherForecast").WithOpenApi();

        Task<Results<Ok<WeatherForecast[]>, ValidationProblem>> Handle()
        {
            // handle exception in the run time and converting to problem details - using DeveloperExceptionPage middleware for catching exception and convert to problem details
            throw new BadRequestException("this is a bad request");

            var forecast = Enumerable
                .Range(1, 5)
                .Select(index => new WeatherForecast(
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    Summaries[Random.Shared.Next(Summaries.Length)]
                ))
                .ToArray();

            return Task.FromResult<Results<Ok<WeatherForecast[]>, ValidationProblem>>(
                TypedResults.Ok(forecast)
            );
        }
    }

    internal static RouteHandlerBuilder MapGetWeatherForecast2Endpoint(
        this IEndpointRouteBuilder app
    )
    {
        return app.MapGet("/weatherforecast2", Handle)
            .WithName("GetWeatherForecast2")
            .WithOpenApi();

        Task<IResult> Handle()
        {
            var forecast = Enumerable
                .Range(1, 5)
                .Select(index => new WeatherForecast(
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    Summaries[Random.Shared.Next(Summaries.Length)]
                ))
                .ToArray();

            // it is not a run time exception but should convert to problem details -  we can't pass value to bad request because it starts the response and `statuscode middleware can't create problem details object`
            return Task.FromResult(Results.BadRequest());
        }
    }

    internal static RouteHandlerBuilder MapGetWeatherForecast3Endpoint(
        this IEndpointRouteBuilder app
    )
    {
        return app.MapGet("/weatherforecast3", Handle).WithName("GetWeatherForecast3");

        Task<IResult> Handle()
        {
            var forecast = Enumerable
                .Range(1, 5)
                .Select(index => new WeatherForecast(
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    Summaries[Random.Shared.Next(Summaries.Length)]
                ))
                .ToArray();

            // it is not a run time exception but should convert to problem details - Problem creates problem details objects with ProblemDetailsFactory
            return Task.FromResult(
                Results.Problem(
                    "this is a bad request",
                    statusCode: StatusCodes.Status400BadRequest
                )
            );
        }
    }
}
