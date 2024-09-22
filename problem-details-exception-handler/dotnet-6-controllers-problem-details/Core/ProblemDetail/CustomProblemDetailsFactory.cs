namespace DotNet6ProblemDetails.Core.ProblemDetail;

using System.Diagnostics;
using Humanizer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

public class CustomProblemDetailsFactory : ProblemDetailsFactory
{
    private readonly ProblemDetailsOptions _options;
    private readonly ApiBehaviorOptions _apiBehaviorOptions;

    public CustomProblemDetailsFactory(
        IOptions<ApiBehaviorOptions> apiBehaviourOptions,
        IOptions<ProblemDetailsOptions> options
    )
    {
        _options = options.Value;
        _apiBehaviorOptions =
            apiBehaviourOptions?.Value
            ?? throw new ArgumentNullException(nameof(apiBehaviourOptions));
    }

    public override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null
    )
    {
        IExceptionHandlerFeature? exceptionFeature =
            httpContext.Features.Get<IExceptionHandlerFeature>();

        var problem = exceptionFeature is { }
            ? _options.GetProblemDetails(httpContext, exceptionFeature.Error)
                ?? _options.MapStatusCode(httpContext)
            : _options.MapStatusCode(httpContext);

        var exception = exceptionFeature?.Error;

        if (_options.IncludeExceptionDetails(httpContext, exception))
        {
            problem = new ProblemDetails
            {
                Title =
                    title
                    ?? (
                        exception is not null
                            ? exception.GetType().Name.Humanize(LetterCasing.Title)
                            : problem.Title
                    ),
                Detail = detail ?? exception?.Message,
                Status =
                    statusCode
                    ?? (problem.Status > 0 ? problem.Status : httpContext.Response.StatusCode),
                Extensions =
                {
                    ["exception"] = new
                    {
                        Details = exception?.ToString(),
                        Headers = httpContext.Request.Headers,
                        Path = httpContext.Request.Path.ToString(),
                        Endpoint = httpContext.GetEndpoint()?.ToString(),
                        RouteValues = httpContext.Features.Get<IRouteValuesFeature>()?.RouteValues,
                    },
                },
                Type = type ?? problem.Type,
                Instance = instance ?? $"{httpContext.Request.Method} {httpContext.Request.Path}",
            };
        }
        else
        {
            problem = new ProblemDetails
            {
                Title =
                    title
                    ?? (
                        exception is not null
                            ? exception.GetType().Name.Humanize(LetterCasing.Title)
                            : problem.Title
                    ),
                Status =
                    statusCode
                    ?? (problem.Status > 0 ? problem.Status : httpContext.Response.StatusCode),
                Type = type ?? problem.Type,
            };
        }

        httpContext.Response.StatusCode = (int)problem.Status;

        ApplyProblemDetailsDefaults(httpContext, problem, (int)problem.Status);

        return problem;
    }

    public override ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelStateDictionary,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null
    )
    {
        if (modelStateDictionary == null)
        {
            throw new ArgumentNullException(nameof(modelStateDictionary));
        }

        statusCode ??= 400;

        var problemDetails = new ValidationProblemDetails(modelStateDictionary)
        {
            Status = statusCode,
            Type = type,
            Detail = detail,
            Instance = instance,
        };

        if (title != null)
        {
            // For validation problem details, don't overwrite the default title with null.
            problemDetails.Title = title;
        }

        ApplyProblemDetailsDefaults(httpContext, problemDetails, statusCode.Value);

        return problemDetails;
    }

    private void ApplyProblemDetailsDefaults(
        HttpContext httpContext,
        ProblemDetails problemDetails,
        int statusCode
    )
    {
        if (_apiBehaviorOptions.ClientErrorMapping.TryGetValue(statusCode, out var clientErrorData))
        {
            problemDetails.Title ??= clientErrorData.Title;
            problemDetails.Type ??= clientErrorData.Link;
        }

        var traceId = Activity.Current?.Id ?? httpContext?.TraceIdentifier;
        if (traceId != null)
        {
            problemDetails.Extensions["traceId"] = traceId;
        }

        // https://github.com/dotnet/aspnetcore/issues/54325
        // https://github.com/dotnet/aspnetcore/pull/54478/files#diff-306493a5bb9543cbfd64bb9352e5fc0e8ca3d78ace648c5b5726c74ce8248da3R58
        // https://devblogs.microsoft.com/dotnet/improvements-in-net-core-3-0-for-troubleshooting-and-monitoring-distributed-apps/
        // traceId will add by default in .net 9 by `DefaultProblemDetailsWriter.cs`
        problemDetails.Extensions.TryAdd("spanId", Activity.Current?.SpanId.ToString());
        problemDetails.Extensions.TryAdd("traceId", Activity.Current?.TraceId.ToString());
        problemDetails.Extensions.TryAdd("id", Activity.Current?.Id);
    }
}
