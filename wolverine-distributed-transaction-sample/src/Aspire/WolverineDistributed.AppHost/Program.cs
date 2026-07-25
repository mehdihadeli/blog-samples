const string PostgresImage = "postgres";
const string PostgresTag = "17";
const string RabbitMqImage = "rabbitmq";
const string RabbitMqTag = "3.13-management";

var builder = DistributedApplication.CreateBuilder(args);
var transport =
    builder.Configuration["Messaging:Transport"]?.Trim().ToLowerInvariant() ?? "rabbitmq";

var postgres = builder.AddPostgres("postgres").WithImage(PostgresImage).WithImageTag(PostgresTag);
var ordersDb = postgres.AddDatabase("ordersdb");

var orderSagaApi = builder
    .AddProject<Projects.OrderSaga_Api>("ordersaga-api")
    .WithReference(ordersDb)
    .WithEnvironment("Messaging__Transport", transport);

var paymentApi = builder
    .AddProject<Projects.Payment_Api>("payment-api")
    .WithEnvironment("Messaging__Transport", transport);

switch (transport)
{
    case "rabbitmq":
        var rabbitMq = builder
            .AddRabbitMQ("rabbitmq")
            .WithImage(RabbitMqImage)
            .WithImageTag(RabbitMqTag);
        orderSagaApi.WithReference(rabbitMq);
        paymentApi.WithReference(rabbitMq);
        break;
    default:
        throw new InvalidOperationException(
            $"Unsupported messaging transport '{transport}'. Use 'rabbitmq'.");
}

builder.Build().Run();
