namespace DotNet7ProblemDetailsService.Core.Exceptions;

public class AppException : CustomException
{
    public AppException(
        string message,
        int statusCode = StatusCodes.Status400BadRequest,
        Exception? innerException = null
    )
        : base(message, statusCode, innerException) { }
}
