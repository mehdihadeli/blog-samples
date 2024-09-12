using System.Text.Json;

namespace DotNet6ProblemDetails.Core.ProblemDetail;

internal sealed class ProblemDetailWriter : IProblemDetailsWriter
{
    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var httpContext = context.HttpContext;
        ProblemDetailsDefaults.Apply(context.ProblemDetails, httpContext.Response.StatusCode);

        if (context.ProblemDetails.Extensions is { Count: 0 })
        {
            // We can use the source generation in this case
            return new ValueTask(
                httpContext.Response.WriteAsJsonAsync(
                    context.ProblemDetails,
                    new JsonSerializerOptions(),
                    contentType: "application/problem+json"
                )
            );
        }

        return new ValueTask(
            httpContext.Response.WriteAsJsonAsync(
                context.ProblemDetails,
                options: null,
                contentType: "application/problem+json"
            )
        );
    }
}
