using System.Collections.ObjectModel;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Module 1 — Documentation Manager. Scans a project's docs, runs a structural
/// and an AI semantic pass, and produces a report + Claude Code fix prompt.
/// </summary>
public sealed partial class DocumentationViewModel : PageViewModel
{
    private readonly IDocReconciliationService _docService;
    private readonly IProjectStore _projects;
    private readonly IFilePickerService _picker;
    private IReadOnlyList<DocFile> _scanned = Array.Empty<DocFile>();
    private IReadOnlyList<Finding> _structural = Array.Empty<Finding>();

    public override string Title => "Documentation";
    public override string Glyph => "\U0001F4C4";
    public override string Description =>
        "Scan, list, and reconcile project documentation.";

    public ObservableCollection<Project> Projects { get; } = new();
    public ObservableCollection<DocFile> Docs { get; } = new();
    public ObservableCollection<Finding> Findings { get; } = new();

    [ObservableProperty] private Project? _selectedProject;
    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _semanticResult = "";
    [ObservableProperty] private string _fixPrompt = "";
    [ObservableProperty] private bool _isFixPromptVisible;
    [ObservableProperty] private string _statusMessage = "Pick a project or enter a folder path, then Scan.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReport))]
    private int _docCount;

    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private int _infoCount;

    public bool IsNotBusy => !IsBusy;
    public bool HasReport => DocCount > 0;
    public bool HasSemanticResult => !string.IsNullOrWhiteSpace(SemanticResult);

    public DocumentationViewModel(
        IDocReconciliationService docService,
        IProjectStore projects,
        IFilePickerService picker)
    {
        _docService = docService;
        _projects = projects;
        _picker = picker;
        _ = LoadProjectsAsync();
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var picked = await _picker.PickFolderAsync(
            title: "Pick the project folder to scan",
            startLocation: FolderPath);
        if (picked is not null) FolderPath = picked;
    }

    private async Task LoadProjectsAsync()
    {
        var all = await _projects.GetAllAsync();
        Projects.Clear();
        foreach (var p in all) Projects.Add(p);
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        if (value is not null && !string.IsNullOrWhiteSpace(value.FolderPath))
            FolderPath = value.FolderPath;
    }

    partial void OnSemanticResultChanged(string value)
        => OnPropertyChanged(nameof(HasSemanticResult));

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
        StatusMessage = "Scanning\u2026";
        try
        {
            _scanned = await _docService.ScanAsync(FolderPath, ct);
            Docs.Clear();
            foreach (var d in _scanned) Docs.Add(d);

            _structural = await _docService.AnalyzeStructuralAsync(_scanned, ct);
            Findings.Clear();
            foreach (var f in _structural) Findings.Add(f);

            DocCount = Docs.Count;
            CriticalCount = _structural.Count(f => f.Severity == FindingSeverity.Critical);
            WarningCount = _structural.Count(f => f.Severity == FindingSeverity.Warning);
            InfoCount = _structural.Count(f => f.Severity == FindingSeverity.Info);

            SemanticResult = "";
            IsFixPromptVisible = false;
            StatusMessage = Docs.Count + " doc(s) scanned, " + _structural.Count
                + " structural finding(s). Run AI Analysis for doc-vs-doc checks.";
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

    [RelayCommand]
    private async Task RunSemanticAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (_scanned.Count == 0)
        {
            StatusMessage = "Scan a project first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Running AI semantic analysis\u2026";
        try
        {
            SemanticResult = await _docService.AnalyzeSemanticAsync(_scanned, ct);
            StatusMessage = "AI semantic analysis complete.";
        }
        catch (Exception ex)
        {
            StatusMessage = "AI analysis failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GenerateFixPrompt()
    {
        if (_scanned.Count == 0)
        {
            StatusMessage = "Scan a project first.";
            return;
        }
        FixPrompt = _docService.BuildFixPrompt(FolderPath, _structural, SemanticResult);
        IsFixPromptVisible = true;
        StatusMessage = "Fix prompt generated — copy it into Claude Code.";
    }

    [RelayCommand]
    private async Task ExportReportAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (_scanned.Count == 0)
        {
            StatusMessage = "Scan a project first.";
            return;
        }

        IsBusy = true;
        try
        {
            var markdown = _docService.BuildReportMarkdown(FolderPath, _structural, SemanticResult);
            var path = await _docService.SaveReportAsync(FolderPath, markdown, ct);
            StatusMessage = "Report saved to " + path;
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
