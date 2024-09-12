namespace DotNet7WithoutProblemDetails.Core.Exceptions;

/// <summary>
/// Exception type for domain exceptions.
/// </summary>
public class DomainException : CustomException
{
    public DomainException(
        string message,
        int statusCode = StatusCodes.Status400BadRequest,
        Exception? innerException = null
    )
        : base(message, statusCode, innerException) { }
}
