using DotNet7CustomizeProblemDetails.Core.Exceptions;
using Humanizer;

namespace DotNet7CustomizeProblemDetails.Core.ProblemDetail;

internal sealed class DefaultProblemDetailMapper : IProblemDetailMapper
{
    public (int StatusCode, string? Title) GetMappedStatusCodes(Exception exception)
    {
        return exception switch
        {
            ConflictException conflictException => (
                conflictException.StatusCode,
                GetTitle(conflictException)
            ),
            ValidationException validationException => (
                validationException.StatusCode,
                GetTitle(validationException)
            ),
            ArgumentException argumentException => (
                StatusCodes.Status400BadRequest,
                GetTitle(argumentException)
            ),
            BadRequestException badRequestException => (
                badRequestException.StatusCode,
                GetTitle(badRequestException)
            ),
            NotFoundException notFoundException => (
                notFoundException.StatusCode,
                GetTitle(notFoundException)
            ),
            HttpResponseException httpResponseException => (
                httpResponseException.StatusCode,
                GetTitle(httpResponseException)
            ),
            HttpRequestException httpRequestException => (
                (int)httpRequestException.StatusCode,
                GetTitle(httpRequestException)
            ),
            AppException appException => (appException.StatusCode, GetTitle(appException)),
            _ => (0, null),
        };
    }

    private string GetTitle(object exception)
    {
        return exception.GetType().Name.Humanize(LetterCasing.Title);
    }
}
