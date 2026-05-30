using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using VybeDesk.Tests.AppSmoke;

// Registers the Avalonia headless application used by every [AvaloniaFact] /
// [AvaloniaTheory] test in this assembly. Regular [Fact] tests are unaffected.
[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]

namespace VybeDesk.Tests.AppSmoke;

/// <summary>
/// Minimal headless Avalonia application for layout-regression tests. Loads only
/// the FluentTheme — that's what gives ContentControl / ScrollViewer their
/// control templates, which is the whole point of the scroll-bounding tests
/// (the bug was rooted in the Fluent ContentControl template defaulting
/// VerticalContentAlignment to Top). We deliberately do NOT use the real
/// VybeDesk.App.App: it is [SupportedOSPlatform("windows")] and its
/// OnFrameworkInitializationCompleted resolves Program.Services (DI), neither
/// of which belongs in a headless layout unit test.
/// </summary>
public sealed class HeadlessTestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
