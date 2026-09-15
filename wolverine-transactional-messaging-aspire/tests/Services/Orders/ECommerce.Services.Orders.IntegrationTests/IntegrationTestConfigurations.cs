namespace ECommerce.Services.Orders.IntegrationTests;

internal static class IntegrationTestConfigurations
{
    public const string OrdersDatabase = "ConnectionStrings__ordersdb";
    public const string RabbitMq = "ConnectionStrings__rabbitmq";
    public const string Kafka = "ConnectionStrings__kafka";
    public const string TransportType = "WolverineBusOptions__TransportType";
}
