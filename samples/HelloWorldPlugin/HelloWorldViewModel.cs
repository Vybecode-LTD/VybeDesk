using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VybeDesk.Plugin;

namespace HelloWorldPlugin;

/// <summary>
/// A sidebar page contributed entirely by the plugin. Derives from the SDK's
/// <see cref="PageViewModel"/>, so it gets a sidebar entry (Title + Glyph) and
/// the content area for free — exactly like a built-in module.
/// </summary>
public sealed partial class HelloWorldViewModel : PageViewModel
{
    public override string Title => "Hello";
    public override string Glyph => "\U0001F44B"; // 👋
    public override string Description => "A sample page contributed by the Hello World plugin.";

    [ObservableProperty] private string _greeting = "Hello from a VybeDesk plugin!";
    [ObservableProperty] private int _waves;

    [RelayCommand]
    private void Wave()
    {
        Waves++;
        Greeting = $"\U0001F44B Waved {Waves} time{(Waves == 1 ? "" : "s")} — this page runs from its own AssemblyLoadContext.";
    }
}
