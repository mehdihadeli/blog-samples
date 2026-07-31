using System.Reflection;
using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine.Kafka;

namespace BuildingBlocks.Integration.Wolverine.Kafka.Extensions;

public static class Extensions
{
    public static IHostApplicationBuilder AddWolverineKafka(
        this IHostApplicationBuilder builder,
        Action<WolverineBusOptions>? wolverineBusOptions = null,
        Action<WolverineKafkaRegistrationBuilder>? configure = null,
        IReadOnlyCollection<Assembly>? assemblies = null
    )
    {
        builder.AddWolverineMessaging(
            wolverineBusOptions,
            configure: (options, busOptions) =>
            {
                KafkaTransportExpression transport;

                if (!string.IsNullOrWhiteSpace(busOptions.ConnectionString))
                {
                    transport = options.UseKafka(busOptions.ConnectionString);
                }
                else
                {
                    transport = options.UseKafkaUsingNamedConnection(busOptions.ConnectionName);
                }

                transport.AutoProvision();
                if (!string.IsNullOrWhiteSpace(busOptions?.DeadLetterQueueName))
                {
                    transport.DeadLetterQueueTopicName(busOptions.DeadLetterQueueName);
                }

                var registrationBuilder = new WolverineKafkaRegistrationBuilder(
                    options,
                    transport,
                    busOptions
                );

                // Auto-scan — Kafka has no conventional routing, so every
                // consumer topic must be explicitly declared. Need both:
                //   - ApplyMessagesPublishTopology: declare publish topics
                //   - ApplyMessagesConsumeTopology: register Listen<T>() for
                //     each IIntegrationEvent that handlers consume.
                // Contrast with RabbitMQ where UseConventionalRouting handles
                // consumer topology automatically.
                if (busOptions is not null && busOptions.AutoConfigMessagesTopology)
                {
                    assemblies ??= [];
                    if (assemblies.Count != 0)
                    {
                        registrationBuilder.ApplyMessagesPublishTopology(assemblies);
                        registrationBuilder.ApplyMessagesConsumeTopology(assemblies);
                    }
                }
                else
                {
                    configure?.Invoke(registrationBuilder);
                }
            },
            assemblies: assemblies
        );

        return builder;
    }
}
