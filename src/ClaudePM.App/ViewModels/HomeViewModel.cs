using System.Collections.ObjectModel;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.App.ViewModels;

public sealed partial class HomeViewModel : PageViewModel
{
    private readonly IProjectStore _projects;

    public override string Title => "Home";
    public override string Glyph => "\U0001F3E0";
    public override string Description => "Your registered projects at a glance.";

    public ObservableCollection<Project> Projects { get; } = new();

    public HomeViewModel(IProjectStore projects)
    {
        _projects = projects;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var all = await _projects.GetAllAsync();
        Projects.Clear();
        foreach (var p in all) Projects.Add(p);
    }
}
