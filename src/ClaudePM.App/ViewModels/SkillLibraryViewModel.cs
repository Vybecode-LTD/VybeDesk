using System.Collections.ObjectModel;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Module 5 — Skill Library Manager. Browse, validate, edit, dedupe, and
/// rename the user's skills (flat *.skill or folder/SKILL.md), then export
/// to both formats so the same skill loads in Claude Code and Claude web.
/// </summary>
public sealed partial class SkillLibraryViewModel : PageViewModel
{
    private readonly ISkillLibraryService _service;
    private readonly IFilePickerService _picker;
    private readonly List<SkillFile> _all = new();
    private IReadOnlyList<Finding> _duplicates = Array.Empty<Finding>();

    public override string Title => "Skill Library";
    public override string Glyph => "\U0001F9E9";
    public override string Description =>
        "Browse, validate, edit, rename, and dedupe your skills.";

    public ObservableCollection<SkillFile> Skills { get; } = new();
    public ObservableCollection<Finding> Issues { get; } = new();

    [ObservableProperty] private SkillFile? _selectedSkill;
    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _statusMessage = "Enter a folder path, then Scan.";

    [ObservableProperty] private string _editName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionBudget))]
    [NotifyPropertyChangedFor(nameof(DescriptionOverBudget))]
    private string _editDescription = "";

    [ObservableProperty] private string _editBody = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private int _infoCount;

    /// <summary>
    /// When set, the right pane shows every finding of this severity across
    /// every scanned skill, not just the selected skill's findings. Click
    /// any severity chip to set; Clear Filter to reset.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterActive))]
    [NotifyPropertyChangedFor(nameof(IsEditorVisible))]
    [NotifyPropertyChangedFor(nameof(FilterHeader))]
    private FindingSeverity? _severityFilter;

    public bool IsNotBusy => !IsBusy;
    public bool HasSelection => SelectedSkill is not null;
    public bool HasResults => Skills.Count > 0;
    public string DescriptionBudget => EditDescription.Length + " / 1024";
    public bool DescriptionOverBudget => EditDescription.Length >= 1024;
    public bool IsFilterActive => SeverityFilter is not null;
    public bool IsEditorVisible => HasSelection && !IsFilterActive;
    public string FilterHeader => SeverityFilter is { } sev
        ? "Showing all " + sev.ToString().ToLowerInvariant() + " findings across "
            + _all.Count + " skill(s)"
        : "";

    public SkillLibraryViewModel(ISkillLibraryService service, IFilePickerService picker)
    {
        _service = service;
        _picker = picker;
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var picked = await _picker.PickFolderAsync(
            title: "Pick a folder with .skill files",
            startLocation: FolderPath);
        if (picked is not null) FolderPath = picked;
    }

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
        StatusMessage = "Scanning for skills…";
        try
        {
            var found = await _service.ScanAsync(FolderPath, ct);
            _all.Clear();
            _all.AddRange(found);
            _duplicates = _service.FindDuplicates(_all);

            Skills.Clear();
            foreach (var s in _all) Skills.Add(s);
            SelectedSkill = null;
            SeverityFilter = null;

            RecomputeCounts();
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

    partial void OnSelectedSkillChanged(SkillFile? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsEditorVisible));

        // Selecting a skill clears any active global filter so the right
        // pane reverts to the per-skill editor/findings view.
        if (value is not null) SeverityFilter = null;

        if (value is null)
        {
            EditName = EditDescription = EditBody = "";
            Issues.Clear();
            return;
        }

        EditName = value.Name;
        EditDescription = value.Description;
        EditBody = value.Body;
        RefreshIssues();
    }

    partial void OnSeverityFilterChanged(FindingSeverity? value) => RefreshIssues();

    /// <summary>
    /// Rebuilds the Issues collection from whichever source matches the
    /// current mode: a global per-severity slice when a filter is active,
    /// or the selected skill's own findings otherwise.
    /// </summary>
    private void RefreshIssues()
    {
        Issues.Clear();

        if (SeverityFilter is { } sev)
        {
            foreach (var s in _all)
                foreach (var f in _service.Validate(s))
                    if (f.Severity == sev) Issues.Add(f);
            foreach (var d in _duplicates)
                if (d.Severity == sev) Issues.Add(d);
            return;
        }

        if (SelectedSkill is null) return;
        foreach (var f in _service.Validate(SelectedSkill)) Issues.Add(f);
        foreach (var d in _duplicates)
            if (d.File.Equals(SelectedSkill.Name, StringComparison.OrdinalIgnoreCase))
                Issues.Add(d);
    }

    private void RecomputeCounts()
    {
        var all = new List<Finding>();
        foreach (var s in _all) all.AddRange(_service.Validate(s));
        all.AddRange(_duplicates);
        CriticalCount = all.Count(f => f.Severity == FindingSeverity.Critical);
        WarningCount = all.Count(f => f.Severity == FindingSeverity.Warning);
        InfoCount = all.Count(f => f.Severity == FindingSeverity.Info);
    }

    [RelayCommand]
    private void FilterBySeverity(FindingSeverity severity)
        => SeverityFilter = SeverityFilter == severity ? (FindingSeverity?)null : severity;

    [RelayCommand]
    private void ClearFilter() => SeverityFilter = null;

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
            _duplicates = _service.FindDuplicates(_all);
            RefreshIssues();
            RecomputeCounts();
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

    /// <summary>
    /// Renames the selected skill on disk to match the current <see cref="EditName"/>.
    /// Handles both formats — flat <c>*.skill</c> files get File.Move; folder-format
    /// SKILL.md gets Directory.Move on its containing folder. Refuses to overwrite
    /// an existing target and refuses to rename when the editor name is empty
    /// or unchanged. Also rewrites the skill so the on-disk frontmatter <c>name:</c>
    /// matches the new filename.
    /// </summary>
    [RelayCommand]
    private async Task RenameAsync(CancellationToken ct)
    {
        if (SelectedSkill is null || IsBusy) return;
        var newName = EditName.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            StatusMessage = "Enter a name first.";
            return;
        }

        var oldFull = SelectedSkill.FullPath;
        var isFolderFormat = SelectedSkill.FileName.EndsWith(
            "/SKILL.md", StringComparison.OrdinalIgnoreCase);

        string newFull;
        string newFileName;

        IsBusy = true;
        try
        {
            if (isFolderFormat)
            {
                // oldFull = …/<oldName>/SKILL.md → rename the containing folder.
                var parentDir = Path.GetDirectoryName(oldFull)!;
                var grandparent = Path.GetDirectoryName(parentDir)!;
                var newParent = Path.Combine(grandparent, newName);
                if (string.Equals(parentDir, newParent, StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = "Name unchanged.";
                    return;
                }
                if (Directory.Exists(newParent) || File.Exists(newParent))
                {
                    StatusMessage = "A folder named '" + newName + "' already exists.";
                    return;
                }
                Directory.Move(parentDir, newParent);
                newFull = Path.Combine(newParent, "SKILL.md");
                newFileName = newName + "/SKILL.md";
            }
            else
            {
                // oldFull = …/<oldName>.skill → rename the file.
                var dir = Path.GetDirectoryName(oldFull)!;
                var newPath = Path.Combine(dir, newName + ".skill");
                if (string.Equals(oldFull, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = "Name unchanged.";
                    return;
                }
                if (File.Exists(newPath))
                {
                    StatusMessage = "A file named '" + newName + ".skill' already exists.";
                    return;
                }
                File.Move(oldFull, newPath);
                newFull = newPath;
                newFileName = newName + ".skill";
            }

            SelectedSkill.FullPath = newFull;
            SelectedSkill.FileName = newFileName;
            SelectedSkill.Name = newName;
            SelectedSkill.Description = EditDescription.Trim();
            SelectedSkill.Body = EditBody;
            SelectedSkill.HasFrontMatter = true;

            // Rewrite so the new file's frontmatter `name:` matches.
            await _service.SaveAsync(SelectedSkill, ct);

            // Refresh derived state + force the list row to redraw with the
            // new display name (SkillFile isn't observable, so swap it out
            // in-place to nudge the ListBox).
            _duplicates = _service.FindDuplicates(_all);
            RecomputeCounts();
            var idx = Skills.IndexOf(SelectedSkill);
            if (idx >= 0)
            {
                var skill = SelectedSkill;
                Skills.RemoveAt(idx);
                Skills.Insert(idx, skill);
                SelectedSkill = skill;
            }
            else
            {
                RefreshIssues();
            }
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

    [RelayCommand]
    private async Task ExportAsync(CancellationToken ct)
    {
        if (SelectedSkill is null || IsBusy) return;
        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            StatusMessage = "Set a folder path to export into.";
            return;
        }

        SelectedSkill.Name = EditName.Trim();
        SelectedSkill.Description = EditDescription.Trim();
        SelectedSkill.Body = EditBody;

        IsBusy = true;
        try
        {
            var path = await _service.ExportAsync(SelectedSkill, FolderPath, ct);
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
}
