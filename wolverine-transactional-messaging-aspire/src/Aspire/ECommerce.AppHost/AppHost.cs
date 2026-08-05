const string PostgresImage = "postgres";
const string PostgresTag = "17";
const string MongoImage = "mongo";
const string MongoTag = "8.0";
const string RabbitMqImage = "rabbitmq";
const string RabbitMqTag = "4-management";
const string KafkaImage = "confluentinc/cp-kafka";
const string KafkaTag = "7.5.12";

var builder = DistributedApplication.CreateBuilder(args);
var transport =
    builder.Configuration["WolverineBusOptions:TransportType"]?.Trim().ToLowerInvariant()
    ?? "rabbitmq";

var postgres = builder.AddPostgres("postgres").WithImage(PostgresImage).WithImageTag(PostgresTag);
var catalogsDb = postgres.AddDatabase("catalogsdb");
var ordersDb = postgres.AddDatabase("ordersdb");

var mongo = builder.AddMongoDB("mongo").WithImage(MongoImage).WithImageTag(MongoTag);
var catalogsMongo = mongo.AddDatabase("catalogs-mongo");
var ordersMongo = mongo.AddDatabase("orders-mongo");

var catalogsApi = builder
    .AddProject<Projects.ECommerce_Services_Catalogs_Api>("catalogs-api")
    .WithReference(catalogsDb)
    .WithReference(catalogsMongo)
    .WithEnvironment("WolverineBusOptions__TransportType", transport);

var ordersApi = builder
    .AddProject<Projects.ECommerce_Services_Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WithReference(ordersMongo)
    .WithEnvironment("WolverineBusOptions__TransportType", transport);

switch (transport)
{
    case "rabbitmq":
        var rabbitMq = builder
            .AddRabbitMQ("rabbitmq")
            .WithImage(RabbitMqImage)
            .WithImageTag(RabbitMqTag);
        catalogsApi.WithReference(rabbitMq);
        ordersApi.WithReference(rabbitMq);
        break;

    case "kafka":
        var kafka = builder.AddKafka("kafka").WithImage(KafkaImage).WithImageTag(KafkaTag);
        catalogsApi.WithReference(kafka);
        ordersApi.WithReference(kafka);
        break;

    default:
        throw new InvalidOperationException(
            $"Unsupported messaging transport '{transport}'. Use 'rabbitmq' or 'kafka'."
        );
}

builder.Build().Run();
