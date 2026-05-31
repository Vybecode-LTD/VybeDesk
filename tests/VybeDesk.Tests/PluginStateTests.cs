using VybeDesk.Services.Plugins;
using Xunit;

namespace VybeDesk.Tests;

public class PluginStateTests : IDisposable
{
    private readonly string _dir;

    public PluginStateTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "vybedesk-test-pluginstate-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void LoadDisabled_NoFile_ReturnsEmpty()
        => Assert.Empty(PluginState.LoadDisabled(_dir));

    [Fact]
    public void SetEnabled_False_PersistsDisabled()
    {
        PluginState.SetEnabled("com.acme.todo", enabled: false, _dir);

        Assert.Contains("com.acme.todo", PluginState.LoadDisabled(_dir));
    }

    [Fact]
    public void SetEnabled_True_RemovesFromDisabled()
    {
        PluginState.SetEnabled("com.acme.todo", enabled: false, _dir);
        PluginState.SetEnabled("com.acme.todo", enabled: true, _dir);

        Assert.DoesNotContain("com.acme.todo", PluginState.LoadDisabled(_dir));
    }

    [Fact]
    public void Disabled_LookupIsCaseInsensitive()
    {
        PluginState.SetEnabled("Com.Acme.Todo", enabled: false, _dir);

        Assert.Contains("com.acme.todo", PluginState.LoadDisabled(_dir));
    }

    [Fact]
    public void SetEnabled_DisableTwice_IsIdempotent()
    {
        PluginState.SetEnabled("a", enabled: false, _dir);
        PluginState.SetEnabled("a", enabled: false, _dir);

        Assert.Single(PluginState.LoadDisabled(_dir));
    }
}
