using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace DotnetTektonSample.Tests;

public class WeatherForecastTests
{
    [Fact]
    public async Task Should_ReturnFiveValidForecasts_When_WeatherEndpointIsRequested()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync("/weatherforecast");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetArrayLength().ShouldBe(5);
        foreach (var forecast in body.RootElement.EnumerateArray())
        {
            var temperature = forecast.GetProperty("temperatureC").GetInt32();
            temperature.ShouldBeInRange(-20, 54);
            forecast.GetProperty("temperatureF").GetInt32().ShouldBe(32 + (int)(temperature / 0.5556));
            forecast.GetProperty("summary").GetString().ShouldBeOneOf(
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching");
            DateOnly.TryParse(forecast.GetProperty("date").GetString(), out _).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_UnknownEndpointIsRequested()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync("/missing");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}