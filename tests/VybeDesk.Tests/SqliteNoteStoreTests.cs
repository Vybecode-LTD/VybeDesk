using Microsoft.Data.Sqlite;
using VybeDesk.Core.Models;
using VybeDesk.Services.Storage;
using Xunit;

namespace VybeDesk.Tests;

public class SqliteNoteStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _db;
    private readonly SqliteNoteStore _store;

    public SqliteNoteStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "vybedesk-test-notes-" + Guid.NewGuid() + ".db");
        _db = new Database(_dbPath);
        _store = new SqliteNoteStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task AddThenGet_RoundTripsAllFields()
    {
        var projectId = Guid.NewGuid();
        var note = new Note
        {
            Title = "Test Note",
            Body = "Some body text",
            Tags = new List<string> { "alpha", "beta" },
            ProjectId = projectId,
        };

        await _store.AddAsync(note);
        var all = await _store.GetAllAsync();

        var found = Assert.Single(all);
        Assert.Equal(note.Id, found.Id);
        Assert.Equal("Test Note", found.Title);
        Assert.Equal("Some body text", found.Body);
        Assert.Equal(new[] { "alpha", "beta" }, found.Tags);
        Assert.Equal(projectId, found.ProjectId);
    }

    [Fact]
    public async Task Add_NullProjectId_RoundTripsAsNull()
    {
        var note = new Note
        {
            Title = "Global note",
            Body = "No project",
            ProjectId = null,
        };

        await _store.AddAsync(note);
        var all = await _store.GetAllAsync();

        Assert.Null(Assert.Single(all).ProjectId);
    }

    [Fact]
    public async Task GetAll_OrdersNewestFirst()
    {
        var older = new Note
        {
            Title = "Older",
            Body = "First",
            Created = DateTimeOffset.UtcNow.AddHours(-2),
        };
        var newer = new Note
        {
            Title = "Newer",
            Body = "Second",
            Created = DateTimeOffset.UtcNow,
        };

        await _store.AddAsync(older);
        await _store.AddAsync(newer);
        var all = await _store.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal("Newer", all[0].Title);
        Assert.Equal("Older", all[1].Title);
    }

    [Fact]
    public async Task Remove_DeletesNote()
    {
        var note = new Note { Title = "Doomed", Body = "Will be removed" };
        await _store.AddAsync(note);

        await _store.RemoveAsync(note.Id);
        var all = await _store.GetAllAsync();

        Assert.Empty(all);
    }

    [Fact]
    public async Task Changed_FiresOnAddAndRemove()
    {
        var count = 0;
        _store.Changed += () => count++;

        var note = new Note { Title = "x", Body = "y" };
        await _store.AddAsync(note);
        Assert.Equal(1, count);

        await _store.RemoveAsync(note.Id);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Tags_EmptyList_RoundTrips()
    {
        var note = new Note { Title = "No tags", Body = "none", Tags = new List<string>() };
        await _store.AddAsync(note);

        var all = await _store.GetAllAsync();
        Assert.Empty(Assert.Single(all).Tags);
    }
}
