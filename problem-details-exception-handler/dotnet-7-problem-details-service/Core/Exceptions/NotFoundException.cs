namespace DotNet7ProblemDetailsService.Core.Exceptions;

public class NotFoundException : CustomException
{
    public NotFoundException(string message, Exception? innerException = null)
        : base(message, StatusCodes.Status404NotFound, innerException) { }
}
