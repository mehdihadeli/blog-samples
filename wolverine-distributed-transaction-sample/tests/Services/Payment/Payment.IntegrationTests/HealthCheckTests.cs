using Shouldly;

namespace Payment.IntegrationTests;

public sealed class HealthCheckTests(PaymentSharedFixture sharedFixture)
    : PaymentIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task health_check_should_return_ok()
    {
        // Act
        var response = await Factory.CreateClient().GetAsync("/");

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }
}
