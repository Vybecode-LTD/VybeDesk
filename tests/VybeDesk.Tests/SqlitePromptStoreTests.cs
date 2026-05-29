using VybeDesk.Core.Models;
using VybeDesk.Services.Storage;
using Xunit;

namespace VybeDesk.Tests;

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
            "vybedesk-tests-" + Guid.NewGuid().ToString("N") + ".db");
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
        // Insert a known fixture rather than relying on whatever the
        // current seed happens to contain — keeps the test stable as the
        // curated seed evolves.
        var entry = new PromptEntry
        {
            Title = "ScaffoldTitleFixture for substring search",
            Body = "Body.",
        };
        await _store.AddAsync(entry);

        var hits = await _store.SearchAsync("ScaffoldTitleFixture");

        Assert.Contains(hits, p => p.Title.Contains("ScaffoldTitleFixture",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_MatchesTagToken()
    {
        var entry = new PromptEntry
        {
            Title = "Fixture for tag search",
            Body = "Body.",
            Tags = { "tagsearchfixture" },
        };
        await _store.AddAsync(entry);

        var hits = await _store.SearchAsync("tagsearchfixture");

        Assert.Contains(hits, p => p.Tags.Contains("tagsearchfixture"));
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

    [Fact]
    public async Task UpdateAsync_ContentChange_CreatesVersion()
    {
        var entry = new PromptEntry { Title = "Original", Body = "First body." };
        await _store.AddAsync(entry);

        entry.Body = "Second body.";
        await _store.UpdateAsync(entry);

        var versions = await _store.GetVersionsAsync(entry.Id);
        Assert.Single(versions);
        Assert.Equal("First body.", versions[0].Body);
        Assert.Equal("Original", versions[0].Title);
    }

    [Fact]
    public async Task UpdateAsync_UsageCountOnly_DoesNotCreateVersion()
    {
        var entry = new PromptEntry { Title = "T", Body = "Body" };
        await _store.AddAsync(entry);

        entry.UsageCount++;
        await _store.UpdateAsync(entry);

        var versions = await _store.GetVersionsAsync(entry.Id);
        Assert.Empty(versions);
    }

    [Fact]
    public async Task UpdateAsync_MultipleEdits_VersionsAreNewestFirst()
    {
        var entry = new PromptEntry { Title = "T", Body = "v1" };
        await _store.AddAsync(entry);

        entry.Body = "v2";
        await _store.UpdateAsync(entry);
        await Task.Delay(1100); // captured is unix seconds — separate the timestamps

        entry.Body = "v3";
        await _store.UpdateAsync(entry);

        var versions = await _store.GetVersionsAsync(entry.Id);
        Assert.Equal(2, versions.Count);
        Assert.Equal("v2", versions[0].Body); // newest snapshot = state before v3
        Assert.Equal("v1", versions[1].Body); // oldest snapshot = state before v2
    }

    [Fact]
    public async Task RemoveAsync_CascadesVersions()
    {
        var entry = new PromptEntry { Title = "T", Body = "v1" };
        await _store.AddAsync(entry);

        entry.Body = "v2";
        await _store.UpdateAsync(entry);
        Assert.NotEmpty(await _store.GetVersionsAsync(entry.Id));

        await _store.RemoveAsync(entry.Id);

        Assert.Empty(await _store.GetVersionsAsync(entry.Id));
    }

    [Fact]
    public async Task Changed_FiresOnEveryMutation()
    {
        int fireCount = 0;
        _store.Changed += () => fireCount++;

        var entry = new PromptEntry { Title = "T", Body = "B" };
        await _store.AddAsync(entry);
        Assert.Equal(1, fireCount);

        entry.Body = "Updated";
        await _store.UpdateAsync(entry);
        Assert.Equal(2, fireCount);

        await _store.RemoveAsync(entry.Id);
        Assert.Equal(3, fireCount);
    }
}
