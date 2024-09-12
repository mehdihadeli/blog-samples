namespace DotNet7CustomizeProblemDetails.Core.ProblemDetail;

public interface IProblemDetailMapper
{
    (int StatusCode, string? Title) GetMappedStatusCodes(Exception exception);
}
