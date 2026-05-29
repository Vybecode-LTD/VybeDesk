using Microsoft.Data.Sqlite;
using VybeDesk.Core.Models;
using VybeDesk.Services.Storage;
using Xunit;

namespace VybeDesk.Tests;

/// <summary>
/// Proves that <see cref="SqliteProjectStore.RemoveAsync"/> cascade-deletes
/// all project-scoped rows across every dependent table in a single
/// transaction. Seeds one project plus rows in all 7 project-scoped tables,
/// calls <c>RemoveAsync</c>, and asserts zero rows remain.
/// </summary>
public sealed class SqliteProjectStoreCascadeDeleteTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _db;
    private readonly SqliteProjectStore _store;
    private readonly Guid _projectId = Guid.NewGuid();

    public SqliteProjectStoreCascadeDeleteTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            "vybedesk-tests-cascade-" + Guid.NewGuid().ToString("N") + ".db");
        _db = new Database(_dbPath);
        _store = new SqliteProjectStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    /// <summary>Seeds the project plus one row in every project-scoped table.</summary>
    private async Task SeedAllTablesAsync()
    {
        var idStr = _projectId.ToString();

        // 1. The project itself.
        await _store.AddAsync(new Project
        {
            Id = _projectId,
            Name = "Cascade test project",
            Description = "Will be deleted with all dependents",
            FolderPath = @"C:\fake\path",
            Status = ProjectStatus.Active,
        });

        // 2-8. One row in each project-scoped table.
        await _db.WriteAsync(async c =>
        {
            await ExecAsync(c, @"
                INSERT INTO bugs (id, project_id, title, severity, status,
                    steps_to_reproduce, expected_result, actual_result, area, created)
                VALUES ($bid, $pid, 'Test bug', 0, 0, 'steps', 'expected', 'actual', 'area', 0);",
                ("$bid", Guid.NewGuid().ToString()), ("$pid", idStr));

            await ExecAsync(c, @"
                INSERT INTO testing_plans (id, project_id, strategy_summary,
                    frameworks_json, kinds_json, answers_json, created, modified)
                VALUES ($tid, $pid, 'plan', '[]', '[]', '{}', 0, 0);",
                ("$tid", Guid.NewGuid().ToString()), ("$pid", idStr));

            await ExecAsync(c, @"
                INSERT INTO vision_records (id, project_id, statements_json,
                    created, modified)
                VALUES ($vid, $pid, '[]', 0, 0);",
                ("$vid", Guid.NewGuid().ToString()), ("$pid", idStr));

            await ExecAsync(c, @"
                INSERT INTO audit_history (id, project_id, mode, report_md,
                    deep_dive_md, verdicts_json, generated_at)
                VALUES ($aid, $pid, 0, 'report', 'prompt', '[]', 0);",
                ("$aid", Guid.NewGuid().ToString()), ("$pid", idStr));

            await ExecAsync(c, @"
                INSERT INTO agent_actions (id, project_id, kind, path,
                    original_content, status, executed_at, description)
                VALUES ($aaid, $pid, 0, '/test', '', 0, '0', 'test action');",
                ("$aaid", Guid.NewGuid().ToString()), ("$pid", idStr));

            await ExecAsync(c, @"
                INSERT INTO notes (id, title, body, tags, project_id, created)
                VALUES ($nid, 'test note', 'body', '', $pid, 0);",
                ("$nid", Guid.NewGuid().ToString()), ("$pid", idStr));

            await ExecAsync(c, @"
                INSERT INTO ai_calls (id, project_id, module, model,
                    input_tokens, output_tokens, cost_estimate, timestamp)
                VALUES ($cid, $pid, 'test', 'test-model', 10, 20, 0.001, 0);",
                ("$cid", Guid.NewGuid().ToString()), ("$pid", idStr));
        });
    }

    private static async Task ExecAsync(SqliteConnection c, string sql,
        params (string name, string value)[] parameters)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> CountRowsAsync(string table)
    {
        return await _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE project_id=$pid;";
            cmd.Parameters.AddWithValue("$pid", _projectId.ToString());
            return (long)(await cmd.ExecuteScalarAsync())!;
        });
    }

    // ── Tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_DeletesBugsForProject()
    {
        await SeedAllTablesAsync();
        Assert.Equal(1, await CountRowsAsync("bugs"));

        await _store.RemoveAsync(_projectId);

        Assert.Equal(0, await CountRowsAsync("bugs"));
    }

    [Fact]
    public async Task RemoveAsync_DeletesTestingPlanForProject()
    {
        await SeedAllTablesAsync();
        Assert.Equal(1, await CountRowsAsync("testing_plans"));

        await _store.RemoveAsync(_projectId);

        Assert.Equal(0, await CountRowsAsync("testing_plans"));
    }

    [Fact]
    public async Task RemoveAsync_DeletesVisionRecordForProject()
    {
        await SeedAllTablesAsync();
        Assert.Equal(1, await CountRowsAsync("vision_records"));

        await _store.RemoveAsync(_projectId);

        Assert.Equal(0, await CountRowsAsync("vision_records"));
    }

    [Fact]
    public async Task RemoveAsync_DeletesAuditHistoryForProject()
    {
        await SeedAllTablesAsync();
        Assert.Equal(1, await CountRowsAsync("audit_history"));

        await _store.RemoveAsync(_projectId);

        Assert.Equal(0, await CountRowsAsync("audit_history"));
    }

    [Fact]
    public async Task RemoveAsync_DeletesAgentActionsForProject()
    {
        await SeedAllTablesAsync();
        Assert.Equal(1, await CountRowsAsync("agent_actions"));

        await _store.RemoveAsync(_projectId);

        Assert.Equal(0, await CountRowsAsync("agent_actions"));
    }

    [Fact]
    public async Task RemoveAsync_DeletesNotesForProject()
    {
        await SeedAllTablesAsync();
        Assert.Equal(1, await CountRowsAsync("notes"));

        await _store.RemoveAsync(_projectId);

        Assert.Equal(0, await CountRowsAsync("notes"));
    }

    [Fact]
    public async Task RemoveAsync_DeletesAiCallsForProject()
    {
        await SeedAllTablesAsync();
        Assert.Equal(1, await CountRowsAsync("ai_calls"));

        await _store.RemoveAsync(_projectId);

        Assert.Equal(0, await CountRowsAsync("ai_calls"));
    }

    [Fact]
    public async Task RemoveAsync_DeletesProjectItself()
    {
        await SeedAllTablesAsync();

        await _store.RemoveAsync(_projectId);

        var all = await _store.GetAllAsync();
        Assert.DoesNotContain(all, p => p.Id == _projectId);
    }

    [Fact]
    public async Task RemoveAsync_DoesNotDeleteOtherProjectRows()
    {
        await SeedAllTablesAsync();

        // Add a second project with its own bug.
        var otherProjectId = Guid.NewGuid();
        await _store.AddAsync(new Project
        {
            Id = otherProjectId,
            Name = "Survivor",
            Description = "Should not be touched",
            FolderPath = @"C:\other",
            Status = ProjectStatus.Active,
        });
        await _db.WriteAsync(async c =>
        {
            await ExecAsync(c, @"
                INSERT INTO bugs (id, project_id, title, severity, status,
                    steps_to_reproduce, expected_result, actual_result, area, created)
                VALUES ($bid, $pid, 'Survivor bug', 0, 0, 's', 'e', 'a', 'x', 0);",
                ("$bid", Guid.NewGuid().ToString()),
                ("$pid", otherProjectId.ToString()));
        });

        // Delete only the FIRST project.
        await _store.RemoveAsync(_projectId);

        // The second project's bug survives.
        var otherBugs = await _db.ReadAsync(async c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM bugs WHERE project_id=$pid;";
            cmd.Parameters.AddWithValue("$pid", otherProjectId.ToString());
            return (long)(await cmd.ExecuteScalarAsync())!;
        });
        Assert.Equal(1, otherBugs);

        // The second project itself survives.
        var all = await _store.GetAllAsync();
        Assert.Contains(all, p => p.Id == otherProjectId);
    }

    [Fact]
    public async Task RemoveAsync_FiresChangedEvent()
    {
        await SeedAllTablesAsync();
        var fired = false;
        _store.Changed += () => fired = true;

        await _store.RemoveAsync(_projectId);

        Assert.True(fired);
    }
}
