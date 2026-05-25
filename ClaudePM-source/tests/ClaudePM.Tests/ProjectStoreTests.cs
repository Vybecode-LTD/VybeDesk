using ClaudePM.Core.Models;
using ClaudePM.Services.Storage;
using Xunit;

namespace ClaudePM.Tests;

public class ProjectStoreTests
{
    [Fact]
    public async Task SeededStore_ReturnsProjects()
    {
        var store = new InMemoryProjectStore();
        var all = await store.GetAllAsync();
        Assert.NotEmpty(all);
    }

    [Fact]
    public async Task AddAsync_IncreasesCount()
    {
        var store = new InMemoryProjectStore();
        var before = (await store.GetAllAsync()).Count;

        await store.AddAsync(new Project { Name = "New Project" });

        var after = (await store.GetAllAsync()).Count;
        Assert.Equal(before + 1, after);
    }

    [Fact]
    public async Task RemoveAsync_RemovesProject()
    {
        var store = new InMemoryProjectStore();
        var first = (await store.GetAllAsync())[0];

        await store.RemoveAsync(first.Id);

        var all = await store.GetAllAsync();
        Assert.DoesNotContain(all, p => p.Id == first.Id);
    }
}
