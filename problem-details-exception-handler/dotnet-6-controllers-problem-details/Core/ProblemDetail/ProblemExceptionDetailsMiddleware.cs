namespace DotNet6ProblemDetails.Core.ProblemDetail;

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

public class ProblemExceptionDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemExceptionDetailsMiddleware> _logger;
    private readonly ProblemDetailsOptions _options;
    private readonly ProblemDetailsFactory _problemDetailsFactory;
    private readonly IProblemDetailsWriter _problemDetailsWriter;
    private readonly DiagnosticListener _diagnosticListener;

    private const string DiagnosticListenerKey =
        "Microsoft.AspNetCore.Diagnostics.HandledException";

    public ProblemExceptionDetailsMiddleware(
        RequestDelegate next,
        ILogger<ProblemExceptionDetailsMiddleware> logger,
        IOptions<ProblemDetailsOptions> options,
        ProblemDetailsFactory problemDetailsFactory,
        IProblemDetailsWriter problemDetailsWriter,
        DiagnosticListener diagnosticListener
    )
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
        _problemDetailsFactory = problemDetailsFactory;
        _problemDetailsWriter = problemDetailsWriter;
        _diagnosticListener = diagnosticListener;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        ExceptionDispatchInfo? exceptionDispatchInfo = null;

        try
        {
            await _next(httpContext);
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

        if (_options.ShouldLogUnhandledException(context, exception, problemDetails))
        {
            _logger.LogError("An unhandled exception has occurred while executing the request.");
        }

        if (_diagnosticListener.IsEnabled() && _diagnosticListener.IsEnabled(DiagnosticListenerKey))
        {
            _diagnosticListener.Write(
                DiagnosticListenerKey,
                new { httpContext = context, exception }
            );
        }

        // write problem details to the response
        await _problemDetailsWriter.WriteAsync(
            new ProblemDetailsContext { HttpContext = context, ProblemDetails = problemDetails }
        );

        exceptionDispatchInfo.Throw();
    }
}
