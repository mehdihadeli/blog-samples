using Microsoft.AspNetCore.Mvc;

namespace DotNet6ProblemDetails.Core.ProblemDetail;

// ref: https://github.com/dotnet/aspnetcore/blob/3b4e92a6975816bf107e34b02a6d4d0cd2da5589/src/Shared/ProblemDetails/ProblemDetailsDefaults.cs#L9

internal static class ProblemDetailsDefaults
{
    public static readonly Dictionary<int, (string Type, string Title)> Defaults =
        new()
        {
            [400] = ("https://tools.ietf.org/html/rfc7231#section-6.5.1", "Bad Request"),

            [401] = ("https://tools.ietf.org/html/rfc7235#section-3.1", "Unauthorized"),

            [403] = ("https://tools.ietf.org/html/rfc7231#section-6.5.3", "Forbidden"),

            [404] = ("https://tools.ietf.org/html/rfc7231#section-6.5.4", "Not Found"),

            [405] = ("https://tools.ietf.org/html/rfc7231#section-6.5.5", "Method Not Allowed"),

            [406] = ("https://tools.ietf.org/html/rfc7231#section-6.5.6", "Not Acceptable"),

            [409] = ("https://tools.ietf.org/html/rfc7231#section-6.5.8", "Conflict"),

            [415] = (
                "https://tools.ietf.org/html/rfc7231#section-6.5.13",
                "Unsupported Media Type"
            ),

            [422] = ("https://tools.ietf.org/html/rfc4918#section-11.2", "Unprocessable Entity"),

            [500] = (
                "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                "An error occurred while processing your request."
            ),
        };

    public static void Apply(ProblemDetails problemDetails, int? statusCode)
    {
        // We allow StatusCode to be specified either on ProblemDetails or on the ObjectResult and use it to configure the other.
        // This lets users write <c>return Conflict(new Problem("some description"))</c>
        // or <c>return Problem("some-problem", 422)</c> and have the response have consistent fields.
        if (problemDetails.Status is null)
        {
            if (statusCode is not null)
            {
                problemDetails.Status = statusCode;
            }
            else
            {
                problemDetails.Status =
                    problemDetails is HttpValidationProblemDetails
                        ? StatusCodes.Status400BadRequest
                        : StatusCodes.Status500InternalServerError;
            }
        }

        if (Defaults.TryGetValue(problemDetails.Status.Value, out var defaults))
        {
            problemDetails.Title ??= defaults.Title;
            problemDetails.Type ??= defaults.Type;
        }
    }
}
