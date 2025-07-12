using Microsoft.AspNetCore.Http.HttpResults;
using simple.Core.Exceptions;

namespace simple.Weather;

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

			// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses
			// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/openapi?view=aspnetcore-7.0#multiple-response-types
			return Task.FromResult<Results<Ok<WeatherForecast[]>, ValidationProblem>>(TypedResults.Ok(forecast));
		}
	}
}