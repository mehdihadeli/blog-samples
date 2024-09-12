namespace DotNet7WithoutProblemDetails.Core.Exceptions;

public class BadRequestException : CustomException
{
    public BadRequestException(string message, Exception? innerException = null)
        : base(message, StatusCodes.Status400BadRequest, innerException) { }
}
