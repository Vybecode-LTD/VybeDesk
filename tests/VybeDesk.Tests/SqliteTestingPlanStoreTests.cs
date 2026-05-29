using VybeDesk.Core.Models;
using VybeDesk.Services.Storage;
using Xunit;

namespace VybeDesk.Tests;

/// <summary>
/// SQLite-backed testing plan store tests. Each test uses a fresh temp DB.
/// Covers the three behaviours the spec calls out (save+get-by-project,
/// project-scoped, remove) plus the upsert behaviour the UNIQUE
/// constraint on project_id enables.
/// </summary>
public sealed class SqliteTestingPlanStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _db;
    private readonly SqliteTestingPlanStore _store;

    public SqliteTestingPlanStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            "vybedesk-tests-tplan-" + Guid.NewGuid().ToString("N") + ".db");
        _db = new Database(_dbPath);
        _store = new SqliteTestingPlanStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsNullWhenNoPlanSaved()
    {
        var plan = await _store.GetByProjectAsync(Guid.NewGuid());
        Assert.Null(plan);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByProjectAsync_RoundTripsAllFields()
    {
        var projectId = Guid.NewGuid();
        var plan = new TestingPlan
        {
            ProjectId = projectId,
            StrategySummary = "Unit + integration for a .NET API.",
            Frameworks = new[] { "xUnit" },
            Kinds = new[] { TestKind.Unit, TestKind.Integration },
            Answers = new QuestionnaireAnswers
            {
                ProjectKind = "Library",
                Language = "DotNet",
                Criticality = "Important",
                TeamSize = "SmallTeam",
                ExternalSystems = "Some",
            },
        };

        await _store.SaveAsync(plan);

        var loaded = await _store.GetByProjectAsync(projectId);
        Assert.NotNull(loaded);
        Assert.Equal(plan.Id, loaded!.Id);
        Assert.Equal(projectId, loaded.ProjectId);
        Assert.Equal("Unit + integration for a .NET API.", loaded.StrategySummary);
        Assert.Equal(new[] { "xUnit" }, loaded.Frameworks);
        Assert.Equal(new[] { TestKind.Unit, TestKind.Integration }, loaded.Kinds);
        Assert.Equal("Library", loaded.Answers.ProjectKind);
        Assert.Equal("DotNet", loaded.Answers.Language);
        Assert.Equal("Important", loaded.Answers.Criticality);
        Assert.Equal("SmallTeam", loaded.Answers.TeamSize);
        Assert.Equal("Some", loaded.Answers.ExternalSystems);
    }

    [Fact]
    public async Task GetByProjectAsync_IsScopedToProject()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        await _store.SaveAsync(new TestingPlan
        {
            ProjectId = projectA,
            StrategySummary = "A's plan",
            Frameworks = new[] { "xUnit" },
        });

        var bPlan = await _store.GetByProjectAsync(projectB);
        Assert.Null(bPlan);

        var aPlan = await _store.GetByProjectAsync(projectA);
        Assert.NotNull(aPlan);
        Assert.Equal("A's plan", aPlan!.StrategySummary);
    }

    [Fact]
    public async Task SaveAsync_TwiceForSameProject_UpdatesInPlace()
    {
        var projectId = Guid.NewGuid();
        var plan = new TestingPlan
        {
            ProjectId = projectId,
            StrategySummary = "Initial",
            Frameworks = new[] { "xUnit" },
        };
        await _store.SaveAsync(plan);

        // Save the same plan with a different summary — UNIQUE(project_id)
        // means this should upsert, not insert a duplicate row.
        plan.StrategySummary = "Revised";
        plan.Frameworks = new[] { "xUnit", "Playwright" };
        await _store.SaveAsync(plan);

        var loaded = await _store.GetByProjectAsync(projectId);
        Assert.NotNull(loaded);
        Assert.Equal("Revised", loaded!.StrategySummary);
        Assert.Equal(new[] { "xUnit", "Playwright" }, loaded.Frameworks);
        Assert.Equal(plan.Id, loaded.Id); // Same row, same id.
    }

    [Fact]
    public async Task RemoveAsync_DeletesPlanForProject()
    {
        var projectId = Guid.NewGuid();
        await _store.SaveAsync(new TestingPlan
        {
            ProjectId = projectId,
            StrategySummary = "Doomed",
        });

        await _store.RemoveAsync(projectId);

        Assert.Null(await _store.GetByProjectAsync(projectId));
    }

    [Fact]
    public async Task ChangedEvent_FiresOnEveryMutatingCall()
    {
        var projectId = Guid.NewGuid();
        var count = 0;
        _store.Changed += () => count++;

        await _store.SaveAsync(new TestingPlan { ProjectId = projectId });
        await _store.SaveAsync(new TestingPlan { ProjectId = projectId,
                                                 StrategySummary = "Update" });
        await _store.RemoveAsync(projectId);

        Assert.Equal(3, count);
    }
}
