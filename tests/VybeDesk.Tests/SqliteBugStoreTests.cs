using VybeDesk.Core.Models;
using VybeDesk.Services.Storage;
using Xunit;

namespace VybeDesk.Tests;

/// <summary>
/// SQLite-backed bug store tests. Each test runs against a fresh temp database
/// to keep state isolated. Covers the three behaviors the spec calls out:
/// project-scoped retrieval (project A's bugs do not appear under project B),
/// update round-trip, and remove.
/// </summary>
public sealed class SqliteBugStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _db;
    private readonly SqliteBugStore _store;

    public SqliteBugStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            "vybedesk-tests-bugs-" + Guid.NewGuid().ToString("N") + ".db");
        _db = new Database(_dbPath);
        _store = new SqliteBugStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task GetByProjectAsync_AfterAdd_ReturnsTheBug()
    {
        var projectId = Guid.NewGuid();
        var bug = new Bug
        {
            ProjectId = projectId,
            Title = "Drag-and-drop crashes",
            Severity = BugSeverity.Critical,
            Status = BugStatus.Open,
            StepsToReproduce = "1. Open Session Builder\n2. Drag a file",
            ExpectedResult = "File is staged.",
            ActualResult = "App freezes.",
            Area = "Session Builder",
        };

        await _store.AddAsync(bug);

        var bugs = await _store.GetByProjectAsync(projectId);

        Assert.Single(bugs);
        Assert.Equal(bug.Id, bugs[0].Id);
        Assert.Equal("Drag-and-drop crashes", bugs[0].Title);
        Assert.Equal(BugSeverity.Critical, bugs[0].Severity);
        Assert.Equal(BugStatus.Open, bugs[0].Status);
        Assert.Equal("1. Open Session Builder\n2. Drag a file", bugs[0].StepsToReproduce);
        Assert.Equal("File is staged.", bugs[0].ExpectedResult);
        Assert.Equal("App freezes.", bugs[0].ActualResult);
        Assert.Equal("Session Builder", bugs[0].Area);
    }

    [Fact]
    public async Task GetByProjectAsync_IsScopedToProject()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        await _store.AddAsync(new Bug
        {
            ProjectId = projectA, Title = "A1", Severity = BugSeverity.Major,
        });
        await _store.AddAsync(new Bug
        {
            ProjectId = projectA, Title = "A2", Severity = BugSeverity.Minor,
        });
        await _store.AddAsync(new Bug
        {
            ProjectId = projectB, Title = "B1", Severity = BugSeverity.Critical,
        });

        var aBugs = await _store.GetByProjectAsync(projectA);
        var bBugs = await _store.GetByProjectAsync(projectB);

        Assert.Equal(2, aBugs.Count);
        Assert.Single(bBugs);
        Assert.DoesNotContain(aBugs, b => b.Title == "B1");
        Assert.DoesNotContain(bBugs, b => b.Title.StartsWith("A"));
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var projectId = Guid.NewGuid();
        var bug = new Bug
        {
            ProjectId = projectId,
            Title = "Initial",
            Severity = BugSeverity.Minor,
            Status = BugStatus.Open,
        };
        await _store.AddAsync(bug);

        bug.Title = "Revised";
        bug.Severity = BugSeverity.Critical;
        bug.Status = BugStatus.Fixing;
        bug.Area = "Notebook";
        await _store.UpdateAsync(bug);

        var bugs = await _store.GetByProjectAsync(projectId);

        Assert.Single(bugs);
        Assert.Equal("Revised", bugs[0].Title);
        Assert.Equal(BugSeverity.Critical, bugs[0].Severity);
        Assert.Equal(BugStatus.Fixing, bugs[0].Status);
        Assert.Equal("Notebook", bugs[0].Area);
    }

    [Fact]
    public async Task RemoveAsync_DeletesTheBug()
    {
        var projectId = Guid.NewGuid();
        var bug = new Bug { ProjectId = projectId, Title = "Doomed" };
        await _store.AddAsync(bug);

        await _store.RemoveAsync(bug.Id);

        var bugs = await _store.GetByProjectAsync(projectId);
        Assert.Empty(bugs);
    }

    [Fact]
    public async Task ChangedEvent_FiresOnEveryMutatingCall()
    {
        var projectId = Guid.NewGuid();
        var count = 0;
        _store.Changed += () => count++;

        var bug = new Bug { ProjectId = projectId, Title = "Trace" };
        await _store.AddAsync(bug);
        bug.Title = "Trace edited";
        await _store.UpdateAsync(bug);
        await _store.RemoveAsync(bug.Id);

        Assert.Equal(3, count);
    }
}
