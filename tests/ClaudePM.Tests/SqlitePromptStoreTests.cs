using ClaudePM.Core.Models;
using ClaudePM.Services.Storage;
using Xunit;

namespace ClaudePM.Tests;

/// <summary>
/// Exercises the SQLite-backed prompt store, with focus on FTS5 search
/// (schema creation, trigger sync, tokenization). Each test runs against a
/// fresh temp database to keep state isolated.
/// </summary>
public sealed class SqlitePromptStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _db;
    private readonly SqlitePromptStore _store;

    public SqlitePromptStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            "claudepm-tests-" + Guid.NewGuid().ToString("N") + ".db");
        _db = new Database(_dbPath);
        _store = new SqlitePromptStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsAllSeededPrompts()
    {
        var all = await _store.GetAllAsync();
        var search = await _store.SearchAsync("");

        Assert.Equal(all.Count, search.Count);
        Assert.True(all.Count >= 2, "Database seed should provide at least two prompts.");
    }

    [Fact]
    public async Task SearchAsync_MatchesTitleSubstring()
    {
        var hits = await _store.SearchAsync("scaffold");

        Assert.Contains(hits, p => p.Title.Contains("Scaffold", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_MatchesTagToken()
    {
        var hits = await _store.SearchAsync("docs");

        Assert.Contains(hits, p => p.Tags.Contains("docs"));
    }

    [Fact]
    public async Task SearchAsync_AfterAdd_FindsNewPromptViaTrigger()
    {
        var entry = new PromptEntry
        {
            Title = "Refactor a service",
            Body = "Refactor the {{service_name}} service into cleaner layers.",
            Category = "Refactoring",
            Tags = { "refactor", "architecture" },
        };
        await _store.AddAsync(entry);

        var hits = await _store.SearchAsync("refactor");

        Assert.Contains(hits, p => p.Id == entry.Id);
    }

    [Fact]
    public async Task SearchAsync_AfterUpdate_FindsByNewWordOnly()
    {
        var entry = new PromptEntry
        {
            Title = "Initial title abracadabra",
            Body = "Body.",
            Category = "Misc",
        };
        await _store.AddAsync(entry);

        entry.Title = "Replaced title zymurgy";
        await _store.UpdateAsync(entry);

        var oldHits = await _store.SearchAsync("abracadabra");
        var newHits = await _store.SearchAsync("zymurgy");

        Assert.DoesNotContain(oldHits, p => p.Id == entry.Id);
        Assert.Contains(newHits, p => p.Id == entry.Id);
    }

    [Fact]
    public async Task SearchAsync_AfterRemove_DropsFromIndex()
    {
        var entry = new PromptEntry
        {
            Title = "Ephemeral marker xyzzy",
            Body = "Body.",
            Category = "Misc",
        };
        await _store.AddAsync(entry);

        await _store.RemoveAsync(entry.Id);

        var hits = await _store.SearchAsync("xyzzy");
        Assert.DoesNotContain(hits, p => p.Id == entry.Id);
    }

    [Fact]
    public async Task SearchAsync_SanitizesFtsOperators()
    {
        // The query contains FTS5 metacharacters that would break a naive
        // pass-through. We just need the call to not throw and to return
        // sensibly (no match on this gibberish, but also no exception).
        var hits = await _store.SearchAsync("\"NOT * AND OR (");
        Assert.NotNull(hits);
    }
}
