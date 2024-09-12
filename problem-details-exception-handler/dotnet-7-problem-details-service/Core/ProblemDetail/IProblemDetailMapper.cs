namespace DotNet7ProblemDetailsService.Core.ProblemDetail;

public interface IProblemDetailMapper
{
    (int StatusCode, string Title) GetMappedStatusCodes(Exception exception);
}
