namespace VybeDesk.Core.Services;

/// <summary>
/// Cross-module notification: a bug just transitioned to Fixed in the Bug
/// Tracker. The Testing Manager listens for this to offer regression-test
/// prompt generation. Deliberately a tiny shared surface so the Bug Tracker
/// and the Testing Manager share nothing of each other's internals — only
/// that this event exists.
/// </summary>
public interface IBugFixedNotifier
{
    event Action<BugFixedEvent>? BugFixed;
    void Notify(BugFixedEvent evt);
}

/// <summary>Payload of a Bug-Fixed event.</summary>
/// <param name="ProjectId">Project the bug belongs to.</param>
/// <param name="BugId">Identity of the bug that was just fixed.</param>
/// <param name="Title">Title of the fixed bug, for display in nudges.</param>
public sealed record BugFixedEvent(Guid ProjectId, Guid BugId, string Title);
