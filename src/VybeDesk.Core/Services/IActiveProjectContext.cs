using VybeDesk.Core.Models;

namespace VybeDesk.Core.Services;

/// <summary>
/// Tracks which project the user is currently focused on across the app,
/// so cross-cutting services (like <see cref="IAiService"/>) can pick up
/// per-project overrides without every caller having to pass them
/// explicitly. Project-scoped view models call <see cref="SetCurrent"/>
/// from their <c>OnSelectedProjectChanged</c> / <c>OnActiveProjectChanged</c>
/// partials.
///
/// <see cref="SetCurrent"/> ignores null — the passive null pulses
/// from ComboBox TwoWay bindings during view detachment must not
/// globally clear the focused project. Use <see cref="ClearCurrent"/>
/// for intentional "no project" transitions (e.g. deleting the last
/// project). <see cref="SetCurrent"/> is also idempotent by project
/// ID: re-setting the same project updates the reference without
/// firing <see cref="Changed"/>.
/// </summary>
public interface IActiveProjectContext
{
    Project? Current { get; }
    void SetCurrent(Project? project);
    void ClearCurrent();
    event Action? Changed;
}
