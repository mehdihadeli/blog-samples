using DotNet6ProblemDetails.Core.ProblemDetail;
using DotNet6ProblemDetails.Weather;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

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

app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI();


app.Run();
