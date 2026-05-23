using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Module 2 — Prompt Manager. Browse, search, edit, and tag prompts; fill
/// {{variable}} templates; and use the AI to redesign or generate prompts.
/// </summary>
public sealed partial class PromptManagerViewModel : PageViewModel
{
    private const string AllCategories = "All";

    private readonly IPromptStore _store;
    private readonly IAiService _ai;
    private readonly List<PromptEntry> _all = new();

    public override string Title => "Prompts";
    public override string Glyph => "\U0001F4AC";
    public override string Description =>
        "Store, tag, and reuse your prompts; fill templates and get AI help.";

    public ObservableCollection<PromptEntry> Prompts { get; } = new();
    public ObservableCollection<string> Categories { get; } = new() { AllCategories };
    public ObservableCollection<TemplateVariable> Variables { get; } = new();

    [ObservableProperty] private PromptEntry? _selectedPrompt;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedCategory = AllCategories;

    [ObservableProperty] private string _editTitle = "";
    [ObservableProperty] private string _editCategory = "";
    [ObservableProperty] private string _editTagsText = "";
    [ObservableProperty] private string _editBody = "";

    [ObservableProperty] private bool _isFillPanelOpen;
    [ObservableProperty] private string _filledResult = "";

    [ObservableProperty] private bool _isRedesignPanelOpen;
    [ObservableProperty] private string _redesignResult = "";

    [ObservableProperty] private bool _isGeneratePanelOpen;
    [ObservableProperty] private string _generateRequest = "";
    [ObservableProperty] private string _generatedPrompt = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty] private string _statusMessage = "";

    public bool IsNotBusy => !IsBusy;
    public bool HasSelection => SelectedPrompt is not null;

    public PromptManagerViewModel(IPromptStore store, IAiService ai)
    {
        _store = store;
        _ai = ai;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var all = await _store.GetAllAsync();
        _all.Clear();
        _all.AddRange(all);
        RebuildCategories();
        ApplyFilter();
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

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        IEnumerable<PromptEntry> result = _all;

        if (SelectedCategory != AllCategories)
            result = result.Where(p => p.Category == SelectedCategory);

        if (q.Length > 0)
            result = result.Where(p =>
                p.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Body.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));

        Prompts.Clear();
        foreach (var p in result) Prompts.Add(p);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    partial void OnSelectedPromptChanged(PromptEntry? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        IsFillPanelOpen = false;
        IsRedesignPanelOpen = false;

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

    [RelayCommand]
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
            RedesignResult = await _ai.CompleteAsync(system, EditBody, ct);
            IsFillPanelOpen = false;
            IsRedesignPanelOpen = true;
            StatusMessage = "Redesign ready — review, then apply or dismiss.";
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
        StatusMessage = "Redesign applied — remember to Save.";
    }

    [RelayCommand]
    private void DismissRedesign() => IsRedesignPanelOpen = false;

    [RelayCommand]
    private void ToggleGeneratePanel()
    {
        IsGeneratePanelOpen = !IsGeneratePanelOpen;
        if (!IsGeneratePanelOpen) GeneratedPrompt = "";
    }

    [RelayCommand]
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
