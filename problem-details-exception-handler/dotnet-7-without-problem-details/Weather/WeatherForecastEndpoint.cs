using DotNet7WithoutProblemDetails.Core.Exceptions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DotNet7WithoutProblemDetails.Weather;

internal static class GetWeatherForecastEndpoint
{
	static readonly string[] Summaries =
	{
		"Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot",
		"Sweltering", "Scorching"
	};

	internal static RouteHandlerBuilder MapGetWeatherForecastEndpoint(this IEndpointRouteBuilder app)
	{
		return app.MapGet("/weatherforecast", Handle)
			.WithName("GetWeatherForecast")
			.WithOpenApi();

		Task<Results<Ok<WeatherForecast[]>, ValidationProblem>> Handle()
		{
			throw new BadRequestException("this is a bad request");

			var forecast = Enumerable.Range(1, 5)
				.Select(
					index =>
						new WeatherForecast(
							DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
							Random.Shared.Next(-20, 55),
							Summaries[Random.Shared.Next(Summaries.Length)]))
				.ToArray();

			return Task.FromResult<Results<Ok<WeatherForecast[]>, ValidationProblem>>(TypedResults.Ok(forecast));
		}
	}
	
	internal static RouteHandlerBuilder MapGetWeatherForecast2Endpoint(this IEndpointRouteBuilder app)
	{
		return app.MapGet("/weatherforecast2", Handle)
			.WithName("GetWeatherForecast2")
			.WithOpenApi();

		Task<IResult> Handle()
		{
			var forecast = Enumerable.Range(1, 5)
				.Select(
					index =>
						new WeatherForecast(
							DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
							Random.Shared.Next(-20, 55),
							Summaries[Random.Shared.Next(Summaries.Length)]))
				.ToArray();

			return Task.FromResult(Results.BadRequest("This is a bad request")) ;
		}
	}
}