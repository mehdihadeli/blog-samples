namespace DotNet7ProblemDetailsService.Core.ProblemDetail.Middlewares.CaptureExceptionMiddleware;

public static class CaptureExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseCaptureException(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        return app.UseMiddleware<CaptureExceptionMiddlewareImp>();
    }
}
