using System.Text.Json;

namespace McpGateway.Tests;

internal static class McpTestHelpers
{
    public static List<string> ToolNames(JsonElement result)
    {
        var names = new List<string>();
        foreach (var tool in result.GetProperty("tools").EnumerateArray())
        {
            names.Add(tool.GetProperty("name").GetString()!);
        }

        return names;
    }
}
