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
    private readonly IProblemDetailMapper _problemDetailMapper;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ApiBehaviorOptions _options;

    public CustomProblemDetailsFactory(
        IOptions<ApiBehaviorOptions> options,
        IProblemDetailMapper problemDetailMapper,
        IWebHostEnvironment webHostEnvironment
    )
    {
        _problemDetailMapper = problemDetailMapper;
        _webHostEnvironment = webHostEnvironment;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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

        if (exceptionFeature is null)
        {
            var problem = new ProblemDetails
            {
                Status = statusCode ?? 500,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance,
            };

            ApplyProblemDetailsDefaults(httpContext, problem, (int)problem.Status);

            return problem;
        }

        var exception = exceptionFeature.Error;

        var problemDetails = InitializeProblemDetails(
            httpContext,
            statusCode,
            title,
            type,
            detail,
            instance,
            exception
        );

        httpContext.Response.StatusCode = (int)problemDetails.Status;

        ApplyProblemDetailsDefaults(httpContext, problemDetails, (int)problemDetails.Status);

        return problemDetails;
    }

    private ProblemDetails InitializeProblemDetails(
        HttpContext httpContext,
        int? statusCode,
        string? title,
        string? type,
        string? detail,
        string? instance,
        Exception exception
    )
    {
        var (mappedStatus, mappedTitle) = _problemDetailMapper.GetMappedStatusCodes(exception);

        if (_webHostEnvironment.IsProduction())
        {
            return new ProblemDetails
            {
                Status =
                    statusCode
                    ?? (mappedStatus > 0 ? mappedStatus : httpContext.Response.StatusCode),
            };
        }

        return new ProblemDetails
        {
            Title = title ?? mappedTitle ?? exception.GetType().Name.Humanize(LetterCasing.Title),
            Detail = detail ?? exception.Message,
            Status =
                statusCode ?? (mappedStatus > 0 ? mappedStatus : httpContext.Response.StatusCode),
            Extensions =
            {
                ["exception"] = new
                {
                    Details = exception.ToString(),
                    Headers = httpContext.Request.Headers,
                    Path = httpContext.Request.Path.ToString(),
                    Endpoint = httpContext.GetEndpoint()?.ToString(),
                    RouteValues = httpContext.Features.Get<IRouteValuesFeature>()?.RouteValues,
                },
            },
            Type = type,
            Instance = instance ?? $"{httpContext.Request.Method} {httpContext.Request.Path}",
        };
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
        if (_options.ClientErrorMapping.TryGetValue(statusCode, out var clientErrorData))
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
