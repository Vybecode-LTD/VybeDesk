using System.Collections.ObjectModel;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VybeDesk.App.ViewModels;

/// <summary>
/// Module 3 — Session Builder. A 6-step wizard that turns a claude.ai project
/// into a Claude Code handoff package. Step 0 picks the stack template that
/// drives the generated scaffolding (M4 #15); steps 1–5 collect the
/// project content.
/// </summary>
public sealed partial class SessionBuilderViewModel : PageViewModel
{
    private static readonly string[] StepNames =
        { "Template", "Describe", "Transcripts", "Files", "Review", "Generate" };

    private readonly ISessionBuilderService _service;
    private readonly IFilePickerService _picker;
    public override string Title => "Session Builder";
    public override string Glyph => "\U0001F680";
    public override string Description =>
        "Turn a claude.ai project into a Claude Code handoff package.";

    // ===== Unified module header (v0.31) ======================================
    //
    // Source-generator naming convention (see VisionAuditViewModel for the
    // detailed explanation): the [RelayCommand] methods are named differently
    // from the base virtuals (GoToFirstStep / ResetCurrentStep / RestartWizard)
    // and forwarded via expression-bodied overrides below.

    // StepLabel already gives a perfect one-crumb summary
    // ("Step N of 6 — <Name>"), so we surface it as the single breadcrumb.
    public override IReadOnlyList<string> Breadcrumbs => new[] { StepLabel };

