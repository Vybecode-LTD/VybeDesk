using System.Runtime.Versioning;
using Avalonia;
using ClaudePM.App.Services;
using ClaudePM.App.ViewModels;
using ClaudePM.Core.Services;
using ClaudePM.Services.Agent;
using ClaudePM.Services.Ai;
using ClaudePM.Services.Docs;
using ClaudePM.Services.Security;
using ClaudePM.Services.Session;
using ClaudePM.Services.Skills;
using ClaudePM.Services.Storage;
using ClaudePM.Services.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudePM.App;

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
        s.AddSingleton<IAiService, AnthropicChatService>();
        s.AddSingleton<IDocReconciliationService, DocReconciliationService>();
        s.AddSingleton<IAgentActionService, AgentActionService>();
        s.AddSingleton<ISkillLibraryService, SkillLibraryService>();
        s.AddSingleton<ISessionBuilderService, SessionBuilderService>();
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
        // Skill area — the manager is the only sub-page today; the optional
        // builder is wired in when that module ships (Phase 2 of the v0.28
        // integration). SkillSectionViewModel hosts whichever sub-pages exist.
        s.AddSingleton<SkillManagerViewModel>();
        s.AddSingleton<SkillSectionViewModel>();
        s.AddSingleton<SettingsViewModel>();

        // Shell
        s.AddSingleton<MainWindowViewModel>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
