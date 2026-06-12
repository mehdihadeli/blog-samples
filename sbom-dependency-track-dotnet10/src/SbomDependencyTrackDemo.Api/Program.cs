var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet(
    "/",
    () =>
        Results.Ok(
            new
            {
                Name = "SbomDependencyTrackDemo.Api",
                Runtime = Environment.Version.ToString(),
                Utc = DateTimeOffset.UtcNow,
            }
        )
);

app.MapGet(
    "/dependencies",
    () =>
        Results.Ok(
            new[] { "Microsoft.AspNetCore.OpenApi", "Dapper", "NodaTime", "Serilog.AspNetCore" }
        )
);

app.Run();
