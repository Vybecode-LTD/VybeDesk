using System.Collections.ObjectModel;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Module 3 — Session Builder. A 5-step wizard that turns a claude.ai project
/// into a Claude Code handoff package.
/// </summary>
public sealed partial class SessionBuilderViewModel : PageViewModel
{
    private static readonly string[] StepNames =
        { "Describe", "Transcripts", "Files", "Review", "Generate" };

    private readonly ISessionBuilderService _service;
    private readonly IFilePickerService _picker;

    public override string Title => "Session Builder";
    public override string Glyph => "\U0001F680";
    public override string Description =>
        "Turn a claude.ai project into a Claude Code handoff package.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDescribe), nameof(IsTranscripts), nameof(IsFiles),
        nameof(IsReview), nameof(IsGenerate), nameof(ShowBack), nameof(ShowNext),
        nameof(ShowGenerate), nameof(StepLabel))]
    private int _currentStep;

    [ObservableProperty] private string _projectName = "";
    [ObservableProperty] private string _projectDescription = "";
    [ObservableProperty] private string _stack = "";
    [ObservableProperty] private string _outputLocation = "";

    public ObservableCollection<TranscriptEntry> Transcripts { get; } = new();
    [ObservableProperty] private TranscriptEntry? _selectedTranscript;
    [ObservableProperty] private string _newTranscriptTitle = "";
    [ObservableProperty] private string _newTranscriptBody = "";

    public ObservableCollection<string> FilePaths { get; } = new();
    [ObservableProperty] private string? _selectedFilePath;
    [ObservableProperty] private string _newFilePath = "";

    [ObservableProperty] private string _reviewResult = "";
    [ObservableProperty] private string _resultMessage = "";
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;
    public bool IsDescribe => CurrentStep == 0;
    public bool IsTranscripts => CurrentStep == 1;
    public bool IsFiles => CurrentStep == 2;
    public bool IsReview => CurrentStep == 3;
    public bool IsGenerate => CurrentStep == 4;
    public bool ShowBack => CurrentStep > 0;
    public bool ShowNext => CurrentStep < 4;
    public bool ShowGenerate => CurrentStep == 4;
    public string StepLabel => "Step " + (CurrentStep + 1) + " of 5 — " + StepNames[CurrentStep];

    public SessionBuilderViewModel(ISessionBuilderService service, IFilePickerService picker)
    {
        _service = service;
        _picker = picker;
    }

    [RelayCommand]
    private async Task BrowseOutputLocationAsync()
    {
        var picked = await _picker.PickFolderAsync(
            title: "Pick a folder for the handoff package",
            startLocation: OutputLocation);
        if (picked is not null) OutputLocation = picked;
    }

    [RelayCommand]
    private async Task BrowseNewFileAsync()
    {
        var picked = await _picker.PickFileAsync(title: "Pick a file to stage");
        if (picked is not null) NewFilePath = picked;
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStep == 0 &&
            (string.IsNullOrWhiteSpace(ProjectName) || string.IsNullOrWhiteSpace(OutputLocation)))
        {
            StatusMessage = "Enter a project name and an output location first.";
            return;
        }
        if (CurrentStep < 4) CurrentStep++;
        StatusMessage = "";
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep > 0) CurrentStep--;
        StatusMessage = "";
    }

    [RelayCommand]
    private void AddTranscript()
    {
        if (string.IsNullOrWhiteSpace(NewTranscriptBody))
        {
            StatusMessage = "Paste the transcript text first.";
            return;
        }
        Transcripts.Add(new TranscriptEntry
        {
            Title = string.IsNullOrWhiteSpace(NewTranscriptTitle)
                ? "Transcript " + (Transcripts.Count + 1)
                : NewTranscriptTitle.Trim(),
            Body = NewTranscriptBody,
        });
        NewTranscriptTitle = "";
        NewTranscriptBody = "";
        StatusMessage = "Transcript added.";
    }

    [RelayCommand]
    private void RemoveTranscript()
    {
        if (SelectedTranscript is null) return;
        Transcripts.Remove(SelectedTranscript);
        SelectedTranscript = null;
    }

    [RelayCommand]
    private void AddFile()
    {
        var path = NewFilePath.Trim();
        if (path.Length == 0) return;
        if (!File.Exists(path))
        {
            StatusMessage = "No file found at that path.";
            return;
        }
        if (!FilePaths.Contains(path)) FilePaths.Add(path);
        NewFilePath = "";
        StatusMessage = "File staged.";
    }

    /// <summary>
    /// Bulk-add staged file paths from drag-and-drop. Non-existent paths are
    /// counted as "missing", in-list paths as "duplicate"; the status line
    /// summarizes added/duplicate/missing.
    /// </summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        int added = 0, duplicates = 0, missing = 0;
        foreach (var raw in paths)
        {
            var path = raw?.Trim();
            if (string.IsNullOrEmpty(path)) continue;
            if (!File.Exists(path)) { missing++; continue; }
            if (FilePaths.Contains(path)) { duplicates++; continue; }
            FilePaths.Add(path);
            added++;
        }

        var parts = new List<string>();
        if (added > 0) parts.Add(added + " staged");
        if (duplicates > 0) parts.Add(duplicates + " duplicate");
        if (missing > 0) parts.Add(missing + " missing");
        StatusMessage = parts.Count == 0 ? "Nothing to stage." : string.Join(", ", parts) + ".";
    }

    [RelayCommand]
    private void RemoveFile()
    {
        if (SelectedFilePath is null) return;
        FilePaths.Remove(SelectedFilePath);
        SelectedFilePath = null;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RunReviewAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "Running AI review\u2026";
        try
        {
            ReviewResult = await _service.ReviewAsync(BuildPlan(), ct);
            StatusMessage = "Review complete.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Review failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "Generating handoff package\u2026";
        try
        {
            var path = await _service.GenerateAsync(BuildPlan(), ct);
            ResultMessage = "Handoff package created at:\n" + path;
            StatusMessage = "Done.";
        }
        catch (Exception ex)
        {
            ResultMessage = "";
            StatusMessage = "Generation failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private SessionPlan BuildPlan() => new()
    {
        ProjectName = ProjectName.Trim(),
        Description = ProjectDescription.Trim(),
        Stack = Stack.Trim(),
        OutputLocation = OutputLocation.Trim(),
        Transcripts = Transcripts.ToList(),
        FilePaths = FilePaths.ToList(),
    };
}
