using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

public sealed class ForecastTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public ForecastTests(WebApplicationFactory<Program> application)
    {
        client = application.CreateClient();
    }

    [Fact]
    public async Task Should_ReturnForecastValues_When_ForecastRequested()
    {
        var response = await client.GetAsync("/weatherforecast");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var forecasts = await response.Content.ReadFromJsonAsync<ForecastResponse[]>();
        forecasts.ShouldNotBeNull();
        forecasts.Select(forecast => forecast.Day).ShouldBe(new[] { 1, 2, 3, 4, 5 });
        forecasts.Select(forecast => forecast.TemperatureC).ShouldBe(new[] { -5, 0, 10, 20, 30 });
        forecasts.Select(forecast => forecast.TemperatureF).ShouldBe(new[] { 23, 32, 50, 68, 86 });
        forecasts.Select(forecast => forecast.Summary)
            .ShouldBe(new[] { "Freezing", "Cold", "Cool", "Mild", "Warm" });
    }

    [Fact]
    public async Task Should_ReturnHealthy_When_HealthRequested()
    {
        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        health.ShouldBe(new HealthResponse("healthy"));
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_UnknownEndpointRequested()
    {
        var response = await client.GetAsync("/missing");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}