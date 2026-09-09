using AspireShop.Frontend.Components;
using AspireShop.Frontend.Services;
using AspireShop.GrpcBasket;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpForwarderWithServiceDiscovery();

builder.Services.AddHttpServiceReference<CatalogServiceClient>(
    "http://catalogservice",
    healthRelativePath: "health"
);

var isHttps = builder.Configuration["DOTNET_LAUNCH_PROFILE"] == "https";

builder
    .Services.AddSingleton<BasketServiceClient>()
    .AddGrpcServiceReference<Basket.BasketClient>(
        $"{(isHttps ? "https" : "http")}://basketservice",
        failureStatus: HealthStatus.Degraded
    );

builder.Services.AddRazorComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseStaticFiles();

app.MapRazorComponents<App>();

app.MapForwarder(
    "/catalog/images/{id}",
    "https+http://catalogservice",
    "/api/v1/catalog/items/{id}/image"
);

app.MapDefaultEndpoints();

app.Run();
