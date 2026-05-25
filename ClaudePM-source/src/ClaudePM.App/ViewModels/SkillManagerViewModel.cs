using System.Collections.ObjectModel;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Skill Manager — browses folder-format skills (<c>&lt;name&gt;/SKILL.md</c>),
/// shows the full skill text, lists each skill's supporting resource files,
/// and shows the contents of a selected resource in place of the skill text.
/// Also supports Browse / Rename / Backup / Export and a global severity-
/// filtered findings view.
/// </summary>
public sealed partial class SkillManagerViewModel : PageViewModel
{
    private readonly ISkillLibraryService _service;
    private readonly IFilePickerService _picker;
    private readonly IClipboardService _clipboard;
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

    /// <summary>
    /// Findings of the currently-active filter severity (when the user has
    /// clicked a chip to enter the filter view). Spans every scanned skill.
    /// Each entry's <see cref="Finding.File"/> identifies the source skill.
    /// </summary>
    public ObservableCollection<Finding> FilteredFindings { get; } = new();

    /// <summary>All findings across every scanned skill — the source pool for filtering.</summary>
    private readonly List<Finding> _allFindings = new();

    /// <summary>
    /// Selection in the TreeView. Either a <see cref="SkillFile"/> (the
    /// user clicked a skill node) or a <see cref="SkillResource"/> (the
    /// user clicked one of a skill's nested resource files). The derived
    /// <see cref="SelectedSkill"/> and <see cref="SelectedResource"/>
    /// properties decode this into the two cases the rest of the VM uses.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSkill))]
    [NotifyPropertyChangedFor(nameof(SelectedResource))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(IsViewingResource))]
    [NotifyPropertyChangedFor(nameof(ViewerHeading))]
    private object? _selectedTreeItem;

    /// <summary>The currently-focused skill (parent of any selected resource).</summary>
    public SkillFile? SelectedSkill => SelectedTreeItem switch
    {
        SkillFile s    => s,
        SkillResource r => _all.FirstOrDefault(s => s.Resources.Contains(r)),
        _              => null,
    };

    /// <summary>The currently-viewing resource (null when the skill itself is selected).</summary>
    public SkillResource? SelectedResource => SelectedTreeItem as SkillResource;

    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _statusMessage = "Browse to a folder or paste a path, then Scan.";

    /// <summary>The text shown in the large viewer box.</summary>
    [ObservableProperty] private string _viewerContent = "";

    [ObservableProperty] private string _editName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionBudget))]
    [NotifyPropertyChangedFor(nameof(DescriptionOverBudget))]
    private string _editDescription = "";

    [ObservableProperty] private string _editBody = "";

    /// <summary>Target folder name for Rename. Pre-filled with the current folder name.</summary>
    [ObservableProperty] private string _renameTarget = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    // --- severity filter state ---------------------------------------------

    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private int _infoCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterViewOpen))]
    [NotifyPropertyChangedFor(nameof(IsEditorViewVisible))]
    [NotifyPropertyChangedFor(nameof(FilterViewHeading))]
    private FindingSeverity? _filterSeverity;

    /// <summary>True when the global findings filter is active.</summary>
    public bool IsFilterViewOpen => FilterSeverity is not null;

    /// <summary>The editor / viewer / resources pane is hidden while the filter view is open.</summary>
    public bool IsEditorViewVisible => FilterSeverity is null;

    public string FilterViewHeading => FilterSeverity switch
    {
        FindingSeverity.Critical => "Critical findings across every scanned skill",
        FindingSeverity.Warning  => "Warnings across every scanned skill",
        FindingSeverity.Info     => "Info-level findings across every scanned skill",
        _                        => "",
    };

    // --- derived flags ------------------------------------------------------

    public bool IsNotBusy => !IsBusy;
    public bool HasSelection => SelectedSkill is not null;
    public bool HasResults => Skills.Count > 0;
    public bool HasIssues => Issues.Count > 0;

    public bool IsViewingResource => SelectedResource is not null;

    public string ViewerHeading => SelectedResource is not null
        ? "Resource: " + SelectedResource.DisplayName
        : "Skill file";

    public string DescriptionBudget => EditDescription.Length + " / 1024";
    public bool DescriptionOverBudget => EditDescription.Length >= 1024;

    public SkillManagerViewModel(
        ISkillLibraryService service,
        IFilePickerService picker,
        IClipboardService clipboard)
    {
        _service = service;
        _picker = picker;
        _clipboard = clipboard;
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var picked = await _picker.PickFolderAsync(
            title: "Pick the folder to scan for SKILL.md skills",
            startLocation: FolderPath);
        if (picked is not null) FolderPath = picked;
    }

    [RelayCommand]
    private async Task ScanAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            StatusMessage = "Pick a folder first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Scanning for SKILL.md folders…";
        try
        {
            var found = await _service.ScanAsync(FolderPath, ct);
            foreach (var skill in found)
                _service.PopulateResources(skill);

            _all.Clear();
            _all.AddRange(found);

            Skills.Clear();
            foreach (var s in _all) Skills.Add(s);
            SelectedTreeItem = null;

            RebuildAllFindings();

            OnPropertyChanged(nameof(HasResults));
            StatusMessage = _all.Count + " skill folder(s) found.";
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
    /// Recompute the pool of all findings across every scanned skill, plus
    /// per-severity counts shown in the chip badges. Called after Scan and
    /// after Save (since saving may resolve / introduce findings).
    /// </summary>
    private void RebuildAllFindings()
    {
        _allFindings.Clear();
        foreach (var skill in _all)
        {
            // Stamp each finding with the skill's folder name so the filter
            // view shows where each finding lives. Validate() sets
            // Finding.File to skill.FileName which is always "SKILL.md" for
            // folder-format skills — replace with the folder for clarity.
            foreach (var f in _service.Validate(skill))
                _allFindings.Add(f with { File = LocationLabel(skill) });
        }
        _allFindings.AddRange(_service.FindDuplicates(_all));

        CriticalCount = _allFindings.Count(f => f.Severity == FindingSeverity.Critical);
        WarningCount  = _allFindings.Count(f => f.Severity == FindingSeverity.Warning);
        InfoCount     = _allFindings.Count(f => f.Severity == FindingSeverity.Info);

        if (FilterSeverity is { } sev) ApplyFilter(sev);
    }

    private static string LocationLabel(SkillFile skill)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(skill.FullPath) ?? "");
        return string.IsNullOrEmpty(folder)
            ? (string.IsNullOrWhiteSpace(skill.Name) ? "(unnamed)" : skill.Name)
            : folder;
    }

    /// <summary>
    /// Drives both the editor (which always reflects the focused skill —
    /// the parent of any selected resource) and the viewer (which shows
    /// either the skill body or the selected resource's text). One handler
    /// because both come from the same TreeView selection now.
    /// </summary>
    partial void OnSelectedTreeItemChanged(object? value)
    {
        Issues.Clear();
        Resources.Clear();

        var skill = SelectedSkill;

        if (skill is null)
        {
            EditName = EditDescription = EditBody = RenameTarget = "";
            ViewerContent = "";
            OnPropertyChanged(nameof(HasIssues));
            return;
        }

        // Editor always reflects the focused skill, even when a resource is
        // what was clicked — so the user can edit the parent while viewing
        // one of its supporting files.
        EditName = skill.Name;
        EditDescription = skill.Description;
        EditBody = skill.Body;
        RenameTarget = Path.GetFileName(Path.GetDirectoryName(skill.FullPath) ?? "")
                       ?? skill.Name;

        foreach (var r in skill.Resources) Resources.Add(r);
        foreach (var f in _service.Validate(skill)) Issues.Add(f);
        OnPropertyChanged(nameof(HasIssues));

        // Viewer: resource content if a resource is selected, else skill body.
        if (value is SkillResource res)
            _ = LoadResourceIntoViewerAsync(res);
        else
            ShowSkillInViewer();
    }

    private async Task LoadResourceIntoViewerAsync(SkillResource resource)
    {
        ViewerContent = "Loading…";
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
        => ViewerContent = SelectedSkill is null ? "" : _service.Serialize(SelectedSkill);

    /// <summary>
    /// Returns the viewer to the skill body when the user is currently
    /// looking at a resource. Implemented by collapsing the TreeView
    /// selection up to the parent skill.
    /// </summary>
    [RelayCommand]
    private void ShowSkill()
    {
        if (SelectedSkill is not null)
            SelectedTreeItem = SelectedSkill;
    }

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
            OnPropertyChanged(nameof(HasIssues));
            RebuildAllFindings();
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

    [RelayCommand]
    private async Task BackupAsync()
    {
        if (SelectedSkill is null || IsBusy) return;
        var target = await _picker.PickFolderAsync(
            title: "Pick a destination folder for the backup",
            startLocation: FolderPath);
        if (target is null) return;

        IsBusy = true;
        try
        {
            var path = await _service.BackupAsync(SelectedSkill, target);
            StatusMessage = "Backed up to " + path;
        }
        catch (Exception ex)
        {
            StatusMessage = "Backup failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (SelectedSkill is null || IsBusy) return;
        var target = await _picker.PickFolderAsync(
            title: "Pick a destination folder for the exported skill",
            startLocation: FolderPath);
        if (target is null) return;

        IsBusy = true;
        try
        {
            var path = await _service.ExportAsync(SelectedSkill, target);
            StatusMessage = "Exported to " + path;
        }
        catch (Exception ex)
        {
            StatusMessage = "Export failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RenameAsync(CancellationToken ct)
    {
        if (SelectedSkill is null || IsBusy) return;
        var newName = (RenameTarget ?? "").Trim();
        if (string.IsNullOrEmpty(newName))
        {
            StatusMessage = "Enter the new folder name first.";
            return;
        }

        IsBusy = true;
        try
        {
            await _service.RenameAsync(SelectedSkill, newName, ct);
            // Display order keys on Name → rebuild list and re-select.
            var keepPath = SelectedSkill.FullPath;
            Skills.Clear();
            foreach (var s in _all.OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase))
                Skills.Add(s);
            SelectedTreeItem = Skills.FirstOrDefault(s => s.FullPath == keepPath);
            EditName = SelectedSkill?.Name ?? "";
            RebuildAllFindings();
            StatusMessage = "Renamed to " + newName + ".";
        }
        catch (Exception ex)
        {
            StatusMessage = "Rename failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- severity-filter commands ------------------------------------------

    [RelayCommand] private void FilterCritical() => ApplyFilter(FindingSeverity.Critical);
    [RelayCommand] private void FilterWarning() => ApplyFilter(FindingSeverity.Warning);
    [RelayCommand] private void FilterInfo() => ApplyFilter(FindingSeverity.Info);

    private void ApplyFilter(FindingSeverity severity)
    {
        FilterSeverity = severity;
        FilteredFindings.Clear();
        foreach (var f in _allFindings.Where(f => f.Severity == severity))
            FilteredFindings.Add(f);
        StatusMessage = FilteredFindings.Count + " " +
                        severity.ToString().ToLowerInvariant() + " finding(s).";
    }

    [RelayCommand]
    private void CloseFilter()
    {
        FilterSeverity = null;
        FilteredFindings.Clear();
        StatusMessage = "Closed filter view.";
    }

    /// <summary>Click a finding in the filter view → jump to the owning skill.</summary>
    [RelayCommand]
    private void NavigateToFindingSkill(Finding? finding)
    {
        if (finding is null) return;
        var match = _all.FirstOrDefault(s =>
            LocationLabel(s).Equals(finding.File, StringComparison.OrdinalIgnoreCase));
        if (match is null) return;
        FilterSeverity = null;
        FilteredFindings.Clear();
        SelectedTreeItem = match;
        StatusMessage = "Jumped to " + LocationLabel(match) + ".";
    }

    [RelayCommand]
    private async Task CopyFindingAsync(Finding? finding)
    {
        if (finding is null) return;
        var text = "[" + finding.Severity.ToString().ToUpperInvariant() + "] " +
                   finding.File + " (" + finding.Category + "): " + finding.Message;
        if (await _clipboard.SetTextAsync(text))
            StatusMessage = "Finding copied to clipboard.";
    }

    [RelayCommand]
    private async Task CopyAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (await _clipboard.SetTextAsync(text))
            StatusMessage = "Copied to clipboard.";
    }
}
