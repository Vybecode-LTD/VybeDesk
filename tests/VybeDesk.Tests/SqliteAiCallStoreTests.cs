using Microsoft.Data.Sqlite;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using VybeDesk.Services.Storage;
using Xunit;

namespace VybeDesk.Tests;

public class SqliteAiCallStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly Database _db;
    private readonly SqliteAiCallStore _store;

    public SqliteAiCallStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "vybedesk-test-aicalls-" + Guid.NewGuid() + ".db");
        _db = new Database(_dbPath);
        _store = new SqliteAiCallStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
    }

    private AiCallRecord MakeRecord(Guid? projectId = null, int inputTokens = 100,
        int outputTokens = 50, double cost = 0.01, DateTimeOffset? timestamp = null)
    {
        return new AiCallRecord
        {
            ProjectId = projectId,
            Module = "Notebook",
            Model = "claude-sonnet-4-20250514",
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = 10,
            CacheReadInputTokens = 5,
            CostEstimate = cost,
            DurationMs = 1200,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        };
    }

    [Fact]
    public async Task AddThenGetRecent_RoundTripsAllFields()
    {
        var pid = Guid.NewGuid();
        var record = MakeRecord(pid, inputTokens: 200, outputTokens: 80, cost: 0.05);
        await _store.AddAsync(record);

        var recent = await _store.GetRecentAsync();
        var found = Assert.Single(recent);

        Assert.Equal(record.Id, found.Id);
        Assert.Equal(pid, found.ProjectId);
        Assert.Equal("Notebook", found.Module);
        Assert.Equal("claude-sonnet-4-20250514", found.Model);
        Assert.Equal(200, found.InputTokens);
        Assert.Equal(80, found.OutputTokens);
        Assert.Equal(10, found.CacheCreationInputTokens);
        Assert.Equal(5, found.CacheReadInputTokens);
        Assert.Equal(0.05, found.CostEstimate, 4);
        Assert.Equal(1200, found.DurationMs);
    }

    [Fact]
    public async Task GetRecent_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
            await _store.AddAsync(MakeRecord());

        var limited = await _store.GetRecentAsync(limit: 3);
        Assert.Equal(3, limited.Count);
    }

    [Fact]
    public async Task GetRecent_OrdersNewestFirst()
    {
        var older = MakeRecord(timestamp: DateTimeOffset.UtcNow.AddHours(-2));
        var newer = MakeRecord(timestamp: DateTimeOffset.UtcNow);
        await _store.AddAsync(older);
        await _store.AddAsync(newer);

        var recent = await _store.GetRecentAsync();
        Assert.Equal(newer.Id, recent[0].Id);
        Assert.Equal(older.Id, recent[1].Id);
    }

    [Fact]
    public async Task GetByProject_FiltersCorrectly()
    {
        var pidA = Guid.NewGuid();
        var pidB = Guid.NewGuid();
        await _store.AddAsync(MakeRecord(pidA));
        await _store.AddAsync(MakeRecord(pidA));
        await _store.AddAsync(MakeRecord(pidB));

        var projectA = await _store.GetByProjectAsync(pidA);
        Assert.Equal(2, projectA.Count);
        Assert.All(projectA, r => Assert.Equal(pidA, r.ProjectId));
    }

    [Fact]
    public async Task GetSummary_AggregatesCorrectly()
    {
        await _store.AddAsync(MakeRecord(inputTokens: 100, outputTokens: 50, cost: 0.01));
        await _store.AddAsync(MakeRecord(inputTokens: 200, outputTokens: 80, cost: 0.03));

        var summary = await _store.GetSummaryAsync();

        Assert.Equal(2, summary.TotalCalls);
        Assert.Equal(300, summary.TotalInputTokens);
        Assert.Equal(130, summary.TotalOutputTokens);
        Assert.Equal(0.04, summary.TotalCost, 4);
    }

    [Fact]
    public async Task GetSummary_EmptyTable_ReturnsZeros()
    {
        var summary = await _store.GetSummaryAsync();

        Assert.Equal(0, summary.TotalCalls);
        Assert.Equal(0, summary.TotalInputTokens);
        Assert.Equal(0, summary.TotalOutputTokens);
        Assert.Equal(0.0, summary.TotalCost);
    }

    [Fact]
    public async Task NullProjectId_RoundTripsAsNull()
    {
        await _store.AddAsync(MakeRecord(projectId: null));
        var recent = await _store.GetRecentAsync();
        Assert.Null(Assert.Single(recent).ProjectId);
    }

    [Fact]
    public async Task Changed_FiresOnAdd()
    {
        var count = 0;
        _store.Changed += () => count++;

        await _store.AddAsync(MakeRecord());
        Assert.Equal(1, count);
    }
}
