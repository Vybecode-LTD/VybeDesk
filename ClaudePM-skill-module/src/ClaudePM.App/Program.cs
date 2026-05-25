using Avalonia;
using ClaudePM.App.ViewModels;
using ClaudePM.Core.Services;
using ClaudePM.Services.Agent;
using ClaudePM.Services.Ai;
using ClaudePM.Services.Docs;
using ClaudePM.Services.Security;
using ClaudePM.Services.Session;
using ClaudePM.Services.Skills;
using ClaudePM.Services.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudePM.App;

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
        s.AddSingleton<IAiService, AnthropicChatService>();
        s.AddSingleton<IDocReconciliationService, DocReconciliationService>();
        s.AddSingleton<IAgentActionService, AgentActionService>();
        s.AddSingleton<ISkillLibraryService, SkillLibraryService>();
        s.AddSingleton<ISessionBuilderService, SessionBuilderService>();

        // Page view models (one per module + Home + Settings)
        s.AddSingleton<HomeViewModel>();
        s.AddSingleton<DocumentationViewModel>();
        s.AddSingleton<PromptManagerViewModel>();
        s.AddSingleton<SessionBuilderViewModel>();
        s.AddSingleton<NotebookViewModel>();
        s.AddSingleton<SettingsViewModel>();

        // Skill area — the rebuilt manager, plus the in-pane section container
        // that hosts it. The Skill Builder, once that module is built, is
        // registered here and passed into SkillSectionViewModel; until then the
        // section runs manager-only and the builder tab stays hidden.
        s.AddSingleton<SkillManagerViewModel>();
        s.AddSingleton<SkillSectionViewModel>();

        // Shell
        s.AddSingleton<MainWindowViewModel>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
