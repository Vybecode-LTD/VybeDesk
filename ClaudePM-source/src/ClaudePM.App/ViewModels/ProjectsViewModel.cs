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
    private readonly IClaudeCodeLauncher _launcher;
    private readonly IProjectImportService _importer;

    public override string Title => "Projects";
    public override string Glyph => "\U0001F4C2"; // 📂
    public override string Description =>
        "Register the folders the AI agent and document scans operate on.";

    public ObservableCollection<Project> Projects { get; } = new();
    public IReadOnlyList<ProjectStatus> Statuses => StatusValues;

    /// <summary>
    /// Model dropdown items for the per-project override: the "(Use global
    /// default)" sentinel first, then the same curated model list the Settings
    /// dropdown uses (see <see cref="ModelsCatalog"/>). Picking the sentinel
    /// stores <c>null</c> on the Project so AnthropicChatService falls back
    /// to the global setting.
    /// </summary>
    public IReadOnlyList<ModelOption> AvailableModels { get; } =
        new[] { ModelsCatalog.UseGlobalDefault }
            .Concat(ModelsCatalog.All)
            .ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private Project? _selectedProject;

    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editDescription = "";
    [ObservableProperty] private string _editFolderPath = "";
    [ObservableProperty] private ProjectStatus _editStatus = ProjectStatus.Active;
    // M4 #16 per-project overrides. Empty string in the editor maps to null
    // on the Project (= "fall back to the global Settings value").
    [ObservableProperty] private string _editModel = "";
    [ObservableProperty] private string _editDefaultOutputPath = "";
    // M5 #17 enhancement: per-project logo path shown on the Home dashboard
    // card. Empty string in the editor maps to null on the Project (= no
    // logo; the Home card renders the project glyph as fallback).
    [ObservableProperty] private string _editLogoPath = "";

    /// <summary>
    /// Dropdown selection for the model picker. Two-way bound; changes flow
    /// into <see cref="EditModel"/> (and vice versa, so typing a custom ID
    /// into the freeform TextBox de-selects the dropdown if the ID isn't
    /// in the catalog).
    /// </summary>
    [ObservableProperty] private ModelOption? _selectedModelOption = ModelsCatalog.UseGlobalDefault;

    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;
    public bool HasSelection => SelectedProject is not null;

    public ProjectsViewModel(
        IProjectStore store, IFilePickerService picker, IClaudeCodeLauncher launcher,
        IProjectImportService importer)
    {
        _store = store;
        _picker = picker;
        _launcher = launcher;
        _importer = importer;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task OpenInClaudeCodeAsync()
    {
        if (SelectedProject is null) return;
        var result = await _launcher.LaunchAsync(EditFolderPath.Trim());
        StatusMessage = result.Message;
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
            EditModel = "";
            EditDefaultOutputPath = "";
            EditLogoPath = "";
            SelectedModelOption = ModelsCatalog.UseGlobalDefault;
            return;
        }
        EditName = value.Name;
        EditDescription = value.Description;
        EditFolderPath = value.FolderPath;
        EditStatus = value.Status;
        // null Model/DefaultOutputPath means "use the global default"; surface
        // that as an empty edit field so the watermark hints kick in.
        EditModel = value.Model ?? "";
        EditDefaultOutputPath = value.DefaultOutputPath ?? "";
        EditLogoPath = value.LogoPath ?? "";
        SyncModelOptionFromEditModel();
    }

    /// <summary>
    /// Keep <see cref="SelectedModelOption"/> in sync with the freeform
    /// <see cref="EditModel"/>. Pick the matching catalog entry if the ID
    /// matches; otherwise fall back to the sentinel (empty / unrecognised
    /// custom ID — the freeform TextBox still holds the actual value).
    /// </summary>
    private void SyncModelOptionFromEditModel()
    {
        var match = ModelsCatalog.All.FirstOrDefault(m => m.Id == EditModel);
        SelectedModelOption = match
            ?? (string.IsNullOrWhiteSpace(EditModel)
                ? ModelsCatalog.UseGlobalDefault
                : null);
    }

    partial void OnSelectedModelOptionChanged(ModelOption? value)
    {
        // Dropdown → EditModel. Sentinel ("(Use global default)") writes
        // empty string so the watermark shows and Save persists null.
        if (value is null) return;
        if (value.Id != EditModel) EditModel = value.Id;
    }

    partial void OnEditModelChanged(string value)
    {
        // Freeform TextBox → dropdown. If the user types a catalog ID it
        // syncs the dropdown selection; if they type a custom ID the
        // dropdown drops to null (no match) but EditModel still rules.
        SyncModelOptionFromEditModel();
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

    /// <summary>
    /// M4 #14 — point at an existing folder, ingest its CLAUDE.md as the
    /// Description, seed LastActivity from <c>git log -1</c>, and pull
    /// <c>.claude/commands/*.md</c> into the Prompt library tagged with the
    /// project name. The <c>.claude/skills/</c> half is parked until the
    /// Module 5 rewrite (M6).
    /// </summary>
    [RelayCommand]
    private async Task ImportExistingAsync()
    {
        if (IsBusy) return;
        var folder = await _picker.PickFolderAsync(
            title: "Pick an existing project folder to import");
        if (folder is null) return;

        IsBusy = true;
        try
        {
            var result = await _importer.ImportFromFolderAsync(folder);
            if (!result.Success)
            {
                StatusMessage = "Import failed: " + result.Message;
                return;
            }
            await LoadAsync();
            SelectedProject = result.Project is not null
                ? Projects.FirstOrDefault(p => p.Id == result.Project.Id)
                : null;
            var promptBits = result.PromptsImported > 0
                ? " · " + result.PromptsImported + " prompt(s) imported"
                : "";
            var dupeBits = result.PromptsSkippedDuplicate > 0
                ? " · " + result.PromptsSkippedDuplicate + " duplicate prompt(s) skipped"
                : "";
            var gitBits = result.HadGitTimestamp ? " · git-dated" : "";
            StatusMessage = "Imported '" + result.Project!.Name + "'"
                + promptBits + dupeBits + gitBits + ".";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var picked = await _picker.PickFolderAsync(
            title: "Pick the project folder",
            startLocation: EditFolderPath);
        if (picked is not null) EditFolderPath = picked;
    }

    /// <summary>
    /// M4 #16: pick the per-project default output folder (used as the
    /// starting point for handoff packages, exports, etc., generated from
    /// this project's context). Leaving the field blank falls back to the
    /// global Settings.OutputPath at use time.
    /// </summary>
    [RelayCommand]
    private async Task BrowseDefaultOutputPathAsync()
    {
        var picked = await _picker.PickFolderAsync(
            title: "Pick the default output folder for this project",
            startLocation: EditDefaultOutputPath);
        if (picked is not null) EditDefaultOutputPath = picked;
    }

    /// <summary>
    /// M5 #17 enhancement: pick the per-project logo image shown on the Home
    /// dashboard card. Leaving the field blank means "no logo — render the
    /// project glyph as fallback".
    /// </summary>
    [RelayCommand]
    private async Task BrowseLogoPathAsync()
    {
        var picked = await _picker.PickFileAsync(
            title: "Pick a logo image (PNG / JPG / SVG / ICO)");
        if (picked is not null) EditLogoPath = picked;
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
            // M4 #16: blank editor field → null on the Project (= use global
            // default). The AnthropicChatService and any output-path consumer
            // does the same null-or-blank → fallback check at read time.
            SelectedProject.Model = string.IsNullOrWhiteSpace(EditModel)
                ? null
                : EditModel.Trim();
            SelectedProject.DefaultOutputPath = string.IsNullOrWhiteSpace(EditDefaultOutputPath)
                ? null
                : EditDefaultOutputPath.Trim();
            SelectedProject.LogoPath = string.IsNullOrWhiteSpace(EditLogoPath)
                ? null
                : EditLogoPath.Trim();
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
