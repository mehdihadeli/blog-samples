using Microsoft.Extensions.Hosting;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

// When running under Aspire AppHost, WithReference(api) injects the API
// endpoint URL as "services:ecommerce-api:http:0" in configuration.
// Use it to configure YARP dynamically; otherwise fall back to appsettings.json
// (which uses Docker internal hostnames for docker-compose).
var apiEndpoint = builder.Configuration["services:ecommerce-api:http:0"];
if (!string.IsNullOrEmpty(apiEndpoint))
{
    // Running under Aspire — use the injected service endpoint
    var routes = new[]
    {
        new RouteConfig
        {
            RouteId = "ecommerce-route",
            ClusterId = "ecommerce-cluster",
            Match = new RouteMatch { Path = "{**catch-all}" },
        },
    };
    var clusters = new[]
    {
        new ClusterConfig
        {
            ClusterId = "ecommerce-cluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                {
                    "ecommerce-api",
                    new DestinationConfig { Address = apiEndpoint }
                },
            },
        },
    };
    builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);
}
else
{
    // Running standalone or Docker Compose — load static config from appsettings.json
    builder
        .Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
}

var app = builder.Build();

app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();
