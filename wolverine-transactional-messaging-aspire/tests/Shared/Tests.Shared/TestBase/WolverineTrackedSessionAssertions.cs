using Wolverine;
using Wolverine.Tracking;
using Xunit;

namespace Tests.Shared.TestBase;

public static class WolverineTrackedSessionAssertions
{
    public static void ShouldPublish<T>(this ITrackedSession trackedSession)
        where T : class
    {
        Assert.NotEmpty(trackedSession.FindEnvelopesWithMessageType<T>(MessageEventType.Sent));
        Assert.Empty(
            trackedSession.FindEnvelopesWithMessageType<Fault<T>>(
                MessageEventType.AutoFaultPublished
            )
        );
    }

    public static void ShouldConsume<T>(this ITrackedSession trackedSession)
        where T : class
    {
        Assert.NotEmpty(
            trackedSession.FindEnvelopesWithMessageType<T>(MessageEventType.MessageSucceeded)
        );
        Assert.Empty(
            trackedSession.FindEnvelopesWithMessageType<Fault<T>>(
                MessageEventType.AutoFaultPublished
            )
        );
    }

    public static void ShouldConsume<TMessage, TConsumedBy>(this ITrackedSession trackedSession)
        where TMessage : class
        where TConsumedBy : class
    {
        trackedSession.ShouldConsume<TMessage>();
    }
}
