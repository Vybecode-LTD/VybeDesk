using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VybeDesk.App;

/// <summary>
/// Maps a FooViewModel to a FooView by naming convention.
///
/// Views are cached per VM instance (one view per VM object) so that
/// TwoWay bindings — particularly the ModuleHeader ComboBox chain
/// ComboBox ↔ PickerSelectedItem ↔ SelectedProject — survive
/// navigation away-and-back without emitting a spurious null that
/// wipes the module's project selection.
///
/// ConditionalWeakTable is used as the backing store: keys (VMs) are
/// held weakly, so if the DI container ever releases a VM the view is
/// also eligible for GC. In practice all module VMs are singletons
/// and live for the application lifetime.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private readonly ConditionalWeakTable<object, Control> _cache = new();

    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "null" };

        var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type is null) return new TextBlock { Text = "View not found: " + name };

        return _cache.GetValue(data, _ => (Control)Activator.CreateInstance(type)!);
    }

    public bool Match(object? data) => data is ObservableObject;
}
