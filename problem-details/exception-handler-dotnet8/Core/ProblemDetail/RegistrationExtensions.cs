using System.Reflection;

namespace ExceptionHandlerDotnet8.Core.ProblemDetail;

// https://www.strathweb.com/2022/08/problem-details-responses-everywhere-with-asp-net-core-and-net-7/
public static class RegistrationExtensions
{
    public static IServiceCollection AddCustomProblemDetails(
        this IServiceCollection services,
        Action<ProblemDetailsOptions>? configure = null,
        params Assembly[] scanAssemblies
    )
    {
        var assemblies = scanAssemblies.Any() ? scanAssemblies : [Assembly.GetCallingAssembly()];
        RegisterAllMappers(services, assemblies);

        services.AddExceptionHandler<DefaultExceptionHandler>();

        services.AddProblemDetails(configure);

        return services;
    }

    private static void RegisterAllMappers(IServiceCollection services, Assembly[] scanAssemblies)
    {
        services.Scan(scan =>
            scan.FromAssemblies(scanAssemblies)
                .AddClasses(classes => classes.AssignableTo<IProblemDetailMapper>())
                .AsImplementedInterfaces()
                .WithSingletonLifetime()
        );
    }
}
