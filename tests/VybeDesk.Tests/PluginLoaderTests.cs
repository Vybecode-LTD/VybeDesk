using Microsoft.Extensions.DependencyInjection;
using VybeDesk.Services.Plugins;
using Xunit;

namespace VybeDesk.Tests;

/// <summary>
/// Exercises the loader's discovery + compatibility decision tree against a temp
/// plugins directory. These cases all resolve BEFORE any assembly is loaded, so
/// they need no real plugin DLL — the successful end-to-end load is covered by
/// the manual smoke test with samples/HelloWorldPlugin.
/// </summary>
public class PluginLoaderTests : IDisposable
{
    private readonly string _root;

    public PluginLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "vybedesk-test-plugins-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void MakePlugin(string folder, string? manifestJson)
    {
        var dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        if (manifestJson is not null)
            File.WriteAllText(Path.Combine(dir, "plugin.json"), manifestJson);
    }

    private PluginRegistry Load(Version host, params string[] disabled)
        => new PluginLoader(host, disabled, _root).LoadInto(new ServiceCollection());

    [Fact]
    public void FolderWithoutManifest_IsSkipped()
    {
        MakePlugin("notaplugin", manifestJson: null);

        Assert.Empty(Load(new Version(1, 1, 0)).Plugins);
    }

    [Fact]
    public void InvalidJson_IsFailed()
    {
        MakePlugin("bad", "{ this is not valid json ");

        var info = Assert.Single(Load(new Version(1, 1, 0)).Plugins);
        Assert.Equal(PluginStatus.Failed, info.Status);
    }

    [Fact]
    public void MissingId_IsFailed()
    {
        MakePlugin("noid", """{ "entryAssembly": "x.dll" }""");

        var info = Assert.Single(Load(new Version(1, 1, 0)).Plugins);
        Assert.Equal(PluginStatus.Failed, info.Status);
    }

    [Fact]
    public void DisabledId_IsReportedDisabled_AndNotLoaded()
    {
        MakePlugin("dis", """{ "id": "com.acme.dis", "entryAssembly": "x.dll" }""");

        var info = Assert.Single(Load(new Version(1, 1, 0), "com.acme.dis").Plugins);
        Assert.Equal(PluginStatus.Disabled, info.Status);
    }

    [Fact]
    public void MinHostVersionAboveHost_IsIncompatible()
    {
        MakePlugin("future", """{ "id": "com.acme.future", "entryAssembly": "x.dll", "minHostVersion": "2.0.0" }""");

        var info = Assert.Single(Load(new Version(1, 1, 0)).Plugins);
        Assert.Equal(PluginStatus.Incompatible, info.Status);
        Assert.Contains("2.0.0", info.Error);
    }

    [Fact]
    public void MaxHostVersionBelowHost_IsIncompatible()
    {
        MakePlugin("old", """{ "id": "com.acme.old", "entryAssembly": "x.dll", "maxHostVersion": "1.0.0" }""");

        var info = Assert.Single(Load(new Version(1, 1, 0)).Plugins);
        Assert.Equal(PluginStatus.Incompatible, info.Status);
    }

    [Fact]
    public void CompatibleButMissingAssembly_IsFailed()
    {
        MakePlugin("noasm", """{ "id": "com.acme.noasm", "entryAssembly": "DoesNotExist.dll" }""");

        var info = Assert.Single(Load(new Version(1, 1, 0)).Plugins);
        Assert.Equal(PluginStatus.Failed, info.Status);
        Assert.Contains("Entry assembly not found", info.Error);
    }

    [Fact]
    public void VersionInRange_ClearsTheGate_ThenFailsOnMissingAssembly()
    {
        // min <= host <= max, so it passes the compat gate and proceeds to load —
        // failing only because the assembly is absent. This proves the gate let
        // an in-range version through rather than rejecting it as incompatible.
        MakePlugin("inrange",
            """{ "id": "com.acme.inrange", "entryAssembly": "Nope.dll", "minHostVersion": "1.0.0", "maxHostVersion": "2.0.0" }""");

        var info = Assert.Single(Load(new Version(1, 1, 0)).Plugins);
        Assert.Equal(PluginStatus.Failed, info.Status);
    }

    [Fact]
    public void MultiplePlugins_AreAllReported()
    {
        MakePlugin("a", """{ "id": "a", "entryAssembly": "a.dll", "minHostVersion": "9.0.0" }"""); // incompatible
        MakePlugin("b", """{ "id": "b", "entryAssembly": "b.dll" }""");                              // failed (no asm)
        MakePlugin("c", manifestJson: null);                                                          // skipped

        var plugins = Load(new Version(1, 1, 0)).Plugins;
        Assert.Equal(2, plugins.Count);
    }
}
