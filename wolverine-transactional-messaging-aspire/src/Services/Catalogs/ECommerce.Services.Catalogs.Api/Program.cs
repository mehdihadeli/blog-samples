using ECommerce.Services.Catalogs;
using ECommerce.Services.Catalogs.Shared.Extensions.HostApplicationBuilderExtensions;
using ECommerce.Services.Catalogs.Shared.Extensions.WebApplicationExtensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructure();
builder.AddApplicationServices();

var app = builder.Build();

app.UseDefaultServices();
app.UseInfrastructure();
app.MapApplicationEndpoints();
app.MapDefaultEndpoints();

app.Run();
