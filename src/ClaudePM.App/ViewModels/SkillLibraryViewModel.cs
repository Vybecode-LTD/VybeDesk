using System.Collections.ObjectModel;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Module 5 — Skill Library Manager. Browse, validate, edit, and dedupe the
/// user's .skill files, then export valid ones.
/// </summary>
public sealed partial class SkillLibraryViewModel : PageViewModel
{
    private readonly ISkillLibraryService _service;
    private readonly List<SkillFile> _all = new();
    private IReadOnlyList<Finding> _duplicates = Array.Empty<Finding>();

    public override string Title => "Skill Library";
    public override string Glyph => "\U0001F9E9";
    public override string Description =>
        "Browse, validate, edit, and dedupe your .skill files.";

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

    public bool IsNotBusy => !IsBusy;
    public bool HasSelection => SelectedSkill is not null;
    public bool HasResults => Skills.Count > 0;
    public string DescriptionBudget => EditDescription.Length + " / 1024";
    public bool DescriptionOverBudget => EditDescription.Length >= 1024;

    public SkillLibraryViewModel(ISkillLibraryService service) => _service = service;

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
            _all.Clear();
            _all.AddRange(found);
            _duplicates = _service.FindDuplicates(_all);

            Skills.Clear();
            foreach (var s in _all) Skills.Add(s);
            SelectedSkill = null;

            RecomputeCounts();
            OnPropertyChanged(nameof(HasResults));
            StatusMessage = _all.Count + " skill file(s) found.";
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
        Issues.Clear();

        if (value is null)
        {
            EditName = EditDescription = EditBody = "";
            return;
        }

        EditName = value.Name;
        EditDescription = value.Description;
        EditBody = value.Body;
        RefreshIssuesFor(value);
    }

    private void RefreshIssuesFor(SkillFile skill)
    {
        Issues.Clear();
        foreach (var f in _service.Validate(skill)) Issues.Add(f);
        foreach (var d in _duplicates)
            if (d.File.Equals(skill.Name, StringComparison.OrdinalIgnoreCase))
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
            RefreshIssuesFor(SelectedSkill);
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
