using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Default implementation of IProblemDetailMapper that maps common .NET exceptions
/// to appropriate HTTP status codes.
/// </summary>
internal sealed class DefaultProblemDetailMapper : IProblemDetailMapper
{
    public int GetMappedStatusCodes(Exception? exception)
    {
        return exception switch
        {
            FluentValidation.ValidationException => StatusCodes.Status422UnprocessableEntity,
            BadHttpRequestException => StatusCodes.Status400BadRequest,
            OperationCanceledException => StatusCodes.Status499ClientClosedRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            NotImplementedException => StatusCodes.Status501NotImplemented,
            HttpRequestException httpEx => (int)(
                httpEx.StatusCode ?? System.Net.HttpStatusCode.BadGateway
            ),
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
