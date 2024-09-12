using System.Diagnostics;
using Humanizer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DotNet7ProblemDetailsService.Core.ProblemDetail;

// https://www.strathweb.com/2022/08/problem-details-responses-everywhere-with-asp-net-core-and-net-7/
public class ProblemDetailsService : IProblemDetailsService
{
    private readonly IProblemDetailMapper _problemDetailMapper;
    private readonly IProblemDetailsWriter[] _writers;

    public ProblemDetailsService(
        IEnumerable<IProblemDetailsWriter> writers,
        IProblemDetailMapper problemDetailMapper
    )
    {
        _writers = writers.ToArray();
        _problemDetailMapper = problemDetailMapper;
    }

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));
        ArgumentNullException.ThrowIfNull(context.ProblemDetails);
        ArgumentNullException.ThrowIfNull(context.HttpContext);

        // with help of `capture exception middleware` for capturing actual thrown exception, in .net 8 preview 5 it will create automatically
        IExceptionHandlerFeature? exceptionFeature =
            context.HttpContext.Features.Get<IExceptionHandlerFeature>();

        // if we throw an exception, we should create appropriate ProblemDetail based on the exception, else we just return default ProblemDetail with status 500 or a custom ProblemDetail which is returned from the endpoint
        if (exceptionFeature is not null)
        {
            CreateProblemDetailFromException(context, exceptionFeature);
        }

        if (
            context.HttpContext.Response.HasStarted
            || context.HttpContext.Response.StatusCode < 400
            || _writers.Length == 0
        )
            return ValueTask.CompletedTask;

        IProblemDetailsWriter problemDetailsWriter = null!;
        if (_writers.Length == 1)
        {
            IProblemDetailsWriter writer = _writers[0];
            return !writer.CanWrite(context) ? ValueTask.CompletedTask : writer.WriteAsync(context);
        }

        foreach (var writer in _writers)
        {
            if (writer.CanWrite(context))
            {
                problemDetailsWriter = writer;
                break;
            }
        }

        return problemDetailsWriter?.WriteAsync(context) ?? ValueTask.CompletedTask;
    }

    private void CreateProblemDetailFromException(
        ProblemDetailsContext context,
        IExceptionHandlerFeature exceptionFeature
    )
    {
        var (mappedStatusCode, title) = _problemDetailMapper.GetMappedStatusCodes(
            exceptionFeature.Error
        );

        if (mappedStatusCode <= 0 && !string.IsNullOrEmpty(title))
            return;

        PopulateNewProblemDetail(
            context.ProblemDetails,
            context.HttpContext,
            mappedStatusCode,
            exceptionFeature.Error
        );
        context.HttpContext.Response.StatusCode = mappedStatusCode;
    }

    private static void PopulateNewProblemDetail(
        ProblemDetails existingProblemDetails,
        HttpContext httpContext,
        int statusCode,
        Exception exception
    )
    {
        existingProblemDetails.Title = exception.GetType().Name.Humanize(LetterCasing.Title);
        existingProblemDetails.Detail = exception.Message;
        existingProblemDetails.Status = statusCode;
        existingProblemDetails.Instance =
            $"{httpContext.Request.Method} {httpContext.Request.Path}";

        // https://github.com/dotnet/aspnetcore/issues/54325
        // https://github.com/dotnet/aspnetcore/pull/54478/files#diff-306493a5bb9543cbfd64bb9352e5fc0e8ca3d78ace648c5b5726c74ce8248da3R58
        // https://devblogs.microsoft.com/dotnet/improvements-in-net-core-3-0-for-troubleshooting-and-monitoring-distributed-apps/
        // traceId will add by default in .net 9 by `DefaultProblemDetailsWriter.cs`
        existingProblemDetails.Extensions.TryAdd("spanId", Activity.Current?.SpanId.ToString());
        existingProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.TraceId.ToString());
        existingProblemDetails.Extensions.TryAdd("id", Activity.Current?.Id);
    }
}
