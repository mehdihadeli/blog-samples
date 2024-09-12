using DotNet7WithoutProblemDetails.Weather;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("test"))
{
    // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/handle-errrors
    app.UseDeveloperExceptionPage();
}
else
{
    // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling
    // Does nothing if a response body has already been provided. when our next `DeveloperExceptionMiddleware` is written response for exception (in dev mode) when we back to `ExceptionHandlerMiddlewareImpl` because `context.Response.HasStarted` it doesn't do anything
    // By default `ExceptionHandlerMiddlewareImpl` middleware register original exceptions with `IExceptionHandlerFeature` feature, we don't have this in `DeveloperExceptionPageMiddleware` and we should handle it with a middleware like `CaptureExceptionMiddleware`
    // Just for handling exceptions in production mode
    // https://github.com/dotnet/aspnetcore/pull/26567
    app.UseExceptionHandler(
        options: new ExceptionHandlerOptions { AllowStatusCode404Response = true }
    );
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGetWeatherForecastEndpoint();
app.MapGetWeatherForecast2Endpoint();

app.Run();
