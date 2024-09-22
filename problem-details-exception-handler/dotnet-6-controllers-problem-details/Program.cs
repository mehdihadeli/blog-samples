using DotNet6ProblemDetails.Core.ProblemDetail;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

builder.Services.AddCustomProblemDetailsSupport(options =>
{
    options.IncludeExceptionDetails = (ctx, ex) => builder.Environment.IsDevelopment();

    options.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("test"))
{
    app.UseDeveloperExceptionPage();
    // handle exceptional cases with fully detail problem details
    // app.UseCustomProblemDetailsSupport();
}
else
{
    // The RequestDelegate(HttpContext) or ExceptionHandler property that will handle the exception. If this is not explicitly provided, the subsequent middleware pipeline will be used by default (subsequent middleware is UseStatusCodePages and because /error endpoint not found response status code will change to 404 and next middleware which is `StatusCodePage` will show 404 status code so it is better we define a ExceptionHandler with propery or a /error endpoint for generating error repose in production)
    // or we can handle exception in the /error exception endpoint, if it can't find `/error` endpoint, changes the response status code to `404`
    // https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-errors?viewFallbackFrom=aspnetcore-3.0#exception-handler
    // https://github.com/dotnet/aspnetcore/blob/release/6.0/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddleware.cs#L50
    // if we don't have `ExceptionHandler` or `ExceptionHandlingPath` we get `ExceptionHandlerOptions_NotConfiguredCorrectly` exception
    // app.UseExceptionHandler(
    //     options: new ExceptionHandlerOptions
    //     {
    //         AllowStatusCode404Response = true,
    //         ExceptionHandlingPath = "/error",
    //     }
    // );

    app.UseExceptionHandler(exceptionHandlerApp =>
    {
        exceptionHandlerApp.Run(async context =>
        {
            context.Response.ContentType = "application/json";

            var exceptionResponseService =
                context.RequestServices.GetRequiredService<ExceptionResponseService>();

            var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
            if (exceptionHandlerFeature != null)
            {
                var exception = exceptionHandlerFeature.Error;

                var exceptionResponse = exceptionResponseService.GenerateErrorResponse(exception);
                context.Response.StatusCode = exceptionResponse.StatusCode;

                await context.Response.WriteAsJsonAsync(exceptionResponse);
            }
        });
    });

    // app.UseExceptionHandler(exceptionHandlerApp =>
    //     exceptionHandlerApp.Run(async context => await Results.Problem().ExecuteAsync(context))
    // );

    // handle exceptional cases with summarize problem details
    // app.UseCustomProblemDetailsSupport();
}

// runs for none exceptional cases - 400-600
app.UseStatusCodePages();

app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI();

app.Run();
