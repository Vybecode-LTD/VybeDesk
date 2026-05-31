using System.Text.Json;
using VybeDesk.Services.Plugins;
using Xunit;

namespace VybeDesk.Tests;

public class PluginManifestTests
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Deserialize_FullManifest_MapsEveryField()
    {
        var json = """
        {
          "schemaVersion": 1,
          "id": "com.acme.todo",
          "name": "Todo",
          "version": "2.1.0",
          "author": "Acme",
          "description": "A todo module.",
          "entryAssembly": "Acme.Todo.dll",
          "entryType": "Acme.Todo.TodoModule",
          "minHostVersion": "1.1.0",
          "maxHostVersion": "2.0.0",
          "capabilities": ["filesystem", "network"]
        }
        """;

        var m = JsonSerializer.Deserialize<PluginManifest>(json, Opts)!;

        Assert.Equal(1, m.SchemaVersion);
        Assert.Equal("com.acme.todo", m.Id);
        Assert.Equal("Todo", m.Name);
        Assert.Equal("2.1.0", m.Version);
        Assert.Equal("Acme", m.Author);
        Assert.Equal("A todo module.", m.Description);
        Assert.Equal("Acme.Todo.dll", m.EntryAssembly);
        Assert.Equal("Acme.Todo.TodoModule", m.EntryType);
        Assert.Equal("1.1.0", m.MinHostVersion);
        Assert.Equal("2.0.0", m.MaxHostVersion);
        Assert.Equal(new[] { "filesystem", "network" }, m.Capabilities);
    }

    [Fact]
    public void Deserialize_MinimalManifest_AppliesDefaults()
    {
        var json = """{ "id": "com.acme.min", "entryAssembly": "M.dll" }""";

        var m = JsonSerializer.Deserialize<PluginManifest>(json, Opts)!;

        Assert.Equal("com.acme.min", m.Id);
        Assert.Null(m.EntryType);
        Assert.Empty(m.Capabilities);
        Assert.Equal("", m.Author);
        Assert.Equal(1, m.SchemaVersion);
    }

    [Fact]
    public void Deserialize_UnknownFields_AreIgnored()
    {
        var json = """{ "id": "x", "entryAssembly": "x.dll", "futureField": 42 }""";

        var m = JsonSerializer.Deserialize<PluginManifest>(json, Opts)!;

        Assert.Equal("x", m.Id);
    }
}
