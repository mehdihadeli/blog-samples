using Shouldly;
using Xunit;

namespace AgentGateway.Samples.Tests;

public sealed class CompilationTests
{
    [Fact]
    public void Test_project_loads_and_runs()
    {
        // Sentinel test that always passes. It guarantees the run is not
        // reported as "zero tests ran" when the Docker stack is down and
        // all integration tests skip.
        true.ShouldBeTrue();
    }
}
