using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Integration.Wolverine.Configuration;

public static class MessagingTransportConfigurationExtensions
{
    private const string SectionName = "Messaging";
    private const string TransportKey = "Transport";

    public static MessagingTransportType GetMessagingTransport(this IConfiguration configuration)
    {
        var transport =
            configuration.GetSection(SectionName)?[TransportKey]?.Trim().ToLowerInvariant()
            ?? "rabbitmq";

        return transport switch
        {
            "rabbitmq" => MessagingTransportType.RabbitMq,
            _ => throw new InvalidOperationException(
                $"Unsupported messaging transport '{transport}'. Use 'rabbitmq'."
            ),
        };
    }
}
