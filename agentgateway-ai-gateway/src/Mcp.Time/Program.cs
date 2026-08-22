using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder
    .Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<TimeTools>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapMcp("/mcp");
app.Run();

[McpServerToolType]
public sealed class TimeTools
{
    [McpServerTool(Name = "get_current_time")]
    public static string GetCurrentTime(string? timeZone = null)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            return DateTimeOffset.UtcNow.ToString("O");
        }

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).ToString("O");
        }
        catch (TimeZoneNotFoundException)
        {
            return $"Unknown time zone: {timeZone}";
        }
    }
}
