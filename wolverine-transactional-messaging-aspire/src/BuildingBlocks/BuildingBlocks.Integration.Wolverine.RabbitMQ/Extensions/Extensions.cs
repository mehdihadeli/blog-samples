using System.Reflection;
using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine.RabbitMQ;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ.Extensions;

public static class Extensions
{
    public static IHostApplicationBuilder AddWolverineRabbitMq(
        this IHostApplicationBuilder builder,
        Action<WolverineBusOptions>? wolverineBusOptions = null,
        Action<WolverineRabbitMqRegistrationBuilder>? configure = null,
        IReadOnlyCollection<Assembly>? assemblies = null
    )
    {
        builder.AddWolverineMessaging(
            wolverineBusOptions,
            configure: (options, busOptions) =>
            {
                var transport = options.UseRabbitMqUsingNamedConnection(busOptions.ConnectionName);

                transport.AutoProvision();
                if (!string.IsNullOrWhiteSpace(busOptions?.DeadLetterQueueName))
                {
                    transport.CustomizeDeadLetterQueueing(
                        new DeadLetterQueue(busOptions.DeadLetterQueueName)
                    );
                }

                var registrationBuilder = new WolverineRabbitMqRegistrationBuilder(
                    options,
                    transport,
                    busOptions
                );

                // Auto-scan — only publish topology needed because:
                // ApplyMessagesPublishTopology internally calls UseSnakeCaseConventions(),
                // which activates Wolverine's built-in conventional routing.
                // Wolverine then auto-discovers handlers at runtime and creates
                // queues + bindings for them — consumer topology is fully covered.
                if (busOptions is not null && busOptions.AutoConfigMessagesTopology)
                {
                    assemblies ??= [];
                    if (assemblies.Count != 0)
                    {
                        registrationBuilder.ApplyMessagesPublishTopology(assemblies);
                    }
                }
                else
                {
                    configure?.Invoke(registrationBuilder);
                }
            }
        );

        return builder;
    }
}
