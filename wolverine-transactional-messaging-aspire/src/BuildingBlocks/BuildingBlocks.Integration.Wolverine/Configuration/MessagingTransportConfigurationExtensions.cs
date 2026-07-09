using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Integration.Wolverine.Configuration;

public static class MessagingTransportConfigurationExtensions
{
    public static MessagingTransportType GetMessagingTransport(this IConfiguration configuration)
    {
        var transport = configuration[$"{MessagingTransportOptions.SectionName}:Transport"];

        if (string.IsNullOrWhiteSpace(transport))
        {
            return MessagingTransportType.RabbitMq;
        }

        return transport.Trim().ToLowerInvariant() switch
        {
            "rabbitmq" => MessagingTransportType.RabbitMq,
            "kafka" => MessagingTransportType.Kafka,
            _ => throw new InvalidOperationException(
                $"Unsupported messaging transport '{transport}'. Use 'rabbitmq' or 'kafka'."
            ),
        };
    }
}
