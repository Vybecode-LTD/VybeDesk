using ClaudePM.Core.Models;
using ClaudePM.Services.Storage;
using Xunit;

namespace ClaudePM.Tests;

/// <summary>
/// SQLite-backed vision store tests. Fresh temp DB per test for isolation.
/// Covers the four behaviours the spec calls out: save+get-by-project,
/// project-scoped retrieval, upsert (UNIQUE on project_id), remove, plus
/// the round-trip of the nullable ApprovedAt field.
/// </summary>
public sealed class SqliteVisionStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _db;
    private readonly SqliteVisionStore _store;

    public SqliteVisionStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            "claudepm-tests-vision-" + Guid.NewGuid().ToString("N") + ".db");
        _db = new Database(_dbPath);
        _store = new SqliteVisionStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsNullWhenNoRecordSaved()
        => Assert.Null(await _store.GetByProjectAsync(Guid.NewGuid()));

    [Fact]
    public async Task SaveAsync_ThenGetByProjectAsync_RoundTripsAllFields()
    {
        var projectId = Guid.NewGuid();
        var record = new VisionRecord
        {
            ProjectId = projectId,
            Statements = new[]
            {
                new VisionStatement { Text = "Users can create an account." },
                new VisionStatement { Text = "Data persists between sessions." },
                new VisionStatement { Text = "Works offline." },
            },
            ApprovedAt = DateTimeOffset.Now,
        };

        await _store.SaveAsync(record);

        var loaded = await _store.GetByProjectAsync(projectId);
        Assert.NotNull(loaded);
        Assert.Equal(record.Id, loaded!.Id);
        Assert.Equal(3, loaded.Statements.Count);
        Assert.Equal("Users can create an account.", loaded.Statements[0].Text);
        Assert.True(loaded.IsApproved);
    }

    [Fact]
    public async Task GetByProjectAsync_IsScopedToProject()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        await _store.SaveAsync(new VisionRecord
        {
            ProjectId = projectA,
            Statements = new[] { new VisionStatement { Text = "A's vision." } },
        });

        Assert.Null(await _store.GetByProjectAsync(projectB));
        var aLoaded = await _store.GetByProjectAsync(projectA);
        Assert.NotNull(aLoaded);
        Assert.Equal("A's vision.", aLoaded!.Statements[0].Text);
    }

    [Fact]
    public async Task SaveAsync_TwiceForSameProject_UpsertsInPlace()
    {
        var projectId = Guid.NewGuid();
        var record = new VisionRecord
        {
            ProjectId = projectId,
            Statements = new[] { new VisionStatement { Text = "Initial." } },
        };
        await _store.SaveAsync(record);

        record.Statements = new[]
        {
            new VisionStatement { Text = "Revised one." },
            new VisionStatement { Text = "Revised two." },
        };
        record.ApprovedAt = DateTimeOffset.Now;
        await _store.SaveAsync(record);

        var loaded = await _store.GetByProjectAsync(projectId);
        Assert.NotNull(loaded);
        Assert.Equal(record.Id, loaded!.Id); // same row
        Assert.Equal(2, loaded.Statements.Count);
        Assert.True(loaded.IsApproved);
    }

    [Fact]
    public async Task RemoveAsync_DeletesRecord()
    {
        var projectId = Guid.NewGuid();
        await _store.SaveAsync(new VisionRecord { ProjectId = projectId });

        await _store.RemoveAsync(projectId);

        Assert.Null(await _store.GetByProjectAsync(projectId));
    }

    [Fact]
    public async Task ApprovedAt_NullableRoundTrip()
    {
        // Spec: ApprovedAt is nullable — null until the user approves. The
        // store must preserve null on load, not silently default it.
        var projectId = Guid.NewGuid();
        var record = new VisionRecord
        {
            ProjectId = projectId,
            Statements = new[] { new VisionStatement { Text = "Draft only." } },
            ApprovedAt = null,
        };
        await _store.SaveAsync(record);

        var loaded = await _store.GetByProjectAsync(projectId);
        Assert.NotNull(loaded);
        Assert.False(loaded!.IsApproved);
        Assert.Null(loaded.ApprovedAt);
    }
}
