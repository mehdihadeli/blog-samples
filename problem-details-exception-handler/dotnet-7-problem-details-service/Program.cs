using DotNet7ProblemDetailsService.Core.ProblemDetail;
using DotNet7ProblemDetailsService.Core.ProblemDetail.Middlewares.CaptureExceptionMiddleware;
using DotNet7ProblemDetailsService.Weather;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IProblemDetailMapper, DefaultProblemDetailMapper>();

builder.Services.AddCustomProblemDetails(problemDetailsOptions =>
{
    // customization problem details should go here
    problemDetailsOptions.CustomizeProblemDetails = problemDetailContext =>
    {
        // with help of capture exception middleware for capturing actual exception
        // https://github.com/dotnet/aspnetcore/issues/4765
        // https://github.com/dotnet/aspnetcore/pull/47760
        // .net 8 will add `IExceptionHandlerFeature`in `DisplayExceptionContent` and `SetExceptionHandlerFeatures` methods `DeveloperExceptionPageMiddlewareImpl` class, exact functionality of CaptureException
        // bet before .net 8 preview 5 we should add `IExceptionHandlerFeature` manually with our `UseCaptureException`
        if (
            problemDetailContext.HttpContext.Features.Get<IExceptionHandlerFeature>() is
            { } exceptionFeature
        ) { }
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("test"))
{
    // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/handle-errrors
    // handle exceptional cases with fully detail problem details
    app.UseDeveloperExceptionPage();

    // https://github.com/dotnet/aspnetcore/issues/4765
    // https://github.com/dotnet/aspnetcore/pull/47760
    // with help of capture exception middleware for capturing actual exception and filling `IExceptionHandlerFeature` interface but in .net 8 preview 5 it fixed and we don't need capture exception middleware
    // .net 8 will add `IExceptionHandlerFeature`in `DisplayExceptionContent` and `SetExceptionHandlerFeatures` methods `DeveloperExceptionPageMiddlewareImpl` class, exact functionality of CaptureException
    // bet before .net 8 preview 5 we should add `IExceptionHandlerFeature` manually with our `UseCaptureException`
    app.UseCaptureException();
}
else
{
    // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling
    // Does nothing if a response body has already been provided. when our next `DeveloperExceptionMiddleware` is written response for exception (in dev mode) when we back to `ExceptionHandlerMiddlewareImpl` because `context.Response.HasStarted` it doesn't do anything
    // By default `ExceptionHandlerMiddlewareImpl` middleware register original exceptions with `IExceptionHandlerFeature` feature, we don't have this in `DeveloperExceptionPageMiddleware` and we should handle it with a middleware like `CaptureExceptionMiddleware`
    // Just for handling exceptions in production mode
    // https://github.com/dotnet/aspnetcore/pull/26567
    // handle exceptional cases with summerize problem details
    app.UseExceptionHandler(
        options: new ExceptionHandlerOptions { AllowStatusCode404Response = true }
    );
}

// runs for none exceptional cases - 400-600
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapGetWeatherForecastEndpoint();
app.MapGetWeatherForecast2Endpoint();
app.MapGetWeatherForecast3Endpoint();

app.Run();
