namespace DotNet7WithoutProblemDetails.Core.Exceptions;

public class ConflictException : CustomException
{
    public ConflictException(string message, Exception? innerException = null)
        : base(message, StatusCodes.Status409Conflict, innerException) { }
}
