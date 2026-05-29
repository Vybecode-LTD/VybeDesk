using VybeDesk.Core.Services;

namespace VybeDesk.Services.Testing;

/// <summary>
/// Minimal in-memory pub/sub for <see cref="IBugFixedNotifier"/>. Single
/// shared instance registered as a singleton in DI. Subscribers are
/// responsible for marshalling onto the UI thread if they need to —
/// notifications fire synchronously on whatever thread calls
/// <see cref="Notify"/>.
/// </summary>
public sealed class BugFixedNotifier : IBugFixedNotifier
{
    public event Action<BugFixedEvent>? BugFixed;

    public void Notify(BugFixedEvent evt) => BugFixed?.Invoke(evt);
}
