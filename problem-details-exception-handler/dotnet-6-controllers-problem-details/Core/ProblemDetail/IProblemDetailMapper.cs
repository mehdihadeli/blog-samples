namespace DotNet6ProblemDetails.Core.ProblemDetail;

public interface IProblemDetailMapper
{
    (int StatusCode, string? Title) GetMappedStatusCodes(Exception exception);
}
