namespace ExceptionHandlerDotnet8.Core.ProblemDetail;

public interface IProblemDetailMapper
{
    int GetMappedStatusCodes(Exception? exception);
}
