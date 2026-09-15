namespace ECommerce.Services.Catalogs.IntegrationTests;

internal static class IntegrationTestConfigurations
{
    public const string CatalogsDatabase = "ConnectionStrings__catalogsdb";
    public const string CatalogsMongoDatabase = "ConnectionStrings__catalogs-mongo";
    public const string RabbitMq = "ConnectionStrings__rabbitmq";
    public const string Kafka = "ConnectionStrings__kafka";
    public const string TransportType = "WolverineBusOptions__TransportType";
}
