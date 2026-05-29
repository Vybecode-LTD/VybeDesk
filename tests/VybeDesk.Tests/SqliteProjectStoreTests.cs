using Microsoft.Data.Sqlite;
using VybeDesk.Core.Models;
using VybeDesk.Services.Storage;
using Xunit;

namespace VybeDesk.Tests;

/// <summary>
/// SQLite-backed project store tests covering the M4 #16 per-project
/// override columns (<c>model</c> + <c>default_output_path</c>). Fresh temp
/// DB per test for isolation. The migration is exercised implicitly by the
/// fresh-DB open in the ctor — the Schema const already includes the
/// columns, and the EnsureColumn idempotency check in
/// <see cref="Database"/> is verified by simply re-opening the same file
/// without error in <see cref="Migration_IsIdempotent_OnRepeatedOpen"/>.
/// </summary>
public sealed class SqliteProjectStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _db;
    private readonly SqliteProjectStore _store;

    public SqliteProjectStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            "vybedesk-tests-projects-" + Guid.NewGuid().ToString("N") + ".db");
        _db = new Database(_dbPath);
        _store = new SqliteProjectStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task AddAsync_RoundTripsModelAndDefaultOutputPath()
    {
        var project = new Project
        {
            Name = "Has overrides",
            Description = "exercises the new columns",
            FolderPath = @"C:\dev\overrides",
            Model = "claude-opus-4-1",
            DefaultOutputPath = @"D:\overrides\output",
        };

        await _store.AddAsync(project);

        var all = await _store.GetAllAsync();
        var loaded = all.Single(p => p.Id == project.Id);
        Assert.Equal("claude-opus-4-1", loaded.Model);
        Assert.Equal(@"D:\overrides\output", loaded.DefaultOutputPath);
    }

    [Fact]
    public async Task Model_DefaultsToNull_OnFreshProject()
    {
        // A user who never opens the model override field should get null
        // back — that's the "use the global default" sentinel the AI service
        // checks for.
        var project = new Project { Name = "No overrides" };
        await _store.AddAsync(project);

        var loaded = (await _store.GetAllAsync()).Single(p => p.Id == project.Id);
        Assert.Null(loaded.Model);
    }

    [Fact]
    public async Task DefaultOutputPath_DefaultsToNull_OnFreshProject()
    {
        var project = new Project { Name = "No overrides" };
        await _store.AddAsync(project);

        var loaded = (await _store.GetAllAsync()).Single(p => p.Id == project.Id);
        Assert.Null(loaded.DefaultOutputPath);
    }

    [Fact]
    public async Task UpdateAsync_PersistsOverrideChanges()
    {
        // Start with no overrides; set both via Update; round-trip; clear
        // them again to null; round-trip. Both directions must survive the
        // DBNull bind path.
        var project = new Project { Name = "Mutable" };
        await _store.AddAsync(project);

        project.Model = "claude-haiku";
        project.DefaultOutputPath = @"E:\out";
        await _store.UpdateAsync(project);

        var afterSet = (await _store.GetAllAsync()).Single(p => p.Id == project.Id);
        Assert.Equal("claude-haiku", afterSet.Model);
        Assert.Equal(@"E:\out", afterSet.DefaultOutputPath);

        project.Model = null;
        project.DefaultOutputPath = null;
        await _store.UpdateAsync(project);

        var afterClear = (await _store.GetAllAsync()).Single(p => p.Id == project.Id);
        Assert.Null(afterClear.Model);
        Assert.Null(afterClear.DefaultOutputPath);
    }

    [Fact]
    public async Task AddAsync_RoundTripsLogoPath()
    {
        // The M5 #17 enhancement column: stored as nullable TEXT, surfaces
        // on the Home dashboard card. Round-trips a non-null value here;
        // the null branch is covered by every other test that doesn't
        // set LogoPath (all .Model assertions also load a Project whose
        // LogoPath defaults to null without throwing).
        var project = new Project
        {
            Name = "Has logo",
            LogoPath = @"C:\dev\some-project\favicon.ico",
        };

        await _store.AddAsync(project);

        var loaded = (await _store.GetAllAsync()).Single(p => p.Id == project.Id);
        Assert.Equal(@"C:\dev\some-project\favicon.ico", loaded.LogoPath);
    }

    [Fact]
    public void Migration_IsIdempotent_OnRepeatedOpen()
    {
        // Re-open the same file. EnsureColumn is the only thing that runs
        // beyond the IF NOT EXISTS DDL, and it must early-out cleanly when
        // the columns are already present. Failure mode would be a thrown
        // SqliteException ("duplicate column name").
        using var second = new Database(_dbPath);
        // No assertion needed — the test passes if the second open
        // completes without throwing.
        Assert.NotNull(second);
    }

    [Fact]
    public async Task RemoveAsync_CascadeDeletesDependentRows()
    {
        // Insert a project, add dependent rows in bugs + agent_actions,
        // then delete the project. Verify all dependent rows are gone but
        // rows scoped to a DIFFERENT project survive.
        var projectId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var project = new Project { Id = projectId, Name = "Cascade target" };
        var other = new Project { Id = otherId, Name = "Survivor" };
        await _store.AddAsync(project);
        await _store.AddAsync(other);

        // Insert dependent rows directly via SQL (we don't need the full
        // store API for this — we just need rows present in the tables).
        await _db.WriteAsync(async c =>
        {
            await InsertRow(c, "bugs",
                "id, project_id, title, severity, status, created",
                $"'{Guid.NewGuid()}', '{projectId}', 'Bug A', 1, 0, 0");
            await InsertRow(c, "bugs",
                "id, project_id, title, severity, status, created",
                $"'{Guid.NewGuid()}', '{otherId}', 'Bug B', 1, 0, 0");
            await InsertRow(c, "agent_actions",
                "id, project_id, kind, path, status, executed_at, description",
                $"'{Guid.NewGuid()}', '{projectId}', 0, '/tmp/a', 0, '2026-01-01', 'test'");
            await InsertRow(c, "testing_plans",
                "id, project_id, created, modified",
                $"'{Guid.NewGuid()}', '{projectId}', 0, 0");
        });

        await _store.RemoveAsync(projectId);

        // Project itself is gone
        var projects = await _store.GetAllAsync();
        Assert.DoesNotContain(projects, p => p.Id == projectId);
        Assert.Contains(projects, p => p.Id == otherId);

        // Dependent rows for the deleted project are gone
        await _db.ReadAsync(async c =>
        {
            Assert.Equal(0, await CountRows(c, "bugs", projectId));
            Assert.Equal(0, await CountRows(c, "agent_actions", projectId));
            Assert.Equal(0, await CountRows(c, "testing_plans", projectId));
            // Other project's data is untouched
            Assert.Equal(1, await CountRows(c, "bugs", otherId));
            return true;
        });
    }

    private static async Task InsertRow(SqliteConnection c, string table, string columns, string values)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"INSERT INTO {table} ({columns}) VALUES ({values});";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountRows(SqliteConnection c, string table, Guid projectId)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE project_id = $pid;";
        cmd.Parameters.AddWithValue("$pid", projectId.ToString());
        return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    }
}