    public override IRelayCommand? GoModuleHomeCommand => GoToFirstStepCommand;
    public override IRelayCommand? ResetCommand        => ResetCurrentStepCommand;
    public override IRelayCommand? RestartCommand      => RestartWizardCommand;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTemplate), nameof(IsDescribe), nameof(IsTranscripts),
        nameof(IsFiles), nameof(IsReview), nameof(IsGenerate), nameof(ShowBack), nameof(ShowNext),
        nameof(ShowGenerate), nameof(StepLabel), nameof(Breadcrumbs))]
    private int _currentStep;

    // ===== Step 0: Template ===================================================

    [ObservableProperty] private SessionTemplate _selectedTemplate = SessionTemplate.PlainMonorepo;

    /// <summary>
    /// All available templates. Retained as a public read-only surface even
    /// though the current view renders per-template radios in markup directly;
    /// future work (cost tracking, template metadata diagnostics) wants the
    /// programmatic enumeration.
    /// </summary>
    public IReadOnlyList<SessionTemplate> AvailableTemplates { get; } =
        Enum.GetValues<SessionTemplate>();

    // ===== Step 1: Describe ===================================================

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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    // 6-step wizard: 0=Template, 1=Describe, 2=Transcripts, 3=Files, 4=Review, 5=Generate.
    public bool IsTemplate    => CurrentStep == 0;
    public bool IsDescribe    => CurrentStep == 1;
    public bool IsTranscripts => CurrentStep == 2;
    public bool IsFiles       => CurrentStep == 3;
    public bool IsReview      => CurrentStep == 4;
    public bool IsGenerate    => CurrentStep == 5;
    public bool ShowBack      => CurrentStep > 0;
    public bool ShowNext      => CurrentStep < 5;
    public bool ShowGenerate  => CurrentStep == 5;
    public string StepLabel   => "Step " + (CurrentStep + 1) + " of 6 — " + StepNames[CurrentStep];

    public SessionBuilderViewModel(
        ISessionBuilderService service, IFilePickerService picker, IClipboardService clipboard)
    {
        _service = service;
        _picker = picker;
        Clipboard = clipboard;
    }

    /// <summary>
    /// Human-readable label for a <see cref="SessionTemplate"/>. Used by the
    /// View to render the picker — the bare enum names are PascalCase which
    /// reads poorly in the UI.
    /// </summary>
    public static string DisplayName(SessionTemplate template) => template switch
    {
        SessionTemplate.PlainMonorepo    => "Plain monorepo — no template",
        SessionTemplate.AvaloniaDotNet   => "Avalonia + .NET",
        SessionTemplate.FastApiPython    => "FastAPI + Python",
        SessionTemplate.NextJsTypeScript => "Next.js + TypeScript",
        SessionTemplate.PythonCli        => "Python CLI",
        _ => template.ToString(),
    };

    /// <summary>
    /// Short hint shown under each template radio. Helps the user pick without
    /// already knowing what each stack means.
    /// </summary>
    public static string DescriptionFor(SessionTemplate template) => template switch
    {
        SessionTemplate.PlainMonorepo    => "Generic scaffolding — safe default if no other template fits.",
        SessionTemplate.AvaloniaDotNet   => "Cross-platform desktop app on .NET with MVVM and compiled bindings.",
        SessionTemplate.FastApiPython    => "Async HTTP service in Python with type hints and Pydantic schemas.",
        SessionTemplate.NextJsTypeScript => "Web app on Next.js App Router with TypeScript strict mode.",
        SessionTemplate.PythonCli        => "Command-line tool packaged as a console entry point.",
        _ => "",
    };

    [RelayCommand]
    private void PickTemplate(SessionTemplate template)
    {
        SelectedTemplate = template;
        StatusMessage = DisplayName(template) + " selected.";
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
        // Step 0 (Template) — no validation; there is always a default.
        // Step 1 (Describe) — projectName + outputLocation are required.
        if (CurrentStep == 1 &&
            (string.IsNullOrWhiteSpace(ProjectName) || string.IsNullOrWhiteSpace(OutputLocation)))
        {
            StatusMessage = "Enter a project name and an output location first.";
            return;
        }
        if (CurrentStep < 5) CurrentStep++;
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
        StatusMessage = "Running AI review…";
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
        StatusMessage = "Generating handoff package…";
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
        Template = SelectedTemplate,
    };

    // ===== Unified-header commands (v0.31) ====================================

    /// <summary>
    /// Jump back to the Template step WITHOUT clearing any data. Every field
    /// the user has entered so far is preserved.
    /// Matches the GoModuleHome semantic on PageViewModel.
    /// </summary>
    [RelayCommand]
    private void GoToFirstStep()
    {
        CurrentStep = 0;
        StatusMessage = "Back to step 1 — your data is preserved.";
    }

    /// <summary>
    /// Clear the input fields on the CURRENT step only. Does NOT change
    /// the active step. Per-step:
    ///   Template    — reset SelectedTemplate to PlainMonorepo.
    ///   Describe    — clear ProjectName / ProjectDescription / Stack / OutputLocation.
    ///   Transcripts — clear NewTranscriptTitle / NewTranscriptBody AND the Transcripts collection.
    ///   Files       — clear NewFilePath AND the FilePaths collection.
    ///   Review      — clear ReviewResult.
    ///   Generate    — clear ResultMessage.
    /// </summary>
    [RelayCommand]
    private void ResetCurrentStep()
    {
        if (IsBusy) return;
        switch (CurrentStep)
        {
            case 0:
                SelectedTemplate = SessionTemplate.PlainMonorepo;
                StatusMessage = "Template reset to the default.";
                break;
            case 1:
                ProjectName = "";
                ProjectDescription = "";
                Stack = "";
                OutputLocation = "";
                StatusMessage = "Describe-step fields cleared.";
                break;
            case 2:
                NewTranscriptTitle = "";
                NewTranscriptBody = "";
                Transcripts.Clear();
                SelectedTranscript = null;
                StatusMessage = "Transcripts cleared.";
                break;
            case 3:
                NewFilePath = "";
                FilePaths.Clear();
                SelectedFilePath = null;
                StatusMessage = "Staged files cleared.";
                break;
            case 4:
                ReviewResult = "";
                StatusMessage = "Review result cleared.";
                break;
            case 5:
                ResultMessage = "";
                StatusMessage = "Generate result cleared.";
                break;
        }
    }

    /// <summary>
    /// Clear ALL wizard state across every step and navigate back to step 1.
    /// The hard reset.
    /// </summary>
    [RelayCommand]
    private void RestartWizard()
    {
        if (IsBusy) return;
        SelectedTemplate = SessionTemplate.PlainMonorepo;
        ProjectName = "";
        ProjectDescription = "";
        Stack = "";
        OutputLocation = "";
        NewTranscriptTitle = "";
        NewTranscriptBody = "";
        Transcripts.Clear();
        SelectedTranscript = null;
        NewFilePath = "";
        FilePaths.Clear();
        SelectedFilePath = null;
        ReviewResult = "";
        ResultMessage = "";
        CurrentStep = 0;
        StatusMessage = "Reset — pick a template for a new handoff package to begin.";
    }
}
