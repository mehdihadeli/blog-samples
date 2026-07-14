using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace BuildingBlocks.Integration.Wolverine;

public sealed class RabbitMqTopologyProvisioningHostedService(
    string connectionString,
    ILogger<RabbitMqTopologyProvisioningHostedService> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var listenerTopologies =
            WolverineMessageTopologyExtensions.GetRegisteredListenerTopologies();
        var publishTopologies = WolverineMessageTopologyExtensions.GetRegisteredPublishTopologies();

        if (listenerTopologies.Count == 0 && publishTopologies.Count == 0)
        {
            return;
        }

        var connectionFactory = new ConnectionFactory { Uri = new Uri(connectionString) };

        await using var connection = await connectionFactory.CreateConnectionAsync(
            cancellationToken: cancellationToken
        );
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken
        );

        foreach (var topology in publishTopologies)
        {
            logger.LogInformation(
                "Declaring RabbitMQ publish exchange {ExchangeName}",
                topology.ExchangeName
            );

            await channel.ExchangeDeclareAsync(
                topology.ExchangeName,
                ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken
            );
        }

        foreach (var topology in listenerTopologies)
        {
            logger.LogInformation(
                "Declaring RabbitMQ topology for queue {QueueName} and exchange {ExchangeName}",
                topology.QueueName,
                topology.ExchangeName
            );

            await channel.ExchangeDeclareAsync(
                topology.ExchangeName,
                ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken
            );

            try
            {
                await channel.QueueDeclareAsync(
                    topology.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: cancellationToken
                );

                await channel.QueueBindAsync(
                    topology.QueueName,
                    topology.ExchangeName,
                    topology.RoutingKey,
                    arguments: null,
                    cancellationToken: cancellationToken
                );
            }
            catch (OperationInterruptedException exception)
                when (exception.Message.Contains("inequivalent arg", StringComparison.Ordinal))
            {
                logger.LogWarning(
                    exception,
                    "Skipping explicit topology declaration for queue {QueueName} because RabbitMQ already has a compatible Wolverine-managed declaration.",
                    topology.QueueName
                );
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
