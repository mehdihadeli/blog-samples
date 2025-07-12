using simple.Core.ProblemDetail;
using simple.Weather;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCustomProblemDetails(problemDetailsOptions =>
{
    // customization problem details should go here
    problemDetailsOptions.CustomizeProblemDetails =
        problemDetailContext =>
        {
            // https://github.com/dotnet/aspnetcore/issues/4765
            // https://github.com/dotnet/aspnetcore/pull/47760
            // `problemDetailContext` doesn't contain real `exception` it will add in this pull request in .net 8 preview 5
            // with help of capture exception middleware for capturing actual exception and filling `IExceptionHandlerFeature` interface but in .net 8 preview 5 it fixed and we don't need capture exception middleware
            // .net 8 will add `IExceptionHandlerFeature` in `DisplayExceptionContent` and `SetExceptionHandlerFeatures` methods `DeveloperExceptionPageMiddlewareImpl` class, exact functionality of CaptureException
            var realException = problemDetailContext.Exception;
        };
});

var app = builder.Build();

// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling
// Does nothing if a response body has already been provided. when our next `DeveloperExceptionMiddleware` is written response for exception (in dev mode) when we back to `ExceptionHandlerMiddlewareImpl` because `context.Response.HasStarted` it doesn't do anything
// By default `ExceptionHandlerMiddlewareImpl` middleware register original exceptions with `IExceptionHandlerFeature` feature, we don't have this in `DeveloperExceptionPageMiddleware` and we should handle it with a middleware like `CaptureExceptionMiddleware`
// Just for handling exceptions in production mode
// https://github.com/dotnet/aspnetcore/pull/26567
app.UseExceptionHandler(opt => { });

// Handles non-exceptional status codes (e.g., 404 from Results.NotFound(), 401 from unauthorized access) and returns standardized ProblemDetails responses
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // // https://github.com/dotnet/aspnetcore/issues/4765
    // // https://github.com/dotnet/aspnetcore/pull/47760
    // with help of capture exception middleware for capturing actual exception and filling `IExceptionHandlerFeature` interface but in .net 8 preview 5 it fixed and we don't need capture exception middleware
    // .net 8 will add `IExceptionHandlerFeature` in `DisplayExceptionContent` and `SetExceptionHandlerFeatures` methods `DeveloperExceptionPageMiddlewareImpl` class, exact functionality of CaptureException
    app.UseDeveloperExceptionPage();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGetWeatherForecastEndpoint();

app.Run();