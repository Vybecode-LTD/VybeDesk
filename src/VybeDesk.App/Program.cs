using System.Reflection;
using System.Runtime.Versioning;
using Avalonia;
using VybeDesk.App.Modules;
using VybeDesk.App.Services;
using VybeDesk.App.ViewModels;
using VybeDesk.Core.Services;
using VybeDesk.Services.Agent;
using VybeDesk.Services.Ai;
using VybeDesk.Services.Docs;
using VybeDesk.Services.Import;
using VybeDesk.Services.Plugins;
using VybeDesk.Services.ProjectHealth;
using VybeDesk.Services.Security;
using VybeDesk.Services.Session;
using VybeDesk.Services.Settings;
using VybeDesk.Services.Skills;
using VybeDesk.Services.Storage;
using VybeDesk.Services.Testing;
using VybeDesk.Services.Vision;
using Microsoft.Extensions.DependencyInjection;

namespace VybeDesk.App;

[SupportedOSPlatform("windows")]
internal static class Program
{
    /// <summary>The composition root. Resolved from App.axaml.cs only.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        var sc = new ServiceCollection();
        ConfigureServices(sc);
        Services = sc.BuildServiceProvider();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            (Services as IDisposable)?.Dispose();
        }
    }

    private static void ConfigureServices(IServiceCollection s)
    {
        // Infrastructure
        s.AddSingleton<Database>();
        s.AddSingleton<ISecureKeyStore, DpapiKeyStore>();
        s.AddSingleton<ISettingsService, JsonSettingsService>();
        s.AddSingleton<IProjectStore, SqliteProjectStore>();
        s.AddSingleton<IPromptStore, SqlitePromptStore>();
        s.AddSingleton<INoteStore, SqliteNoteStore>();
        s.AddSingleton<IBugStore, SqliteBugStore>();
        s.AddSingleton<ITestingPlanStore, SqliteTestingPlanStore>();
        s.AddSingleton<ITestingFrameworkCatalog, TestingFrameworkCatalog>();
        s.AddSingleton<IBugFixedNotifier, BugFixedNotifier>();
        s.AddSingleton<INotebookOpener, NotebookOpener>();
        s.AddSingleton<IHomeNavigator, HomeNavigator>();
        // M4 #16: tracks the currently-focused project across modules so
        // AnthropicChatService can resolve a per-project Model override.
        s.AddSingleton<IActiveProjectContext, ActiveProjectContext>();
        s.AddSingleton<IVisionStore, SqliteVisionStore>();
        s.AddSingleton<IAuditHistoryStore, SqliteAuditHistoryStore>();
        s.AddSingleton<IAgentActionLogStore, SqliteAgentActionLogStore>();
        s.AddSingleton<IAiCallStore, SqliteAiCallStore>();
        s.AddSingleton<IVisionAuditService, VisionAuditService>();
        s.AddSingleton<IAiService, AnthropicChatService>();
        s.AddSingleton<IDocReconciliationService, DocReconciliationService>();
        s.AddSingleton<IAgentActionService, AgentActionService>();
        s.AddSingleton<ISkillLibraryService, SkillLibraryService>();
        s.AddSingleton<ISkillBuilderService, SkillBuilderService>();
        s.AddSingleton<ISessionBuilderService, SessionBuilderService>();
        s.AddSingleton<IProjectImportService, ProjectImportService>();
        s.AddSingleton<IProjectHealthService, ProjectHealthService>();
        s.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        s.AddSingleton<IClaudeCodeLauncher, ClaudeCodeLauncher>();
        s.AddSingleton<IClipboardService, AvaloniaClipboardService>();

        // Page view models (one per module + Home + Projects + Settings)
        s.AddSingleton<HomeViewModel>();
        s.AddSingleton<ProjectsViewModel>();
        s.AddSingleton<DocumentationViewModel>();
        s.AddSingleton<PromptManagerViewModel>();
        s.AddSingleton<SessionBuilderViewModel>();
        s.AddSingleton<NotebookViewModel>();
        s.AddSingleton<BugTrackerViewModel>();
        s.AddSingleton<TestingManagerViewModel>();
        s.AddSingleton<VisionAuditViewModel>();
        // Skill area — Section hosts Manager + Builder sub-pages. The
        // Section's constructor takes an optional PageViewModel builder; we
        // resolve SkillBuilderViewModel and pass it in via a factory
        // registration so the section's in-pane toggle lights up the
        // Builder tab automatically (Phase 2 of the Skills rebuild).
        s.AddSingleton<SkillManagerViewModel>();
        s.AddSingleton<SkillBuilderViewModel>();
        s.AddSingleton<SkillSectionViewModel>(sp => new SkillSectionViewModel(
            sp.GetRequiredService<SkillManagerViewModel>(),
            sp.GetRequiredService<SkillBuilderViewModel>()));
        s.AddSingleton<SettingsViewModel>();
        // Settings is a group node hosting General (the existing settings page,
        // unchanged) + Plugins, mirroring the Skills section. SettingsViewModel
        // simply becomes the section's first child.
        s.AddSingleton<PluginsViewModel>();
        s.AddSingleton<SettingsSectionViewModel>(sp => new SettingsSectionViewModel(
            sp.GetRequiredService<SettingsViewModel>(),
            sp.GetRequiredService<PluginsViewModel>()));

        // ===== Plugins =====
        // Register the host facade plugins can inject, then discover + load
        // every enabled, host-compatible plugin from
        // %LOCALAPPDATA%\VybeDesk\plugins. Each loaded plugin's IVybeModule is
        // registered as a singleton so the module catalog (below) collects its
        // pages alongside the built-ins. Discovery runs HERE, at composition
        // time, because plugins must contribute their service registrations
        // before the provider is built.
        s.AddSingleton<IModuleHost, ModuleHost>();
        var hostVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        var pluginRegistry = new PluginLoader(hostVersion, PluginState.LoadDisabled()).LoadInto(s);
        s.AddSingleton<IPluginRegistry>(pluginRegistry);

        // ===== Module catalog — the ordered sidebar set =====
        // Built-in modules in their curated order, then any pages contributed
        // by loaded plugins (IVybeModule registrations — see PluginLoader),
        // then Settings pinned last. MainWindowViewModel consumes this instead
        // of a hard-coded constructor list, so plugins extend the sidebar
        // through exactly the same path the built-ins use.
        s.AddSingleton<IModuleCatalog>(sp =>
        {
            var pages = new List<PageViewModel>
            {
                sp.GetRequiredService<HomeViewModel>(),
                sp.GetRequiredService<ProjectsViewModel>(),
                sp.GetRequiredService<DocumentationViewModel>(),
                sp.GetRequiredService<PromptManagerViewModel>(),
                sp.GetRequiredService<SessionBuilderViewModel>(),
                sp.GetRequiredService<NotebookViewModel>(),
                sp.GetRequiredService<SkillSectionViewModel>(),
                sp.GetRequiredService<BugTrackerViewModel>(),
                sp.GetRequiredService<TestingManagerViewModel>(),
                sp.GetRequiredService<VisionAuditViewModel>(),
            };

            // Plugin-contributed pages slot in after the built-in modules and
            // before Settings. No-op until the loader registers IVybeModules.
            foreach (var module in sp.GetServices<IVybeModule>())
                pages.AddRange(module.GetPages(sp));

            pages.Add(sp.GetRequiredService<SettingsSectionViewModel>());
            return new ModuleCatalog(pages);
        });

        // Shell
        s.AddSingleton<MainWindowViewModel>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
