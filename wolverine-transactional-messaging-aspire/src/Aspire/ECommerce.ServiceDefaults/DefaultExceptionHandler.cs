using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Central exception handler that uses IProblemDetailMapper chain to map exceptions
/// to HTTP status codes and produces RFC 9457 ProblemDetails responses.
/// Registered first in the pipeline so it handles everything.
/// </summary>
internal sealed class DefaultExceptionHandler(
    ILogger<DefaultExceptionHandler> logger,
    IWebHostEnvironment webHostEnvironment,
    IEnumerable<IProblemDetailMapper> problemDetailMappers,
    IProblemDetailsService problemDetailsService
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        logger.LogError(exception, "An unexpected error occurred");

        var problemDetail = CreateProblemDetailFromException(httpContext, exception);

        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetail,
        };

        await problemDetailsService.WriteAsync(context);
        return true;
    }

    private ProblemDetails CreateProblemDetailFromException(
        HttpContext context,
        Exception? exception
    )
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var mapper = problemDetailMappers.FirstOrDefault() ?? new DefaultProblemDetailMapper();
        int statusCode = mapper.GetMappedStatusCodes(exception);

        context.Response.StatusCode = statusCode;

        return PopulateNewProblemDetail(statusCode, context, exception, traceId);
    }

    private ProblemDetails PopulateNewProblemDetail(
        int code,
        HttpContext httpContext,
        Exception? exception,
        string traceId
    )
    {
        var extensions = new Dictionary<string, object?> { ["traceId"] = traceId };

        // In development, include stack trace for debugging
        if (webHostEnvironment.IsDevelopment() && exception is { })
        {
            extensions["stackTrace"] = exception.StackTrace;
        }

        return TypedResults
            .Problem(
                statusCode: code,
                detail: exception?.Message,
                title: exception?.GetType().Name,
                instance: $"{httpContext.Request.Method} {httpContext.Request.Path}",
                extensions: extensions
            )
            .ProblemDetails;
    }
}
