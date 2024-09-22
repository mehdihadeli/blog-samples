using Microsoft.AspNetCore.Mvc;

namespace DotNet6ProblemDetails.Core.ProblemDetail;

// ref:https://github.com/khellang/Middleware/blob/master/src/ProblemDetails/ProblemDetailsOptions.cs
public class ProblemDetailsOptions
{
    private List<(
        Type ExceptionType,
        Func<HttpContext, Exception, ProblemDetails?> MapFunc
    )> Mappers { get; } = new();

    public Func<HttpContext, Exception?, bool> IncludeExceptionDetails { get; set; } =
        IncludeExceptionDetailsDefault;

    public Func<
        HttpContext,
        Exception,
        ProblemDetails,
        bool
    > ShouldLogUnhandledException { get; set; } = (ctx, e, d) => IsServerError(d.Status);
    public Func<HttpContext, ProblemDetails> MapStatusCode { get; set; } = DefaultMapStatusCode;

    public void MapToStatusCode<TException>(int statusCode)
        where TException : Exception
    {
        Map<TException>((_, _) => new ProblemDetails { Status = statusCode });
    }

    public void Map<TException>(Func<TException, ProblemDetails?> mapping)
        where TException : Exception
    {
        Mappers.Add((typeof(TException), (ctx, ex) => mapping((TException)ex)));
    }

    public void Map<TException>(Func<HttpContext, TException, ProblemDetails?> mapping)
        where TException : Exception
    {
        Mappers.Add((typeof(TException), (ctx, ex) => mapping(ctx, (TException)ex)));
    }

    public void Map(Func<HttpContext, Exception, ProblemDetails?> mapping)
    {
        Mappers.Add((typeof(Exception), mapping));
    }

    internal ProblemDetails? GetProblemDetails(HttpContext context, Exception? exception)
    {
        if (exception is null)
        {
            return null;
        }

        foreach (var (exceptionType, mapFunc) in Mappers)
        {
            if (exceptionType.IsInstanceOfType(exception))
            {
                try
                {
                    return mapFunc(context, exception);
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    private static bool IsServerError(int? statusCode)
    {
        return statusCode >= 500;
    }

    private static bool IncludeExceptionDetailsDefault(HttpContext context, Exception? exception)
    {
        return context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
    }

    private static ProblemDetails DefaultMapStatusCode(HttpContext context)
    {
        // `Title` and `Type` will apply by Problem details default during response write
        return new ProblemDetails { Status = context.Response.StatusCode };
    }
}
