using DotNet6ProblemDetails.Core.ProblemDetail;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DotNet6ProblemDetails.Core;

[ApiExplorerSettings(IgnoreApi = true)]
public class ErrorController : ControllerBase
{
    private readonly ExceptionResponseService _exceptionResponseService;

    public ErrorController(ExceptionResponseService exceptionResponseService)
    {
        _exceptionResponseService = exceptionResponseService;
    }

    [Route("/error")]
    public IActionResult HandleErrorDevelopment([FromServices] IHostEnvironment hostEnvironment)
    {
        var exceptionHandlerFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();

        if (exceptionHandlerFeature != null)
        {
            var exception = exceptionHandlerFeature.Error;

            // Use the service to generate the error response
            var errorResponse = _exceptionResponseService.GenerateErrorResponse(exception);

            return StatusCode(errorResponse.StatusCode, errorResponse);
        }

        // Return a 500 error if no exception is found
        return StatusCode(
            StatusCodes.Status500InternalServerError,
            new
            {
                StatusCode = StatusCodes.Status500InternalServerError,
                Message = "An unexpected error occurred.",
            }
        );
    }
}
