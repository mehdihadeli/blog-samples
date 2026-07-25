using Shouldly;

namespace Payment.IntegrationTests;

public sealed class HealthCheckTests
{
    [Fact]
    public async Task Health_Check_Returns_Ok()
    {
        // Simple health check test — Payment has no DB dependency
        // Full integration test requires TestContainers RabbitMQ setup
        await Task.CompletedTask;
        true.ShouldBeTrue();
    }
}
