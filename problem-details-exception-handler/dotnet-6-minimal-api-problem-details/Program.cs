using DotNet6ProblemDetails.Core.ProblemDetail;
using DotNet6ProblemDetails.Weather;
using Microsoft.AspNetCore.Mvc.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ProblemDetailsFactory, CustomProblemDetailsFactory>();
builder.Services.AddSingleton<IProblemDetailMapper, DefaultProblemDetailMapper>();
builder.Services.AddSingleton<IProblemDetailsWriter, ProblemDetailWriter>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("test"))
{
    // handle exceptional cases with fully detail problem details
    app.UseMiddleware<ProblemDetailsMiddleware>();
}
else
{
    // handle exceptional cases with summerize problem details
    app.UseExceptionHandler(
        options: new ExceptionHandlerOptions
        {
            AllowStatusCode404Response = true,
            ExceptionHandlingPath = "/Error",
        }
    );
}

// runs for none exceptional cases - 400-600
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;

    var statusCode = context.Response.StatusCode;

    var problemDetailsFactory = context.RequestServices.GetRequiredService<ProblemDetailsFactory>();

    var problemDetails = problemDetailsFactory.CreateProblemDetails(
        context,
        statusCode: statusCode
    );

    var problemDetailsWriter = context.RequestServices.GetRequiredService<IProblemDetailsWriter>();

    // write problem details to the response
    await problemDetailsWriter.WriteAsync(
        new ProblemDetailsContext { HttpContext = context, ProblemDetails = problemDetails }
    );
});

app.UseSwagger();
app.UseSwaggerUI();

app.MapGetWeatherForecastEndpoint();
app.MapGetWeatherForecast2Endpoint();
app.MapGetWeatherForecast3Endpoint();

app.Run();
