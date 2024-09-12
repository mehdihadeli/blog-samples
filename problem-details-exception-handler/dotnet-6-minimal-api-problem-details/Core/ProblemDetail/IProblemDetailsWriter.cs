namespace DotNet6ProblemDetails.Core.ProblemDetail;

public interface IProblemDetailsWriter
{
    ValueTask WriteAsync(ProblemDetailsContext context);
}
