using VybeDesk.Core.Models;
using VybeDesk.Services.Settings;
using Xunit;

namespace VybeDesk.Tests;

public sealed class ActiveProjectContextTests
{
    [Fact]
    public void SetCurrent_SameProjectId_DoesNotFireChanged()
    {
        var ctx = new ActiveProjectContext();
        var id = Guid.NewGuid();
        var count = 0;
        ctx.Changed += () => count++;

        ctx.SetCurrent(new Project { Id = id, Name = "A" });
        ctx.SetCurrent(new Project { Id = id, Name = "A refreshed" });

        Assert.Equal(1, count);
        Assert.Equal(id, ctx.Current?.Id);
    }

    [Fact]
    public void SetCurrent_Null_DoesNotClearExistingProject()
    {
        var ctx = new ActiveProjectContext();
        var id = Guid.NewGuid();
        ctx.SetCurrent(new Project { Id = id, Name = "A" });

        ctx.SetCurrent(null);

        Assert.Equal(id, ctx.Current?.Id);
    }

    [Fact]
    public void SetCurrent_Null_WhenAlreadyNull_DoesNotFireChanged()
    {
        var ctx = new ActiveProjectContext();
        var count = 0;
        ctx.Changed += () => count++;

        ctx.SetCurrent(null);

        Assert.Equal(0, count);
        Assert.Null(ctx.Current);
    }

    [Fact]
    public void SetCurrent_DifferentProjectId_FiresChanged()
    {
        var ctx = new ActiveProjectContext();
        var count = 0;
        ctx.Changed += () => count++;

        ctx.SetCurrent(new Project { Id = Guid.NewGuid(), Name = "A" });
        ctx.SetCurrent(new Project { Id = Guid.NewGuid(), Name = "B" });

        Assert.Equal(2, count);
    }

    [Fact]
    public void ClearCurrent_ExplicitlyClearsAndFiresChanged()
    {
        var ctx = new ActiveProjectContext();
        var id = Guid.NewGuid();
        var count = 0;
        ctx.SetCurrent(new Project { Id = id, Name = "A" });
        ctx.Changed += () => count++;

        ctx.ClearCurrent();

        Assert.Null(ctx.Current);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ClearCurrent_WhenAlreadyNull_DoesNotFireChanged()
    {
        var ctx = new ActiveProjectContext();
        var count = 0;
        ctx.Changed += () => count++;

        ctx.ClearCurrent();

        Assert.Equal(0, count);
        Assert.Null(ctx.Current);
    }

    [Fact]
    public void SetCurrent_SameId_UpdatesReference()
    {
        var ctx = new ActiveProjectContext();
        var id = Guid.NewGuid();
        ctx.SetCurrent(new Project { Id = id, Name = "A" });
        ctx.SetCurrent(new Project { Id = id, Name = "A updated" });

        Assert.Equal("A updated", ctx.Current?.Name);
    }
}
