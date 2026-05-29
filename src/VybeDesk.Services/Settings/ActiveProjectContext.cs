using VybeDesk.Core.Models;
using VybeDesk.Core.Services;

namespace VybeDesk.Services.Settings;

/// <summary>
/// In-memory implementation of <see cref="IActiveProjectContext"/>.
///
/// <see cref="SetCurrent"/> is null-safe and idempotent:
/// - null is silently ignored (the passive null pulse from ComboBox
///   TwoWay binding during view detachment must not globally clear
///   the focused project — see docs/PROJECT_PERSISTENCE_BUG.md).
/// - Same project ID (by <see cref="Project.Id"/>) updates the
///   reference but does not fire <see cref="Changed"/>, preventing
///   infinite feedback loops between modules.
///
/// <see cref="ClearCurrent"/> is the intentional "no project" path
/// — used when a project is deleted or the user deliberately clears
/// the selection.
/// </summary>
public sealed class ActiveProjectContext : IActiveProjectContext
{
    public Project? Current { get; private set; }
    public event Action? Changed;

    public void SetCurrent(Project? project)
    {
        if (project is null) return;

        if (Current?.Id == project.Id)
        {
            Current = project;
            return;
        }

        Current = project;
        Changed?.Invoke();
    }

    public void ClearCurrent()
    {
        if (Current is null) return;
        Current = null;
        Changed?.Invoke();
    }
}
