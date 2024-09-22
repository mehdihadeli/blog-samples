using DotNet6ProblemDetails.Core.Exceptions;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace DotNet6ProblemDetails.Core.ProblemDetail;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddCustomProblemDetailsSupport(
        this IServiceCollection services,
        Action<ProblemDetailsOptions>? configure = null
    )
    {
        services.AddSingleton<ProblemDetailsFactory, CustomProblemDetailsFactory>();
        services.AddSingleton<IProblemDetailsWriter, ProblemDetailWriter>();
        services.AddSingleton<ExceptionResponseService>();

        // Register ProblemDetailsOptions with defaults
        services.Configure<ProblemDetailsOptions>(options =>
        {
            options.Map((ctx, exception) => MapDefaultExceptions(exception));

            // Apply custom configuration if provided
            configure?.Invoke(options);
        });

        return services;
    }

    private static ProblemDetails? MapDefaultExceptions(Exception exception)
    {
        switch (exception)
        {
            case ConflictException conflictException:
            {
                return new ProblemDetails
                {
                    Title = GetTitle(conflictException),
                    Status = conflictException.StatusCode,
                };
            }
            case ValidationException validationException:
            {
                return new ProblemDetails
                {
                    Title = GetTitle(validationException),
                    Status = validationException.StatusCode,
                };
            }
            case BadRequestException badRequestException:
            {
                return new ProblemDetails
                {
                    Title = GetTitle(badRequestException),
                    Status = badRequestException.StatusCode,
                };
            }
            case NotFoundException notFoundException:
            {
                return new ProblemDetails
                {
                    Title = GetTitle(notFoundException),
                    Status = notFoundException.StatusCode,
                };
            }
            case ArgumentException argumentException:
            {
                return new ProblemDetails
                {
                    Title = GetTitle(argumentException),
                    Status = StatusCodes.Status400BadRequest,
                };
            }
            case HttpResponseException httpResponseException:
            {
                return new ProblemDetails
                {
                    Title = GetTitle(httpResponseException),
                    Status = httpResponseException.StatusCode,
                };
            }
            case HttpRequestException httpRequestException:
            {
                return new ProblemDetails
                {
                    Title = GetTitle(httpRequestException),
                    Status = (int)httpRequestException.StatusCode,
                };
            }
            default:
                return null;
        }
    }

    private static string GetTitle(object exception)
    {
        return exception.GetType().Name.Humanize(LetterCasing.Title);
    }

    public static IApplicationBuilder UseCustomProblemDetailsSupport(this IApplicationBuilder app)
    {
        app.UseMiddleware<ProblemExceptionDetailsMiddleware>();

        return app;
    }
}
