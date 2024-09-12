using System.Reflection;
using Scrutor;

namespace DotNet7ProblemDetailsService.Core.ProblemDetail;

// https://www.strathweb.com/2022/08/problem-details-responses-everywhere-with-asp-net-core-and-net-7/
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddCustomProblemDetails(
        this IServiceCollection services,
        Action<ProblemDetailsOptions>? configure = null,
        params Assembly[] scanAssemblies
    )
    {
        var assemblies = scanAssemblies.Any()
            ? scanAssemblies
            : new[] { Assembly.GetCallingAssembly() };

        services.AddProblemDetails(configure);
        services.AddSingleton<IProblemDetailsService, ProblemDetailsService>();
        services.AddSingleton<IProblemDetailMapper, DefaultProblemDetailMapper>();
        // services.AddSingleton<IProblemDetailsWriter, ProblemDetailsWriter>();

        RegisterAllMappers(services, assemblies);

        return services;
    }

    private static void RegisterAllMappers(IServiceCollection services, Assembly[] scanAssemblies)
    {
        services.Scan(scan =>
            scan.FromAssemblies(scanAssemblies)
                .AddClasses(classes => classes.AssignableTo(typeof(IProblemDetailMapper)), false)
                .UsingRegistrationStrategy(RegistrationStrategy.Append)
                .As<IProblemDetailMapper>()
                .WithLifetime(ServiceLifetime.Singleton)
        );
    }
}
