using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VybeDesk.Plugin;

namespace VybePlugin;

/// <summary>The page this plugin adds to the sidebar. Build your feature here.</summary>
public sealed partial class VybePluginViewModel : PageViewModel
{
    public override string Title => "PLUGIN_DISPLAY_NAME";
    public override string Glyph => "\U0001F9E9"; // 🧩 — pick your own sidebar icon
    public override string Description => "A VybeDesk plugin page.";

    [ObservableProperty] private string _message = "Hello from PLUGIN_DISPLAY_NAME!";

    [RelayCommand]
    private void DoSomething()
        => Message = "It works — now edit VybePluginViewModel + VybePluginView to build your feature.";
}
