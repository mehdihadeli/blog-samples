using System.Reflection;
using Xunit;
using Xunit.v3;

[assembly: TestCaseOrderer(typeof(AgentGateway.Samples.Tests.GatewayTestCaseOrderer))]

namespace AgentGateway.Samples.Tests;

public sealed class GatewayTestCaseOrderer : ITestCaseOrderer
{
    IReadOnlyCollection<TTestCase> ITestCaseOrderer.OrderTestCases<TTestCase>(
        IReadOnlyCollection<TTestCase> testCases
    )
    {
        return testCases
            .OrderBy(testCase => GetPriority(testCase!))
            .ThenBy(testCase => GetTestName(testCase!), StringComparer.Ordinal)
            .ToArray();
    }

    private static int GetPriority(object testCase)
    {
        var name = GetTestName(testCase);
        if (name.Contains("Z_Excessive_burst", StringComparison.Ordinal))
        {
            return 2;
        }

        if (name.Contains("Normal_load_is_allowed", StringComparison.Ordinal))
        {
            return 0;
        }

        return 1;
    }

    private static string GetTestName(object testCase)
    {
        var testMethod = testCase.GetType().GetProperty("TestMethod")?.GetValue(testCase);
        var method = testMethod?.GetType().GetProperty("Method")?.GetValue(testMethod);
        return method?.GetType().GetProperty("Name")?.GetValue(method)?.ToString()
            ?? testCase.ToString()
            ?? string.Empty;
    }
}
