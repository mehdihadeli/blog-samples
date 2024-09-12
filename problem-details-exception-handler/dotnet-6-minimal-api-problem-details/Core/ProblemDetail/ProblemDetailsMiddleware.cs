namespace DotNet6ProblemDetails.Core.ProblemDetail;

using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

public class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;
    private readonly ProblemDetailsFactory _problemDetailsFactory;
    private readonly IProblemDetailsWriter _problemDetailsWriter;

    public ProblemDetailsMiddleware(
        RequestDelegate next,
        ILogger<ProblemDetailsMiddleware> logger,
        ProblemDetailsFactory problemDetailsFactory,
        IProblemDetailsWriter problemDetailsWriter
    )
    {
        _next = next;
        _logger = logger;
        _problemDetailsFactory = problemDetailsFactory;
        _problemDetailsWriter = problemDetailsWriter;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        ExceptionDispatchInfo? exceptionDispatchInfo = null;

        try
        {
            await _next(httpContext);

            // runs for none exceptional cases - 400-600
            // await HandleStatusProblem(httpContext);
        }
        catch (Exception ex)
        {
            exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
        }

        if (exceptionDispatchInfo is { })
        {
            await HandleExceptionAsync(httpContext, exceptionDispatchInfo);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        ExceptionDispatchInfo exceptionDispatchInfo
    )
    {
        var exception = exceptionDispatchInfo.SourceException;

        var feature = new ExceptionHandlerFeature
        {
            Path = context.Request.Path,
            Error = exception,
        };

        context.Features.Set<IExceptionHandlerPathFeature>(feature);
        context.Features.Set<IExceptionHandlerFeature>(feature);

        var problemDetails = _problemDetailsFactory.CreateProblemDetails(context);

        // write problem details to the response
        await _problemDetailsWriter.WriteAsync(
            new ProblemDetailsContext { HttpContext = context, ProblemDetails = problemDetails }
        );
    }

    private async Task HandleStatusProblem(HttpContext context)
    {
        if (
            context.Response.HasStarted
            || context.Response.StatusCode < 400
            || context.Response.StatusCode >= 600
            || context.Response.ContentLength.HasValue
            || !string.IsNullOrEmpty(context.Response.ContentType)
        )
        {
            return;
        }

        var statusCode = context.Response.StatusCode;

        var problemDetails = _problemDetailsFactory.CreateProblemDetails(
            context,
            statusCode: statusCode
        );

        // write problem details to the response
        await _problemDetailsWriter.WriteAsync(
            new ProblemDetailsContext { HttpContext = context, ProblemDetails = problemDetails }
        );
    }
}
