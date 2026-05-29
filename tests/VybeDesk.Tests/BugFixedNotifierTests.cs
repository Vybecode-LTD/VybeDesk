using VybeDesk.Core.Services;
using VybeDesk.Services.Testing;
using Xunit;

namespace VybeDesk.Tests;

public class BugFixedNotifierTests
{
    [Fact]
    public void Notify_FiresSubscriber()
    {
        var notifier = new BugFixedNotifier();
        BugFixedEvent? received = null;
        notifier.BugFixed += e => received = e;

        var evt = new BugFixedEvent(Guid.NewGuid(), Guid.NewGuid(), "Test bug");
        notifier.Notify(evt);

        Assert.NotNull(received);
        Assert.Equal(evt.ProjectId, received.ProjectId);
        Assert.Equal(evt.BugId, received.BugId);
        Assert.Equal(evt.Title, received.Title);
    }

    [Fact]
    public void Notify_MultipleSubscribers_AllReceive()
    {
        var notifier = new BugFixedNotifier();
        var count = 0;
        notifier.BugFixed += _ => count++;
        notifier.BugFixed += _ => count++;
        notifier.BugFixed += _ => count++;

        notifier.Notify(new BugFixedEvent(Guid.NewGuid(), Guid.NewGuid(), "x"));

        Assert.Equal(3, count);
    }

    [Fact]
    public void Notify_NoSubscribers_DoesNotThrow()
    {
        var notifier = new BugFixedNotifier();
        // Should not throw even with no subscribers.
        notifier.Notify(new BugFixedEvent(Guid.NewGuid(), Guid.NewGuid(), "orphan"));
    }

    [Fact]
    public void Unsubscribe_StopsReceiving()
    {
        var notifier = new BugFixedNotifier();
        var count = 0;
        void Handler(BugFixedEvent _) => count++;

        notifier.BugFixed += Handler;
        notifier.Notify(new BugFixedEvent(Guid.NewGuid(), Guid.NewGuid(), "a"));
        Assert.Equal(1, count);

        notifier.BugFixed -= Handler;
        notifier.Notify(new BugFixedEvent(Guid.NewGuid(), Guid.NewGuid(), "b"));
        Assert.Equal(1, count); // unchanged
    }
}
