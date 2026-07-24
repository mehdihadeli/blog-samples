namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Maps an exception to an HTTP status code for ProblemDetails responses.
/// Implementations are collected by DefaultExceptionHandler via DI.
/// </summary>
public interface IProblemDetailMapper
{
    int GetMappedStatusCodes(Exception? exception);
}
