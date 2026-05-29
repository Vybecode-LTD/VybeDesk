using VybeDesk.Core.Models;
using VybeDesk.Services.Settings;
using Xunit;

namespace VybeDesk.Tests.AppSmoke;

/// <summary>
/// Locks in the "passive null write does not clear active project" rule.
///
/// Root cause documented in docs/PROJECT_PERSISTENCE_BUG.md: TwoWay
/// ComboBox bindings fire null back into SelectedProject during view
/// detachment / collection rebuilds. ActiveProjectContext.SetCurrent
/// must silently swallow that null so the project focus survives
/// navigation.
/// </summary>
public sealed class ProjectSelectionPersistenceTests
{
    [Fact]
    public void SetCurrent_ThenPassiveNull_DoesNotClearProject()
    {
        // Arrange — simulate a project-scoped module setting the active
        // project, then a passive null write from a ComboBox TwoWay binding.
        var ctx = new ActiveProjectContext();
        var projectA = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Alpha",
            FolderPath = @"C:\projects\alpha"
        };

        // Act — set a real project, then send the passive null that
        // ComboBox fires on detachment / Clear().
        ctx.SetCurrent(projectA);
        ctx.SetCurrent(null);           // ← the passive null write

        // Assert — projectA MUST still be Current.
        Assert.NotNull(ctx.Current);
        Assert.Equal(projectA.Id, ctx.Current!.Id);
    }

    [Fact]
    public void SetCurrent_Null_WhenEmpty_RemainsNull()
    {
        // An initial null write (before any project is set) must not
        // throw or change state.
        var ctx = new ActiveProjectContext();

        ctx.SetCurrent(null);

        Assert.Null(ctx.Current);
    }

    [Fact]
    public void SetCurrent_DifferentProject_FiresChangedAndUpdates()
    {
        // Switching between two real projects must fire Changed once
        // per new project.
        var ctx = new ActiveProjectContext();
        var changed = 0;
        ctx.Changed += () => changed++;

        var projectA = new Project { Id = Guid.NewGuid(), Name = "A" };
        var projectB = new Project { Id = Guid.NewGuid(), Name = "B" };

        ctx.SetCurrent(projectA);
        ctx.SetCurrent(projectB);

        Assert.Equal(2, changed);
        Assert.Equal(projectB.Id, ctx.Current?.Id);
    }

    [Fact]
    public void ClearCurrent_IsTheOnlyWayToResetToNull()
    {
        // Only ClearCurrent (not SetCurrent(null)) can reset to null.
        var ctx = new ActiveProjectContext();
        var project = new Project { Id = Guid.NewGuid(), Name = "X" };

        ctx.SetCurrent(project);
        Assert.NotNull(ctx.Current);

        ctx.ClearCurrent();
        Assert.Null(ctx.Current);
    }

    [Fact]
    public void PassiveNullAfterMultipleProjectSwitches_PreservesLastProject()
    {
        // Simulates rapid navigation: A → B → null → should still be B.
        var ctx = new ActiveProjectContext();
        var projectA = new Project { Id = Guid.NewGuid(), Name = "A" };
        var projectB = new Project { Id = Guid.NewGuid(), Name = "B" };

        ctx.SetCurrent(projectA);
        ctx.SetCurrent(projectB);
        ctx.SetCurrent(null);           // passive null
        ctx.SetCurrent(null);           // second passive null

        Assert.Equal(projectB.Id, ctx.Current?.Id);
    }

    [Fact]
    public void PassiveNull_DoesNotFireChangedEvent()
    {
        // A passive null must produce zero Changed events.
        var ctx = new ActiveProjectContext();
        var project = new Project { Id = Guid.NewGuid(), Name = "P" };
        ctx.SetCurrent(project);

        var changedAfterSet = 0;
        ctx.Changed += () => changedAfterSet++;

        ctx.SetCurrent(null);

        Assert.Equal(0, changedAfterSet);
    }
}
