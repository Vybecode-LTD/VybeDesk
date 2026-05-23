using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudePM.App.ViewModels;

/// <summary>A single {{placeholder}} the user fills when using a prompt template.</summary>
public sealed partial class TemplateVariable : ObservableObject
{
    public TemplateVariable(string name) => Name = name;

    public string Name { get; }

    [ObservableProperty]
    private string _value = "";
}
