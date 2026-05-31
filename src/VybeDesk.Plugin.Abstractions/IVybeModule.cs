using Microsoft.Extensions.DependencyInjection;

namespace VybeDesk.Plugin;

/// <summary>
/// The entry point every VybeDesk plugin implements. The host discovers one
/// <see cref="IVybeModule"/> per plugin assembly, instantiates it through its
/// public parameterless constructor, then drives it through three steps:
/// <list type="number">
///   <item>reads <see cref="Manifest"/> (already cross-checked against the
///         on-disk <c>plugin.json</c>),</item>
///   <item>calls <see cref="ConfigureServices"/> once, letting the module add
///         its services + view models to the shared host container,</item>
///   <item>after the provider is built, calls <see cref="GetPages"/> to collect
///         the sidebar page(s) the module contributes.</item>
/// </list>
/// Implementations MUST expose a public parameterless constructor and should do
/// their real work in <see cref="ConfigureServices"/>/<see cref="GetPages"/>,
/// not the constructor (which runs before any host service is available).
/// </summary>
public interface IVybeModule
{
    /// <summary>This module's self-description (id, name, version, capabilities).</summary>
    ModuleManifest Manifest { get; }

    /// <summary>
    /// Register the services and view models this module needs. Called once,
    /// before the host builds its <see cref="IServiceProvider"/>. Register your
    /// <see cref="PageViewModel"/> implementations here (singletons are typical).
    /// Host services — <c>IAiService</c>, <c>IProjectStore</c>,
    /// <c>IActiveProjectContext</c>, <c>IAgentActionService</c>,
    /// <c>IClipboardService</c>, <see cref="IModuleHost"/>, and more — are
    /// already registered and may be constructor-injected into your view models.
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Return the page(s) this module contributes to the sidebar, resolved from
    /// the fully-built <paramref name="services"/> provider. Each returned
    /// <see cref="PageViewModel"/> becomes a sidebar entry (with nested
    /// <see cref="PageViewModel.Children"/> rendered as a submenu). Called once,
    /// after <see cref="ConfigureServices"/>. Return an empty sequence for a
    /// headless module that only registers services.
    /// </summary>
    IEnumerable<PageViewModel> GetPages(IServiceProvider services);
}
