using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace VybeDesk.App.ViewModels;

/// <summary>
/// Module 2 — Prompt Manager. Browse, search, edit, and tag prompts; fill
/// {{variable}} templates; and use the AI to redesign or generate prompts.
/// </summary>
public sealed partial class PromptManagerViewModel : PageViewModel
{
    private const string AllCategories = "All";

    /// <summary>
    /// Sentinel item placed at the top of <see cref="Projects"/> so the
    /// picker in the unified header can offer an "no project filter" option.
    /// Identified by <c>Id == Guid.Empty</c>. The filter treats this OR
    /// <c>null</c> as "show all prompts regardless of project tag".
    /// </summary>
    private static readonly Project AllProjectsSentinel = new()
    {
        Id = Guid.Empty,
        Name = "(All projects)",
    };

    private readonly IPromptStore _store;
    private readonly IAiService _ai;
    private readonly IProjectStore _projects;
    private readonly List<PromptEntry> _all = new();

    /// <summary>
    /// Module-local memory of the last selected project. Survives
    /// ContentPresenter detachment null pulses so the selection persists
    /// across navigation. Guid.Empty = AllProjectsSentinel.
    /// </summary>
    private Guid? _lastSelectedProjectId;
    private bool _reloadingProjects;

    public override string Title => "Prompts";
    public override string Glyph => "\U0001F4AC";
    public override string Description =>
        "Store, tag, and reuse your prompts; fill templates and get AI help.";

    public ObservableCollection<PromptEntry> Prompts { get; } = new();
    public ObservableCollection<string> Categories { get; } = new() { AllCategories };
    public ObservableCollection<TemplateVariable> Variables { get; } = new();
    public ObservableCollection<DiffLine> RedesignDiff { get; } = new();
    public ObservableCollection<PromptVersion> Versions { get; } = new();

