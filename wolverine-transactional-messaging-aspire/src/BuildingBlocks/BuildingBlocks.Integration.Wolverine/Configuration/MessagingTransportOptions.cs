namespace BuildingBlocks.Integration.Wolverine.Configuration;

public sealed class MessagingTransportOptions
{
    public const string SectionName = "Messaging";

    public string Transport { get; set; } = "rabbitmq";
}
