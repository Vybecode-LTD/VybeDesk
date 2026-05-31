using Microsoft.Extensions.DependencyInjection;
using VybeDesk.Plugin;

namespace VybePlugin;

/// <summary>The plugin entry point. The host discovers this, registers the page, and adds it to the sidebar.</summary>
public sealed class VybePluginModule : IVybeModule
{
    public ModuleManifest Manifest { get; } = new()
    {
        Id = "PLUGIN_ID",
        Name = "PLUGIN_DISPLAY_NAME",
        Version = "1.0.0",
        Author = "",
        Description = "A VybeDesk plugin.",
        // Declare capabilities for user disclosure, e.g.:
        // Capabilities = new[] { PluginCapabilities.Network },
    };

    public void ConfigureServices(IServiceCollection services)
        => services.AddSingleton<VybePluginViewModel>();

    public IEnumerable<PageViewModel> GetPages(IServiceProvider services)
        => new PageViewModel[] { services.GetRequiredService<VybePluginViewModel>() };
}
