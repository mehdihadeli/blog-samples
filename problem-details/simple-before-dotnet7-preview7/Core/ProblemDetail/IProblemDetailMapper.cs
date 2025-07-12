namespace simple.Core.ProblemDetail;

public interface IProblemDetailMapper
{
    int GetMappedStatusCodes(Exception? exception);
}
