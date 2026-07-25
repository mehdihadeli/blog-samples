using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Integration.Wolverine.Configuration;

public static class MessagingTransportConfigurationExtensions
{
    public static MessagingTransportType GetMessagingTransport(this IConfiguration configuration)
    {
        var value = configuration["Messaging:Transport"]?.Trim().ToLowerInvariant() ?? "rabbitmq";
        return value switch
        {
            "rabbitmq" => MessagingTransportType.RabbitMq,
            "kafka" => MessagingTransportType.Kafka,
            _ => throw new InvalidOperationException($"Unsupported messaging transport '{value}'."),
        };
    }
}
