using System.Collections.ObjectModel;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Project management — the central object Modules 1, 3, and 4 operate on.
/// Full CRUD over <see cref="IProjectStore"/>. The store's Changed event
/// keeps the Documentation project picker and Notebook scoped roots in sync.
/// </summary>
public sealed partial class ProjectsViewModel : PageViewModel
{
    private static readonly ProjectStatus[] StatusValues =
        (ProjectStatus[])Enum.GetValues(typeof(ProjectStatus));

    private readonly IProjectStore _store;
    private readonly IFilePickerService _picker;

    public override string Title => "Projects";
    public override string Glyph => "\U0001F4C2"; // 📂
    public override string Description =>
        "Register the folders the AI agent and document scans operate on.";

    public ObservableCollection<Project> Projects { get; } = new();
    public IReadOnlyList<ProjectStatus> Statuses => StatusValues;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private Project? _selectedProject;

    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editDescription = "";
    [ObservableProperty] private string _editFolderPath = "";
    [ObservableProperty] private ProjectStatus _editStatus = ProjectStatus.Active;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;
    public bool HasSelection => SelectedProject is not null;

    public ProjectsViewModel(IProjectStore store, IFilePickerService picker)
    {
        _store = store;
        _picker = picker;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var keepId = SelectedProject?.Id;
        var all = await _store.GetAllAsync();
        Projects.Clear();
        foreach (var p in all) Projects.Add(p);
        SelectedProject = keepId is not null
            ? Projects.FirstOrDefault(p => p.Id == keepId)
            : null;
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        if (value is null)
        {
            EditName = "";
            EditDescription = "";
            EditFolderPath = "";
            EditStatus = ProjectStatus.Active;
            return;
        }
        EditName = value.Name;
        EditDescription = value.Description;
        EditFolderPath = value.FolderPath;
        EditStatus = value.Status;
    }

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        var project = new Project { Name = "New project", Status = ProjectStatus.Active };
        await _store.AddAsync(project);
        await LoadAsync();
        SelectedProject = Projects.FirstOrDefault(p => p.Id == project.Id);
        StatusMessage = "New project created — set its folder path and Save.";
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var picked = await _picker.PickFolderAsync(
            title: "Pick the project folder",
            startLocation: EditFolderPath);
        if (picked is not null) EditFolderPath = picked;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedProject is null) return;
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            SelectedProject.Name = string.IsNullOrWhiteSpace(EditName)
                ? "Untitled project"
                : EditName.Trim();
            SelectedProject.Description = EditDescription.Trim();
            SelectedProject.FolderPath = EditFolderPath.Trim();
            SelectedProject.Status = EditStatus;
            SelectedProject.LastActivity = DateTimeOffset.Now;

            await _store.UpdateAsync(SelectedProject);
            await LoadAsync();
            StatusMessage = "Saved.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedProject is null) return;
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            await _store.RemoveAsync(SelectedProject.Id);
            SelectedProject = null;
            await LoadAsync();
            StatusMessage = "Project deleted.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
