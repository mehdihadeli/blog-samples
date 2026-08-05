using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ECommerce.Services.Catalogs.Shared.Extensions.WebApplicationExtensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseInfrastructure(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapGet(
            "/",
            () => Results.Ok(new { service = CatalogsMetadata.ModuleName, status = "running" })
        );

        return app;
    }
}
