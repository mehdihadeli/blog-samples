using System.Reflection;
using ExceptionHandlerDotnet8.Core.ProblemDetail;
using ExceptionHandlerDotnet8.Weather;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCustomProblemDetails(scanAssemblies: Assembly.GetExecutingAssembly());
var app = builder.Build();

app.UseExceptionHandler(opt => { });

// Handles non-exceptional status codes (e.g., 404 from Results.NotFound(), 401 from unauthorized access) and returns standardized ProblemDetails responses
app.UseStatusCodePages();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGetWeatherForecastEndpoint();

app.Run();