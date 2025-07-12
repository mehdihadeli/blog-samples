using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace simple.Core.ProblemDetail;

// https://www.strathweb.com/2022/08/problem-details-responses-everywhere-with-asp-net-core-and-net-7/
public static class RegistrationExtensions
{
    public static IServiceCollection AddCustomProblemDetails(
        this IServiceCollection services,
        Action<ProblemDetailsOptions>? configure = null,
        bool useCustomProblemService = false,
        params Assembly[] scanAssemblies
    )
    {
        var assemblies = scanAssemblies.Any() ? scanAssemblies : [Assembly.GetCallingAssembly()];
        RegisterAllMappers(services, assemblies);


        if (useCustomProblemService)
        {
            // Must be registered BEFORE AddProblemDetails because AddProblemDetails internally uses TryAddSingleton for adding default implementation for IProblemDetailsWriter
            services.AddSingleton<IProblemDetailsService, ProblemDetailsService>();
            services.AddSingleton<IProblemDetailsWriter, ProblemDetailsWriter>();

            services.AddProblemDetails(configure);
        }
        else
        {
            services.AddProblemDetails(c =>
            {
                c.CustomizeProblemDetails = context =>
                {
                    // with the help of `capture exception middleware` for capturing actual thrown exception, in .net 8 preview 5 it will create automatically
                    IExceptionHandlerFeature? exceptionFeature =
                        context.HttpContext.Features.Get<IExceptionHandlerFeature>();

                    Exception? exception = exceptionFeature?.Error ?? context.Exception;

                    var mappers =
                        context.HttpContext.RequestServices.GetServices<IProblemDetailMapper>();

                    var webHostEnvironment =
                        context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();

                    // if we throw an exception, we should create appropriate ProblemDetail based on the exception, else we just return default ProblemDetail with status 500 or a custom ProblemDetail which is returned from the endpoint
                    CreateProblemDetailFromException(
                        context,
                        webHostEnvironment,
                        exception,
                        mappers
                    );
                };

                //configure?.Invoke(c);
            });
        }

        return services;
    }

    private static void CreateProblemDetailFromException(
        ProblemDetailsContext context,
        IWebHostEnvironment webHostEnvironment,
        Exception? exception,
        IEnumerable<IProblemDetailMapper>? problemDetailMappers
    )
    {
        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        int statusCode =
            problemDetailMappers?.Select(m => m.GetMappedStatusCodes(exception)).FirstOrDefault()
            ?? new DefaultProblemDetailMapper().GetMappedStatusCodes(exception);

        context.HttpContext.Response.StatusCode = statusCode;

        context.ProblemDetails = PopulateNewProblemDetail(
            statusCode,
            context.HttpContext,
            webHostEnvironment,
            exception,
            traceId
        );
    }

    private static ProblemDetails PopulateNewProblemDetail(
        int code,
        HttpContext httpContext,
        IWebHostEnvironment webHostEnvironment,
        Exception? exception,
        string traceId
    )
    {
        var extensions = new Dictionary<string, object?> { { "traceId", traceId } };

        // Add stackTrace in development mode for debugging purposes
        if (webHostEnvironment.IsDevelopment() && exception is { })
        {
            extensions["stackTrace"] = exception.StackTrace;
        }

        // type will fill automatically by .net core
        var problem = TypedResults
            .Problem(
                statusCode: code,
                detail: exception?.Message,
                title: exception?.GetType().Name,
                instance: $"{httpContext.Request.Method} {httpContext.Request.Path}",
                extensions: extensions
            )
            .ProblemDetails;

        return problem;
    }

    private static void RegisterAllMappers(IServiceCollection services, Assembly[] scanAssemblies)
    {
        services.Scan(scan =>
            scan.FromAssemblies(scanAssemblies)
                .AddClasses(classes => classes.AssignableTo<IProblemDetailMapper>())
                .AsImplementedInterfaces()
                .WithSingletonLifetime()
        );
    }
}