    /// <summary>
    /// Projects shown in the unified header's picker. Includes
    /// <see cref="AllProjectsSentinel"/> as the first entry; selecting it
    /// (or null) disables the project filter.
    /// </summary>
    public ObservableCollection<Project> Projects { get; } =
        new() { AllProjectsSentinel };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(ShowEmptyPlaceholder))]
    private PromptEntry? _selectedPrompt;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedCategory = AllCategories;

    /// <summary>
    /// Active project filter. When null, the "Choose a project" landing is
    /// shown. When <see cref="AllProjectsSentinel"/>, the prompts list is
    /// unfiltered (all projects). When a real project is selected, only
    /// prompts whose <c>ProjectId</c> matches or whose <c>Tags</c> contain
    /// the project's name (case-insensitive) are shown.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject), nameof(IsDefaultViewVisible))]
    private Project? _selectedProject;

    [ObservableProperty] private string _editTitle = "";
    [ObservableProperty] private string _editCategory = "";
    [ObservableProperty] private string _editTagsText = "";
    [ObservableProperty] private string _editBody = "";

    [ObservableProperty] private bool _isFillPanelOpen;
    [ObservableProperty] private string _filledResult = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDefaultViewVisible))]
    private bool _isRedesignPanelOpen;
    [ObservableProperty] private string _redesignResult = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDefaultViewVisible))]
    private bool _isHistoryPanelOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyPlaceholder))]
    private bool _isGeneratePanelOpen;
    [ObservableProperty] private string _generateRequest = "";
    [ObservableProperty] private string _generatedPrompt = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;
    public bool HasSelection => SelectedPrompt is not null;
    public bool ShowEmptyPlaceholder => !HasSelection && !IsGeneratePanelOpen;
    public bool IsDefaultViewVisible => HasProject && !IsRedesignPanelOpen && !IsHistoryPanelOpen;

    public PromptManagerViewModel(
        IPromptStore store, IAiService ai, IClipboardService clipboard,
        IProjectStore projects)
    {
        _store = store;
        _ai = ai;
        Clipboard = clipboard;
        _projects = projects;
        _projects.Changed += OnProjectsChanged;
        _ = LoadAsync();
        _ = LoadProjectsAsync();
    }

    private void OnProjectsChanged()
        => Avalonia.Threading.Dispatcher.UIThread.Post(async () => await LoadProjectsAsync());

    /// <summary>Whether a project (or All Projects) has been chosen.</summary>
    public bool HasProject => SelectedProject is not null;

    private async Task LoadProjectsAsync()
    {
        var all = await _projects.GetAllAsync();
        // Capture keepId AFTER the async gap: prefer the current selection,
        // then fall back to module-local memory.
        var keepId = SelectedProject?.Id ?? _lastSelectedProjectId;

        _reloadingProjects = true;
        try
        {
            Projects.Clear();
            Projects.Add(AllProjectsSentinel);
            foreach (var p in all) Projects.Add(p);
        }
        finally
        {
            _reloadingProjects = false;
        }

        // Only restore a previous selection — do NOT auto-select on
        // first load. The view shows a "Choose a project" landing until
        // the user picks one (or chooses "All Projects").
        SelectedProject = keepId is not null
            ? Projects.FirstOrDefault(p => p.Id == keepId)
            : null;

        // Persist to module-local memory so the value survives even if
        // OnSelectedProjectChanged was suppressed during the rebuild.
        _lastSelectedProjectId = SelectedProject?.Id;
    }

    private async Task LoadAsync()
    {
        var all = await _store.GetAllAsync();
        _all.Clear();
        _all.AddRange(all);
        RebuildCategories();
        await ApplyFilterAsync();
    }

    private void RebuildCategories()
    {
        var current = SelectedCategory;
        Categories.Clear();
        Categories.Add(AllCategories);
        foreach (var cat in _all.Select(p => p.Category)
                                .Where(c => !string.IsNullOrWhiteSpace(c))
                                .Distinct()
                                .OrderBy(c => c))
        {
            Categories.Add(cat);
        }
        SelectedCategory = Categories.Contains(current) ? current : AllCategories;
    }

    private async Task ApplyFilterAsync()
    {
        var matches = await _store.SearchAsync(SearchText);
        IEnumerable<PromptEntry> result = matches;

        if (SelectedCategory != AllCategories)
            result = result.Where(p => p.Category == SelectedCategory);

        // Project filter — sentinel / null means "all projects".
        if (SelectedProject is { } proj && proj.Id != Guid.Empty)
        {
            result = result.Where(p =>
                p.ProjectId == proj.Id ||
                p.Tags.Any(t => string.Equals(t, proj.Name, StringComparison.OrdinalIgnoreCase)));
        }

        Prompts.Clear();
        foreach (var p in result) Prompts.Add(p);
    }

    partial void OnSearchTextChanged(string value) => _ = ApplyFilterAsync();
    partial void OnSelectedCategoryChanged(string value) => _ = ApplyFilterAsync();
    partial void OnSelectedProjectChanged(Project? oldValue, Project? newValue)
    {
        // Suppress transient nulls from Projects.Clear() inside LoadProjectsAsync.
        if (_reloadingProjects) return;

        // Ignore null writes from view detachment. When Avalonia's
        // ContentPresenter detaches this view on navigation, the ComboBox
        // TwoWay binding fires null back. We must NOT clear state for this
        // — OnActivated will restore the selection when we come back.
        if (newValue is null && oldValue is not null) return;

        // Persist the user's selection for this module.
        _lastSelectedProjectId = newValue?.Id;

        _ = ApplyFilterAsync();
    }

    /// <summary>
    /// Re-sync SelectedProject from this module's own local memory on
    /// every navigation back to this page.
    /// </summary>
    public override void OnActivated()
    {
        if (_lastSelectedProjectId is null) return;
        if (SelectedProject?.Id == _lastSelectedProjectId) return;
        var found = Projects.FirstOrDefault(p => p.Id == _lastSelectedProjectId);
        if (found is not null)
            SelectedProject = found;
    }

    partial void OnSelectedPromptChanged(PromptEntry? value)
    {
        IsFillPanelOpen = false;
        IsRedesignPanelOpen = false;

        if (value is not null)
        {
            IsGeneratePanelOpen = false;
            GeneratedPrompt = "";
            GenerateRequest = "";
        }

        if (value is null)
        {
            EditTitle = EditCategory = EditTagsText = EditBody = "";
            return;
        }

        EditTitle = value.Title;
        EditCategory = value.Category;
        EditTagsText = string.Join(", ", value.Tags);
        EditBody = value.Body;
    }

    [RelayCommand]
    private async Task NewPromptAsync()
    {
        var entry = new PromptEntry { Title = "Untitled prompt", Body = "" };
        if (SelectedProject is { } proj && proj.Id != Guid.Empty)
            entry.ProjectId = proj.Id;
        await _store.AddAsync(entry);
        SearchText = "";
        SelectedCategory = AllCategories;
        await LoadAsync();
        SelectedPrompt = _all.FirstOrDefault(p => p.Id == entry.Id);
        StatusMessage = "New prompt created.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedPrompt is null) return;

        SelectedPrompt.Title = string.IsNullOrWhiteSpace(EditTitle) ? "Untitled prompt" : EditTitle.Trim();
        SelectedPrompt.Category = string.IsNullOrWhiteSpace(EditCategory) ? "General" : EditCategory.Trim();
        SelectedPrompt.Tags = ParseTags(EditTagsText);
        SelectedPrompt.Body = EditBody;
        SelectedPrompt.Modified = DateTimeOffset.Now;

        await _store.UpdateAsync(SelectedPrompt);
        var keepId = SelectedPrompt.Id;
        await LoadAsync();
        SelectedPrompt = _all.FirstOrDefault(p => p.Id == keepId);
        StatusMessage = "Saved.";
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedPrompt is null) return;
        await _store.RemoveAsync(SelectedPrompt.Id);
        SelectedPrompt = null;
        await LoadAsync();
        StatusMessage = "Prompt deleted.";
    }

    [RelayCommand]
    private void OpenFillPanel()
    {
        if (SelectedPrompt is null) return;
        Variables.Clear();
        foreach (var name in ExtractVariables(EditBody))
            Variables.Add(new TemplateVariable(name));
        FilledResult = "";
        IsRedesignPanelOpen = false;
        IsFillPanelOpen = true;
        StatusMessage = Variables.Count == 0
            ? "This prompt has no {{variables}} — the body is ready to use as-is."
            : Variables.Count + " variable(s) to fill.";
    }

    [RelayCommand]
    private void CloseFillPanel()
    {
        IsFillPanelOpen = false;
        Variables.Clear();
        FilledResult = "";
    }

    [RelayCommand]
    private async Task BuildFilledAsync()
    {
        if (SelectedPrompt is null) return;

        var result = EditBody;
        foreach (var v in Variables)
            result = Regex.Replace(
                result,
                @"\{\{\s*" + Regex.Escape(v.Name) + @"\s*\}\}",
                v.Value);
        FilledResult = result;

        SelectedPrompt.UsageCount++;
        await _store.UpdateAsync(SelectedPrompt);
        StatusMessage = "Filled — copy the result below.";
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RedesignAsync(CancellationToken ct)
    {
        if (SelectedPrompt is null || IsBusy) return;
        if (string.IsNullOrWhiteSpace(EditBody))
        {
            StatusMessage = "Nothing to redesign — the prompt body is empty.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Asking Claude to redesign the prompt\u2026";
        try
        {
            const string system =
                "You are a prompt engineer. Rewrite the user's prompt to be maximally " +
                "effective for Claude Code: clear, specific, well-structured, with explicit " +
                "instructions and any useful constraints. Return ONLY the rewritten prompt, " +
                "with no preamble or commentary.";
            var redesigned = await _ai.CompleteAsync(system, EditBody, ct);
            BuildRedesignDiff(EditBody, redesigned);
            RedesignResult = redesigned;
            IsFillPanelOpen = false;
            IsRedesignPanelOpen = true;
            StatusMessage = "Redesign ready — review the diff, then apply or dismiss.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Redesign failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ApplyRedesign()
    {
        EditBody = RedesignResult;
        IsRedesignPanelOpen = false;
        RedesignDiff.Clear();
        StatusMessage = "Redesign applied — remember to Save.";
    }

    [RelayCommand]
    private async Task ApplyRedesignAndSaveAsync()
    {
        if (SelectedPrompt is null) return;
        EditBody = RedesignResult;
        IsRedesignPanelOpen = false;
        RedesignDiff.Clear();
        await SaveAsync();
        StatusMessage = "Redesign applied and saved.";
    }

    [RelayCommand]
    private void DismissRedesign()
    {
        IsRedesignPanelOpen = false;
        RedesignDiff.Clear();
    }

    [RelayCommand]
    private async Task OpenHistoryAsync()
    {
        if (SelectedPrompt is null) return;
        Versions.Clear();
        var versions = await _store.GetVersionsAsync(SelectedPrompt.Id);
        foreach (var v in versions) Versions.Add(v);
        IsFillPanelOpen = false;
        IsRedesignPanelOpen = false;
        IsHistoryPanelOpen = true;
        StatusMessage = Versions.Count == 0
            ? "No prior versions saved yet — they'll appear after the next content change."
            : Versions.Count + " prior version(s).";
    }

    [RelayCommand]
    private void CloseHistory()
    {
        IsHistoryPanelOpen = false;
        Versions.Clear();
    }

    [RelayCommand]
    private void Restore(PromptVersion? version)
    {
        if (version is null) return;
        EditTitle = version.Title;
        EditCategory = version.Category;
        EditTagsText = string.Join(", ", version.Tags);
        EditBody = version.Body;
        IsHistoryPanelOpen = false;
        Versions.Clear();
        StatusMessage = "Restored a prior version to the editor — Save to make it permanent.";
    }

    private void BuildRedesignDiff(string oldText, string newText)
    {
        RedesignDiff.Clear();
        var model = InlineDiffBuilder.Diff(oldText ?? "", newText ?? "");
        foreach (var line in model.Lines)
        {
            var kind = line.Type switch
            {
                ChangeType.Inserted => DiffLineKind.Inserted,
                ChangeType.Deleted  => DiffLineKind.Deleted,
                _                   => DiffLineKind.Unchanged,
            };
            RedesignDiff.Add(new DiffLine(line.Text ?? "", kind));
        }
    }

    [RelayCommand]
    private void ToggleGeneratePanel()
    {
        IsGeneratePanelOpen = !IsGeneratePanelOpen;
        if (IsGeneratePanelOpen)
        {
            SelectedPrompt = null;
            IsFillPanelOpen = false;
            IsRedesignPanelOpen = false;
            IsHistoryPanelOpen = false;
        }
        else
        {
            GeneratedPrompt = "";
            GenerateRequest = "";
        }
    }

    [RelayCommand]
    private void CloseGeneratePanel()
    {
        IsGeneratePanelOpen = false;
        GeneratedPrompt = "";
        GenerateRequest = "";
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task GenerateAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(GenerateRequest))
        {
            StatusMessage = "Describe the prompt you need first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Generating a prompt\u2026";
        try
        {
            const string system =
                "You are a prompt engineer. The user describes a task they want a prompt " +
                "for. Produce a single, high-quality, ready-to-use prompt for that task. " +
                "Use {{variable}} placeholders for parts the user should fill in. Return " +
                "ONLY the prompt text, with no preamble or commentary.";
            GeneratedPrompt = await _ai.CompleteAsync(system, GenerateRequest, ct);
            StatusMessage = "Prompt generated — edit if needed, then save it.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Generation failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveGeneratedAsync()
    {
        if (string.IsNullOrWhiteSpace(GeneratedPrompt)) return;

        var entry = new PromptEntry
        {
            Title = "Generated prompt",
            Body = GeneratedPrompt,
            Category = "Generated",
        };
        if (SelectedProject is { } proj && proj.Id != Guid.Empty)
            entry.ProjectId = proj.Id;
        await _store.AddAsync(entry);
        SearchText = "";
        SelectedCategory = AllCategories;
        await LoadAsync();
        SelectedPrompt = _all.FirstOrDefault(p => p.Id == entry.Id);
        IsGeneratePanelOpen = false;
        GenerateRequest = "";
        GeneratedPrompt = "";
        StatusMessage = "Generated prompt saved.";
    }

    private static List<string> ParseTags(string text)
        => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Distinct()
               .ToList();

    private static IEnumerable<string> ExtractVariables(string body)
        => Regex.Matches(body, @"\{\{\s*([A-Za-z0-9_]+)\s*\}\}")
                .Select(m => m.Groups[1].Value)
                .Distinct();
}
