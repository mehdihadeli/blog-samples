using System.Runtime.CompilerServices;
using A2A;
using A2A.Server;
using A2A.Server.Services;
using Microsoft.Extensions.AI;

namespace SupportAgent;

/// <summary>
/// A2A agent runtime: turns an incoming A2A message into a stream of task
/// events produced by an LLM call that also goes through AgentGateway.
/// </summary>
public sealed class SupportAgentRuntime(IChatClient chatClient) : IA2AAgentRuntime
{
    public Task<A2A.Models.Response> ProcessAsync(
        A2A.Models.Message message,
        IA2AAgentInvocationContext context,
        CancellationToken cancellationToken = default
    )
    {
        var task = new A2A.Models.Task
        {
            Id = Guid.NewGuid().ToString("N"),
            ContextId = message.ContextId ?? Guid.NewGuid().ToString("N"),
            History = [message],
        };

        return Task.FromResult<A2A.Models.Response>(task);
    }

    public async IAsyncEnumerable<A2A.Models.TaskEvent> ExecuteAsync(
        A2A.Models.Task task,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var message =
            task.History?.LastOrDefault()
            ?? throw new NullReferenceException(
                $"The history of the task with id '{task.Id}' is null or empty."
            );

        var messageText = string.Join(
            '\n',
            message.Parts.OfType<A2A.Models.TextPart>().Select(p => p.Text)
        );
        var artifactId = Guid.NewGuid().ToString("N");
        var isFirstChunk = true;

        yield return new A2A.Models.TaskStatusUpdateEvent
        {
            ContextId = task.ContextId,
            TaskId = task.Id,
            Status = new()
            {
                State = TaskState.Working,
                Message = new()
                {
                    ContextId = task.ContextId,
                    TaskId = task.Id,
                    Role = Role.Agent,
                    Parts =
                    [
                        new A2A.Models.TextPart
                        {
                            Text = "Processing started by the Support Escalation Agent.",
                        },
                    ],
                },
            },
        };

        await foreach (
            var content in chatClient.GetStreamingResponseAsync(
                messageText,
                cancellationToken: cancellationToken
            )
        )
        {
            yield return new A2A.Models.TaskArtifactUpdateEvent
            {
                ContextId = task.ContextId,
                TaskId = task.Id,
                Artifact = new()
                {
                    ArtifactId = artifactId,
                    Parts = [new A2A.Models.TextPart { Text = content.Text }],
                },
                Append = !isFirstChunk,
            };

            isFirstChunk = false;
        }

        yield return new A2A.Models.TaskStatusUpdateEvent
        {
            ContextId = task.ContextId,
            TaskId = task.Id,
            Status = new()
            {
                State = TaskState.Completed,
                Message = new()
                {
                    ContextId = task.ContextId,
                    TaskId = task.Id,
                    Role = Role.Agent,
                    Parts = [new A2A.Models.TextPart { Text = "Processing completed." }],
                },
            },
            Final = true,
        };
    }
}
