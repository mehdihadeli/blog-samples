using DotNet6ProblemDetails.Core.Exceptions;
using Humanizer;

namespace DotNet6ProblemDetails.Core.ProblemDetail;

public class ExceptionResponseService
{
    public ExceptionResponse GenerateErrorResponse(Exception exception)
    {
        switch (exception)
        {
            case ConflictException conflictException:
                return new ExceptionResponse
                {
                    StatusCode = conflictException.StatusCode,
                    Message = GetTitle(conflictException),
                };

            case ValidationException validationException:
                return new ExceptionResponse
                {
                    StatusCode = validationException.StatusCode,
                    Message = GetTitle(validationException),
                };

            case BadRequestException badRequestException:
                return new ExceptionResponse
                {
                    StatusCode = badRequestException.StatusCode,
                    Message = GetTitle(badRequestException),
                };

            case NotFoundException notFoundException:
                return new ExceptionResponse
                {
                    StatusCode = notFoundException.StatusCode,
                    Message = GetTitle(notFoundException),
                };

            case ArgumentException argumentException:
                return new ExceptionResponse
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Message = GetTitle(argumentException),
                };

            case HttpResponseException httpResponseException:
                return new ExceptionResponse
                {
                    StatusCode = httpResponseException.StatusCode,
                    Message = GetTitle(httpResponseException),
                };

            case HttpRequestException httpRequestException:
                return new ExceptionResponse
                {
                    StatusCode = (int)httpRequestException.StatusCode,
                    Message = GetTitle(httpRequestException),
                };

            default:
                return new ExceptionResponse
                {
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = "An unexpected error occurred.",
                };
        }
    }

    private static string GetTitle(object exception)
    {
        return exception.GetType().Name.Humanize(LetterCasing.Title);
    }
}

public class ExceptionResponse
{
    public string Message { get; set; } = default!;
    public int StatusCode { get; set; }
}
