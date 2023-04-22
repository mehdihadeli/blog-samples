using AutoBogus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PublisherConfirm.Contracts;

namespace PublisherConfirm;

public class ConsoleRunnerWorker : IHostedService
{
	private readonly IPublisher _publisher;
	private readonly ILogger<ConsoleRunnerWorker> _logger;
	private readonly IHostApplicationLifetime _appLifetime;

	public ConsoleRunnerWorker(
		IPublisher publisher,
		ILogger<ConsoleRunnerWorker> logger,
		IHostApplicationLifetime appLifetime)
	{
		_publisher = publisher;
		_logger = logger;
		_appLifetime = appLifetime;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Application Started");

		while (true)
		{
			Console.WriteLine("Enter the name of message to publish. (Q for exit)");
			var inputNumber = Console.ReadLine();

			if (inputNumber?.ToLower() == "q") break;

			if (!int.TryParse(inputNumber, out int number))
			{
				continue;
			}

			var messages = GenerateMessages(number);
			await _publisher.PublishAsync(messages);
		}

		_appLifetime.StopApplication();
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Application Stopped");
		return Task.CompletedTask;
	}

	private List<EnvelopMessage> GenerateMessages(int numMessages)
	{
		return new AutoFaker<EnvelopMessage>().RuleFor(
				x => x.Message,
				f => new PersonMessage
					 {
						 FirstName = f.Person.FirstName,
						 LastName = f.Person.LastName
					 })
			.RuleFor(
				x => x.Metadata,
				f => new Metadata(
					new Dictionary<string, object?>
					{
						{"message-id", Guid.NewGuid().ToString()}
					}))
			.Generate(numMessages);
	}
}