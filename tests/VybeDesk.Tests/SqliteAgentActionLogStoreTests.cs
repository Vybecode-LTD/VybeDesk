using VybeDesk.Core.Models;
using VybeDesk.Services.Storage;
using Xunit;

namespace VybeDesk.Tests;

/// <summary>
/// SQLite-backed agent action log store tests. Mirrors
/// <see cref="SqliteAuditHistoryStoreTests"/> in shape — the agent log has
/// the same per-project newest-first surface plus two extras the audit
/// history doesn't need: <c>GetMostRecentUndoableAsync</c> for picking the
/// next eligible undo target, and <c>UpdateStatusAsync</c> for marking an
/// undone entry without removing it (so the history list can still show
/// "this was done and then undone" instead of going silent).
/// </summary>
public sealed class SqliteAgentActionLogStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _db;
    private readonly SqliteAgentActionLogStore _store;

    public SqliteAgentActionLogStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            "vybedesk-tests-agent-actions-" + Guid.NewGuid().ToString("N") + ".db");
        _db = new Database(_dbPath);
        _store = new SqliteAgentActionLogStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private static AgentActionLogEntry MakeEntry(
        Guid projectId,
        AgentActionKind kind = AgentActionKind.CreateFile,
        string path = "C:/scratch/example.txt",
        string? originalContent = null,
        AgentActionLogStatus status = AgentActionLogStatus.Done,
        DateTimeOffset? executedAt = null,
        string description = "Create file: example.txt")
        => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = kind,
            Path = path,
            DestinationPath = "",
            OriginalContent = originalContent,
            Status = status,
            ExecutedAt = executedAt ?? DateTimeOffset.Now,
            Description = description,
        };

    [Fact]
    public async Task AddAsync_RoundTripsAllFieldsIncludingOriginalContent()
    {
        var projectId = Guid.NewGuid();
        var executedAt = DateTimeOffset.Now;
        var entry = new AgentActionLogEntry
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = AgentActionKind.EditFile,
            Path = "C:/proj/notes.md",
            DestinationPath = "",
            OriginalContent = "the original body\nwith two lines",
            Status = AgentActionLogStatus.Done,
            ExecutedAt = executedAt,
            Description = "Edit file: notes.md",
        };
        await _store.AddAsync(entry);

        var entries = await _store.GetByProjectAsync(projectId);
        var loaded = Assert.Single(entries);
        Assert.Equal(entry.Id, loaded.Id);
        Assert.Equal(projectId, loaded.ProjectId);
        Assert.Equal(AgentActionKind.EditFile, loaded.Kind);
        Assert.Equal("C:/proj/notes.md", loaded.Path);
        Assert.Equal("", loaded.DestinationPath);
        Assert.Equal("the original body\nwith two lines", loaded.OriginalContent);
        Assert.Equal(AgentActionLogStatus.Done, loaded.Status);
        Assert.Equal(executedAt.ToUnixTimeMilliseconds(),
            loaded.ExecutedAt.ToUnixTimeMilliseconds());
        Assert.Equal("Edit file: notes.md", loaded.Description);
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsNewestFirst()
    {
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.Now;

        await _store.AddAsync(MakeEntry(projectId,
            executedAt: now.AddMinutes(-30), description: "middle"));
        await _store.AddAsync(MakeEntry(projectId,
            executedAt: now, description: "newest"));
        await _store.AddAsync(MakeEntry(projectId,
            executedAt: now.AddHours(-2), description: "oldest"));

        var entries = await _store.GetByProjectAsync(projectId);
        Assert.Equal(3, entries.Count);
        Assert.Equal("newest", entries[0].Description);
        Assert.Equal("middle", entries[1].Description);
        Assert.Equal("oldest", entries[2].Description);
    }

    [Fact]
    public async Task GetByProjectAsync_IsScopedToProject()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        await _store.AddAsync(MakeEntry(projectA));
        await _store.AddAsync(MakeEntry(projectA));
        await _store.AddAsync(MakeEntry(projectB));

        Assert.Equal(2, (await _store.GetByProjectAsync(projectA)).Count);
        Assert.Single(await _store.GetByProjectAsync(projectB));
    }

    [Fact]
    public async Task GetMostRecentUndoableAsync_ReturnsMostRecentDone_SkippingNewerUndone()
    {
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.Now;

        // Oldest: Done.
        var oldDone = MakeEntry(projectId,
            executedAt: now.AddHours(-2),
            description: "old-done");
        // Middle: Done — should be the winner (most recent Done).
        var midDone = MakeEntry(projectId,
            executedAt: now.AddMinutes(-30),
            description: "mid-done");
        // Newest: Undone — must be skipped despite being most-recent overall.
        var newUndone = MakeEntry(projectId,
            executedAt: now,
            status: AgentActionLogStatus.Undone,
            description: "new-undone");

        await _store.AddAsync(oldDone);
        await _store.AddAsync(midDone);
        await _store.AddAsync(newUndone);

        var result = await _store.GetMostRecentUndoableAsync(projectId);
        Assert.NotNull(result);
        Assert.Equal(midDone.Id, result!.Id);
        Assert.Equal(AgentActionLogStatus.Done, result.Status);
    }

    [Fact]
    public async Task GetMostRecentUndoableAsync_ReturnsNullWhenOnlyUndoneEntriesExist()
    {
        var projectId = Guid.NewGuid();
        await _store.AddAsync(MakeEntry(projectId,
            status: AgentActionLogStatus.Undone));
        await _store.AddAsync(MakeEntry(projectId,
            status: AgentActionLogStatus.Undone));

        Assert.Null(await _store.GetMostRecentUndoableAsync(projectId));
    }

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus_VerifiableViaRequery()
    {
        var projectId = Guid.NewGuid();
        var entry = MakeEntry(projectId, status: AgentActionLogStatus.Done);
        await _store.AddAsync(entry);

        await _store.UpdateStatusAsync(entry.Id, AgentActionLogStatus.Undone);

        var loaded = Assert.Single(await _store.GetByProjectAsync(projectId));
        Assert.Equal(AgentActionLogStatus.Undone, loaded.Status);
        // And the undoable picker now returns null since the only entry is Undone.
        Assert.Null(await _store.GetMostRecentUndoableAsync(projectId));
    }

    [Fact]
    public async Task RemoveAsync_DeletesOnlyTheSpecifiedEntry()
    {
        var projectId = Guid.NewGuid();
        var keep = MakeEntry(projectId, description: "keep");
        var doomed = MakeEntry(projectId, description: "doomed");
        await _store.AddAsync(keep);
        await _store.AddAsync(doomed);

        await _store.RemoveAsync(doomed.Id);

        var entries = await _store.GetByProjectAsync(projectId);
        Assert.Single(entries);
        Assert.Equal(keep.Id, entries[0].Id);
    }

    [Fact]
    public async Task ClearProjectAsync_WipesEveryEntryForTheProject_LeavingOtherProjectsIntact()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        await _store.AddAsync(MakeEntry(projectA));
        await _store.AddAsync(MakeEntry(projectA));
        await _store.AddAsync(MakeEntry(projectB));

        await _store.ClearProjectAsync(projectA);

        Assert.Empty(await _store.GetByProjectAsync(projectA));
        Assert.Single(await _store.GetByProjectAsync(projectB));
    }

    [Fact]
    public async Task GetMostRecentUndoneAsync_ReturnsMostRecentUndoneEntry()
    {
        // Mirror of GetMostRecentUndoable but for the Undone side: the
        // newest Undone entry wins, older Undone entries are skipped, and
        // any Done entry (regardless of timestamp) is ignored.
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.Now;

        var oldUndone = MakeEntry(projectId,
            executedAt: now.AddHours(-2),
            status: AgentActionLogStatus.Undone,
            description: "old-undone");
        var midUndone = MakeEntry(projectId,
            executedAt: now.AddMinutes(-30),
            status: AgentActionLogStatus.Undone,
            description: "mid-undone");
        // Newest entry is Done — must be skipped by GetMostRecentUndoneAsync.
        var newDone = MakeEntry(projectId,
            executedAt: now,
            status: AgentActionLogStatus.Done,
            description: "new-done");

        await _store.AddAsync(oldUndone);
        await _store.AddAsync(midUndone);
        await _store.AddAsync(newDone);

        var result = await _store.GetMostRecentUndoneAsync(projectId);
        Assert.NotNull(result);
        Assert.Equal(midUndone.Id, result!.Id);
        Assert.Equal(AgentActionLogStatus.Undone, result.Status);
    }

    [Fact]
    public async Task GetMostRecentUndoneAsync_SkipsDoneEntries()
    {
        // No Undone entries at all → null. Done entries do not satisfy the
        // redo predicate even if they are the most-recent overall.
        var projectId = Guid.NewGuid();
        await _store.AddAsync(MakeEntry(projectId, status: AgentActionLogStatus.Done));
        await _store.AddAsync(MakeEntry(projectId, status: AgentActionLogStatus.Done));

        Assert.Null(await _store.GetMostRecentUndoneAsync(projectId));
    }

    [Fact]
    public async Task GetMostRecentUndoneAsync_ReturnsNull_WhenAllDone()
    {
        // Empty project → null (no entries at all).
        Assert.Null(await _store.GetMostRecentUndoneAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AddAsync_RoundTripsNewContent()
    {
        // The redo-data column (added v0.31) round-trips through SQLite,
        // including the multi-line case that exercises TEXT encoding.
        var projectId = Guid.NewGuid();
        var entry = MakeEntry(projectId, kind: AgentActionKind.CreateFile)
            with { NewContent = "the proposed body\nwith two lines" };

        await _store.AddAsync(entry);

        var loaded = Assert.Single(await _store.GetByProjectAsync(projectId));
        Assert.Equal("the proposed body\nwith two lines", loaded.NewContent);

        // Null round-trips as null (CreateFolder / Move case).
        var folderEntry = MakeEntry(projectId, kind: AgentActionKind.CreateFolder);
        Assert.Null(folderEntry.NewContent);
        await _store.AddAsync(folderEntry);
        var second = (await _store.GetByProjectAsync(projectId))
            .Single(e => e.Id == folderEntry.Id);
        Assert.Null(second.NewContent);
    }

    [Fact]
    public async Task ChangedEvent_FiresOnceForEachMutatingCall()
    {
        var projectId = Guid.NewGuid();
        var fires = 0;
        _store.Changed += () => fires++;

        var entry = MakeEntry(projectId);
        await _store.AddAsync(entry);                                          // +1
        await _store.UpdateStatusAsync(entry.Id, AgentActionLogStatus.Undone); // +1
        await _store.RemoveAsync(entry.Id);                                    // +1
        await _store.ClearProjectAsync(projectId);                             // +1

        Assert.Equal(4, fires);
    }
}
