using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Tracks which project the user is currently focused on across the app,
/// so cross-cutting services (like <see cref="IAiService"/>) can pick up
/// per-project overrides without every caller having to pass them
/// explicitly. Project-scoped view models call <see cref="SetCurrent"/>
/// from their <c>OnSelectedProjectChanged</c> / <c>OnActiveProjectChanged</c>
/// partials.
///
/// Null Current means "no project focused right now" — services should
/// fall back to global settings.
/// </summary>
public interface IActiveProjectContext
{
    Project? Current { get; }
    void SetCurrent(Project? project);
    event Action? Changed;
}
