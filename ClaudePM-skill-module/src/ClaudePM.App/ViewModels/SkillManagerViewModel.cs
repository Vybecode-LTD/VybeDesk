using System.Collections.ObjectModel;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Skill Manager — browses .skill files, shows the full skill text, lists each
/// skill's supporting resource files, and shows the contents of a selected
/// resource in place of the skill text. Reached via the "Skill" sidebar entry's
/// "Skill Manager" submenu option.
/// </summary>
public sealed partial class SkillManagerViewModel : PageViewModel
{
    private readonly ISkillLibraryService _service;
    private readonly List<SkillFile> _all = new();

    public override string Title => "Skill Manager";
    public override string Glyph => "\U0001F9E9";
    public override string Description =>
        "Browse, inspect, and edit your skills and their supporting files.";

    /// <summary>The skills shown in the left-hand list.</summary>
    public ObservableCollection<SkillFile> Skills { get; } = new();

    /// <summary>Validation findings for the currently selected skill.</summary>
    public ObservableCollection<Finding> Issues { get; } = new();

    /// <summary>
    /// The resource files belonging to the selected skill. Bound to the
    /// fixed-size resource ListBox in the lower-right area.
    /// </summary>
    public ObservableCollection<SkillResource> Resources { get; } = new();

    [ObservableProperty] private SkillFile? _selectedSkill;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewingResource))]
    [NotifyPropertyChangedFor(nameof(ViewerHeading))]
    private SkillResource? _selectedResource;

    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _statusMessage = "Enter a folder path, then Scan.";

    /// <summary>
    /// The text shown in the large viewer box. Holds the full skill text when no
    /// resource is selected, or the selected resource's contents when one is.
    /// </summary>
    [ObservableProperty] private string _viewerContent = "";

    /// <summary>Editable fields — only meaningful while a skill is selected.</summary>
    [ObservableProperty] private string _editName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionBudget))]
    [NotifyPropertyChangedFor(nameof(DescriptionOverBudget))]
    private string _editDescription = "";

    [ObservableProperty] private string _editBody = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;
    public bool HasSelection => SelectedSkill is not null;
    public bool HasResults => Skills.Count > 0;

    /// <summary>True when the viewer is currently showing a resource, not the skill.</summary>
    public bool IsViewingResource => SelectedResource is not null;

    /// <summary>Heading shown above the viewer box, reflecting what it contains.</summary>
    public string ViewerHeading => SelectedResource is not null
        ? "Resource: " + SelectedResource.DisplayName
        : "Skill file";

    public string DescriptionBudget => EditDescription.Length + " / 1024";
    public bool DescriptionOverBudget => EditDescription.Length >= 1024;

    public SkillManagerViewModel(ISkillLibraryService service) => _service = service;

    [RelayCommand]
    private async Task ScanAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            StatusMessage = "Enter a folder path first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Scanning for .skill files\u2026";
        try
        {
            var found = await _service.ScanAsync(FolderPath, ct);

            // For each skill, discover the supporting files in its own folder.
            foreach (var skill in found)
                _service.PopulateResources(skill);

            _all.Clear();
            _all.AddRange(found);

            Skills.Clear();
            foreach (var s in _all) Skills.Add(s);
            SelectedSkill = null;

            OnPropertyChanged(nameof(HasResults));
            StatusMessage = _all.Count + " skill(s) found.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Scan failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// When the selected skill changes, reset the editor fields, refresh the
    /// resource list, clear any resource selection, and show the skill text in
    /// the viewer.
    /// </summary>
    partial void OnSelectedSkillChanged(SkillFile? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        Issues.Clear();
        Resources.Clear();

        // Selecting a different skill must drop any resource selection, so the
        // viewer falls back to showing the skill itself rather than a stale
        // resource from the previously selected skill.
        SelectedResource = null;

        if (value is null)
        {
            EditName = EditDescription = EditBody = "";
            ViewerContent = "";
            return;
        }

        EditName = value.Name;
        EditDescription = value.Description;
        EditBody = value.Body;

        foreach (var r in value.Resources) Resources.Add(r);
        foreach (var f in _service.Validate(value)) Issues.Add(f);

        ShowSkillInViewer();
    }

    /// <summary>
    /// When a resource is selected, load its contents into the viewer. When the
    /// resource selection is cleared, fall back to showing the skill text.
    /// </summary>
    partial void OnSelectedResourceChanged(SkillResource? value)
    {
        if (value is null)
        {
            ShowSkillInViewer();
            return;
        }
        _ = LoadResourceIntoViewerAsync(value);
    }

    private async Task LoadResourceIntoViewerAsync(SkillResource resource)
    {
        ViewerContent = "Loading\u2026";
        try
        {
            ViewerContent = await _service.ReadResourceAsync(resource);
            StatusMessage = "Showing resource: " + resource.DisplayName;
        }
        catch (Exception ex)
        {
            ViewerContent = "[Could not load resource: " + ex.Message + "]";
        }
    }

    private void ShowSkillInViewer()
    {
        ViewerContent = SelectedSkill is null
            ? ""
            : _service.Serialize(SelectedSkill);
    }

    /// <summary>Clears the resource selection, returning the viewer to the skill.</summary>
    [RelayCommand]
    private void ShowSkill() => SelectedResource = null;

    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (SelectedSkill is null || IsBusy) return;

        SelectedSkill.Name = EditName.Trim();
        SelectedSkill.Description = EditDescription.Trim();
        SelectedSkill.Body = EditBody;
        SelectedSkill.HasFrontMatter = true;

        IsBusy = true;
        try
        {
            await _service.SaveAsync(SelectedSkill, ct);

            Issues.Clear();
            foreach (var f in _service.Validate(SelectedSkill)) Issues.Add(f);

            // Reflect the saved edits in the viewer, but only if the user is
            // currently looking at the skill rather than at a resource.
            if (!IsViewingResource) ShowSkillInViewer();

            StatusMessage = "Saved " + SelectedSkill.FileName + ".";
        }
        catch (Exception ex)
        {
            StatusMessage = "Save failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
