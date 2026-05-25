using ClaudePM.Core.Models;
using ClaudePM.Services.Storage;
using Xunit;

namespace ClaudePM.Tests;

/// <summary>
/// SQLite-backed audit history store tests. Covers the four behaviours the
/// Vision Audit history feature needs: add, get-by-project (newest-first
/// ordering), per-project scoping, remove single entry, clear-all-for-project.
/// Plus verdict-JSON round-trip so historical entries can re-populate the
/// per-statement card view exactly as they were rendered originally.
/// </summary>
public sealed class SqliteAuditHistoryStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _db;
    private readonly SqliteAuditHistoryStore _store;

    public SqliteAuditHistoryStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            "claudepm-tests-audit-history-" + Guid.NewGuid().ToString("N") + ".db");
        _db = new Database(_dbPath);
        _store = new SqliteAuditHistoryStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task GetByProjectAsync_EmptyWhenNothingStored()
        => Assert.Empty(await _store.GetByProjectAsync(Guid.NewGuid()));

    [Fact]
    public async Task AddAsync_RoundTripsAllFields_AndVerdictJson()
    {
        var projectId = Guid.NewGuid();
        var stmtId = Guid.NewGuid();
        var entry = new AuditHistoryEntry
        {
            ProjectId = projectId,
            Mode = AuditMode.Targeted,
            OffTrackCount = 3,
            AtRiskCount = 1,
            OnTrackCount = 7,
            ReportMarkdown = "# Report",
            DeepDivePrompt = "# Prompt",
            Verdicts = new[]
            {
                new StatementVerdict(stmtId, "X must work.",
                    AlignmentRank.OffTrack, "no code", "add code"),
            },
            GeneratedAt = DateTimeOffset.Now,
        };
        await _store.AddAsync(entry);

        var entries = await _store.GetByProjectAsync(projectId);
        var loaded = Assert.Single(entries);
        Assert.Equal(entry.Id, loaded.Id);
        Assert.Equal(AuditMode.Targeted, loaded.Mode);
        Assert.Equal(3, loaded.OffTrackCount);
        Assert.Equal("# Report", loaded.ReportMarkdown);
        Assert.Equal("# Prompt", loaded.DeepDivePrompt);
        Assert.Single(loaded.Verdicts);
        Assert.Equal(stmtId, loaded.Verdicts[0].StatementId);
        Assert.Equal(AlignmentRank.OffTrack, loaded.Verdicts[0].Rank);
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsNewestFirst()
    {
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.Now;

        // Insert intentionally out of chronological order to prove the
        // ORDER BY is real and not just an insertion artefact.
        await _store.AddAsync(new AuditHistoryEntry
        {
            ProjectId = projectId,
            GeneratedAt = now.AddMinutes(-30),
            ReportMarkdown = "middle",
        });
        await _store.AddAsync(new AuditHistoryEntry
        {
            ProjectId = projectId,
            GeneratedAt = now,
            ReportMarkdown = "newest",
        });
        await _store.AddAsync(new AuditHistoryEntry
        {
            ProjectId = projectId,
            GeneratedAt = now.AddHours(-2),
            ReportMarkdown = "oldest",
        });

        var entries = await _store.GetByProjectAsync(projectId);
        Assert.Equal(3, entries.Count);
        Assert.Equal("newest", entries[0].ReportMarkdown);
        Assert.Equal("middle", entries[1].ReportMarkdown);
        Assert.Equal("oldest", entries[2].ReportMarkdown);
    }

    [Fact]
    public async Task GetByProjectAsync_IsScopedToProject()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        await _store.AddAsync(new AuditHistoryEntry { ProjectId = projectA });
        await _store.AddAsync(new AuditHistoryEntry { ProjectId = projectA });
        await _store.AddAsync(new AuditHistoryEntry { ProjectId = projectB });

        Assert.Equal(2, (await _store.GetByProjectAsync(projectA)).Count);
        Assert.Single(await _store.GetByProjectAsync(projectB));
    }

    [Fact]
    public async Task RemoveAsync_DeletesOnlyTheSpecifiedEntry()
    {
        var projectId = Guid.NewGuid();
        var keep = new AuditHistoryEntry { ProjectId = projectId };
        var doomed = new AuditHistoryEntry { ProjectId = projectId };
        await _store.AddAsync(keep);
        await _store.AddAsync(doomed);

        await _store.RemoveAsync(doomed.Id);

        var entries = await _store.GetByProjectAsync(projectId);
        Assert.Single(entries);
        Assert.Equal(keep.Id, entries[0].Id);
    }

    [Fact]
    public async Task ClearProjectAsync_RemovesEveryEntryForTheProject()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        await _store.AddAsync(new AuditHistoryEntry { ProjectId = projectA });
        await _store.AddAsync(new AuditHistoryEntry { ProjectId = projectA });
        await _store.AddAsync(new AuditHistoryEntry { ProjectId = projectB });

        await _store.ClearProjectAsync(projectA);

        Assert.Empty(await _store.GetByProjectAsync(projectA));
        // Project B is untouched — clearing is scoped.
        Assert.Single(await _store.GetByProjectAsync(projectB));
    }

    [Fact]
    public async Task ChangedEvent_FiresOnEveryMutatingCall()
    {
        var projectId = Guid.NewGuid();
        var fires = 0;
        _store.Changed += () => fires++;

        var entry = new AuditHistoryEntry { ProjectId = projectId };
        await _store.AddAsync(entry);
        await _store.RemoveAsync(entry.Id);
        await _store.ClearProjectAsync(projectId);

        Assert.Equal(3, fires);
    }
}
