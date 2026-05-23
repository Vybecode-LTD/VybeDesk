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

    public SessionBuilderViewModel(ISessionBuilderService service) => _service = service;

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

    [RelayCommand]
    private void RemoveFile()
    {
        if (SelectedFilePath is null) return;
        FilePaths.Remove(SelectedFilePath);
        SelectedFilePath = null;
    }

    [RelayCommand]
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
