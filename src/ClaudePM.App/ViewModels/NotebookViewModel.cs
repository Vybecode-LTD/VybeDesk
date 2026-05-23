using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Module 4 — AI Notebook. Conversational advice, saved notes, and AI-proposed
/// filesystem actions gated by validate / execute / undo within scoped roots.
/// </summary>
public sealed partial class NotebookViewModel : PageViewModel
{
    private const string SystemPrompt =
        "You are an assistant inside a desktop project-manager app. Give helpful, concise " +
        "advice. The user may ask you to create files or folders, or to move them. When — and " +
        "only when — the user clearly wants such a filesystem operation, append to your reply " +
        "a single fenced code block tagged json containing {\"actions\":[...]}. Each action is " +
        "an object with \"kind\" (one of \"create_file\", \"create_folder\", \"move\"), " +
        "\"path\" (an absolute path), \"destinationPath\" (absolute, for move only), and " +
        "\"content\" (for create_file). Only use paths inside the user's project folders. If " +
        "no filesystem operation is wanted, do not include a json block.";

    private static readonly Regex JsonBlockRx =
        new(@"```json\s*(\{.*?\})\s*```", RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly IAiService _ai;
    private readonly INoteStore _noteStore;
    private readonly IProjectStore _projects;
    private readonly IAgentActionService _agent;

    public override string Title => "Notebook";
    public override string Glyph => "\U0001F4D3";
    public override string Description =>
        "Ask the AI for advice, save notes, and let it take scoped filesystem actions.";

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<AgentActionRow> PendingActions { get; } = new();
    public ObservableCollection<string> ActionHistory { get; } = new();
    public ObservableCollection<Note> Notes { get; } = new();

    [ObservableProperty] private string _chatInput = "";
    [ObservableProperty] private string _scopedRootsText = "";
    [ObservableProperty] private Note? _selectedNote;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;
    public bool CanUndo => _agent.CanUndo;
    public bool HasPendingActions => PendingActions.Count > 0;

    public NotebookViewModel(
        IAiService ai, INoteStore noteStore, IProjectStore projects, IAgentActionService agent)
    {
        _ai = ai;
        _noteStore = noteStore;
        _projects = projects;
        _agent = agent;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var projects = await _projects.GetAllAsync();
        var roots = projects.Select(p => p.FolderPath)
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .Distinct()
                            .ToList();
        _agent.SetScopedRoots(roots);
        ScopedRootsText = roots.Count > 0
            ? string.Join("\n", roots)
            : "No project folders registered — file actions will be blocked.";
        await LoadNotesAsync();
    }

    private async Task LoadNotesAsync()
    {
        var all = await _noteStore.GetAllAsync();
        Notes.Clear();
        foreach (var n in all) Notes.Add(n);
    }

    [RelayCommand]
    private async Task SendAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        var text = ChatInput.Trim();
        if (text.Length == 0) return;

        Messages.Add(new ChatMessage(ChatMessage.UserRole, text));
        ChatInput = "";
        IsBusy = true;
        StatusMessage = "Thinking\u2026";
        try
        {
            var reply = await _ai.ChatAsync(SystemPrompt, Messages.ToList(), ct);
            Messages.Add(new ChatMessage(ChatMessage.AssistantRole, reply));
            ParseActionsFrom(reply);
            StatusMessage = PendingActions.Count > 0
                ? "Claude proposed " + PendingActions.Count + " action(s) — review them on the right."
                : "";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage(ChatMessage.AssistantRole, "[Error] " + ex.Message));
            StatusMessage = "Chat failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ParseActionsFrom(string reply)
    {
        var match = JsonBlockRx.Match(reply);
        if (!match.Success) return;

        ProposedActions? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ProposedActions>(
                match.Groups[1].Value,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return;
        }
        if (parsed?.Actions is null) return;

        foreach (var pa in parsed.Actions)
        {
            var kind = pa.Kind?.ToLowerInvariant() switch
            {
                "create_file" => AgentActionKind.CreateFile,
                "create_folder" => AgentActionKind.CreateFolder,
                "move" => AgentActionKind.Move,
                _ => (AgentActionKind?)null,
            };
            if (kind is null) continue;

            var action = new AgentAction
            {
                Kind = kind.Value,
                Path = pa.Path ?? "",
                DestinationPath = pa.DestinationPath ?? "",
                Content = pa.Content ?? "",
            };
            var v = _agent.Validate(action);
            PendingActions.Add(new AgentActionRow(
                action, _agent.Describe(action), v.IsValid,
                v.IsValid ? "Ready" : "Blocked: " + v.Message));
        }
        OnPropertyChanged(nameof(HasPendingActions));
    }

    [RelayCommand]
    private async Task ExecuteActionsAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        var ready = PendingActions.Where(r => r.IsValid && r.Status != "Done").ToList();
        if (ready.Count == 0)
        {
            StatusMessage = "No valid pending actions to execute.";
            return;
        }

        IsBusy = true;
        try
        {
            foreach (var row in ready)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await _agent.ExecuteAsync(row.Action, ct);
                    row.Status = "Done";
                }
                catch (Exception ex)
                {
                    row.Status = "Failed: " + ex.Message;
                }
            }
            RefreshHistory();
            StatusMessage = "Executed pending actions.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearActions()
    {
        PendingActions.Clear();
        OnPropertyChanged(nameof(HasPendingActions));
        StatusMessage = "Cleared proposed actions.";
    }

    [RelayCommand]
    private async Task UndoLastAsync(CancellationToken ct)
    {
        if (!_agent.CanUndo)
        {
            StatusMessage = "Nothing to undo.";
            return;
        }
        await _agent.UndoLastAsync(ct);
        RefreshHistory();
        StatusMessage = "Last action undone.";
    }

    private void RefreshHistory()
    {
        ActionHistory.Clear();
        foreach (var h in _agent.UndoHistory) ActionHistory.Add(h);
        OnPropertyChanged(nameof(CanUndo));
    }

    [RelayCommand]
    private async Task SaveLastResponseAsync()
    {
        var last = Messages.LastOrDefault(m => m.IsAssistant);
        if (last is null)
        {
            StatusMessage = "No response to save yet.";
            return;
        }
        var firstLine = last.Text.Split('\n')[0].Trim();
        var title = firstLine.Length > 60 ? firstLine[..60] : firstLine;
        await _noteStore.AddAsync(new Note
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Note" : title,
            Body = last.Text,
        });
        await LoadNotesAsync();
        StatusMessage = "Saved to notes.";
    }

    [RelayCommand]
    private async Task DeleteNoteAsync()
    {
        if (SelectedNote is null)
        {
            StatusMessage = "Select a note to delete.";
            return;
        }
        await _noteStore.RemoveAsync(SelectedNote.Id);
        SelectedNote = null;
        await LoadNotesAsync();
        StatusMessage = "Note deleted.";
    }

    private sealed class ProposedActions
    {
        public List<ProposedAction>? Actions { get; set; }
    }

    private sealed class ProposedAction
    {
        public string? Kind { get; set; }
        public string? Path { get; set; }
        public string? DestinationPath { get; set; }
        public string? Content { get; set; }
    }
}
