using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Settings;

/// <summary>
/// In-memory implementation of <see cref="IActiveProjectContext"/>. No
/// persistence — the "current focus" is a UI-session concept that resets at
/// startup. Each project-scoped VM's <c>OnXChanged</c> partial calls
/// <see cref="SetCurrent"/> during initial project load, so by the time the
/// AI service reads <see cref="Current"/> on the first user-driven action,
/// it's already up to date.
/// </summary>
public sealed class ActiveProjectContext : IActiveProjectContext
{
    public Project? Current { get; private set; }
    public event Action? Changed;

    public void SetCurrent(Project? project)
    {
        Current = project;
        Changed?.Invoke();
    }
}
