using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Threading;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Module 4 — AI Notebook. Conversational advice, saved notes, and AI-proposed
/// filesystem actions gated by validate / execute / undo within scoped roots.
/// Uses Anthropic's streaming tool_use protocol: text appears live in the
/// chat as deltas arrive, tool calls land in the preview pane for user
/// confirmation, and tool_result blocks continue the conversation.
/// </summary>
public sealed partial class NotebookViewModel : PageViewModel
{
    private const string SystemPrompt =
        "You are an assistant inside a desktop project-manager app. Give helpful, " +
        "concise advice. When the user clearly wants you to take a filesystem " +
        "action (create a file, create a folder, or move a file or folder) use " +
        "the appropriate tool. Only use absolute paths inside the user's project " +
        "folders. If no filesystem operation is wanted, respond with text only.";

    private readonly IAiService _ai;
    private readonly INoteStore _noteStore;
    private readonly IProjectStore _projects;
    private readonly IAgentActionService _agent;

    /// <summary>
    /// Authoritative agent conversation history (user/assistant turns with
    /// rich content blocks). The UI <see cref="Messages"/> collection mirrors
    /// only the prose; tool_use / tool_result blocks live exclusively here so
    /// Claude sees a consistent transcript across turns.
    /// </summary>
    private readonly List<AgentTurn> _history = new();

    public override string Title => "Notebook";
    public override string Glyph => "\U0001F4D3";
    public override string Description =>
        "Ask the AI for advice, save notes, and let it take scoped filesystem actions.";

    public ObservableCollection<NotebookMessage> Messages { get; } = new();
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
        _projects.Changed += OnProjectsChanged;
        _ = InitializeAsync();
    }

    private void OnProjectsChanged()
        => Dispatcher.UIThread.Post(async () => await RefreshScopedRootsAsync());

    private async Task InitializeAsync()
    {
        await RefreshScopedRootsAsync();
        await LoadNotesAsync();
    }

    private async Task RefreshScopedRootsAsync()
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
    }

    private async Task LoadNotesAsync()
    {
        var all = await _noteStore.GetAllAsync();
        Notes.Clear();
        foreach (var n in all) Notes.Add(n);
    }

    // ─── send → stream → propose actions ────────────────────────────────

    [RelayCommand]
    private async Task SendAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        var text = ChatInput.Trim();
        if (text.Length == 0) return;

        Messages.Add(new NotebookMessage(ChatMessage.UserRole, text));
        _history.Add(AgentTurn.UserText(text));
        ChatInput = "";

        IsBusy = true;
        StatusMessage = "Thinking…";
        try
        {
            await RunAssistantTurnAsync(ct);
        }
        catch (Exception ex)
        {
            Messages.Add(new NotebookMessage(ChatMessage.AssistantRole, "[Error] " + ex.Message));
            StatusMessage = "Chat failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Run one assistant turn against the current <see cref="_history"/>:
    /// open a fresh chat row, stream text into it, and (if Claude wants
    /// tool results) surface the tool_use blocks as PendingActions.
    /// </summary>
    private async Task RunAssistantTurnAsync(CancellationToken ct)
    {
        var bubble = new NotebookMessage(ChatMessage.AssistantRole, "");
        Messages.Add(bubble);

        var response = await _ai.AgentChatAsync(
            SystemPrompt,
            _history,
            Tools,
            chunk => Dispatcher.UIThread.Post(() => bubble.Text += chunk),
            ct);

        // Stamp the full assistant turn (including any tool_use blocks) into
        // history so the next API call sees it.
        _history.Add(new AgentTurn
        {
            Role = AgentTurn.AssistantRole,
            Content = response.Blocks.ToList(),
        });

        // If the bubble is empty (Claude only emitted tool_use, no text) keep
        // a placeholder so the timeline still makes sense.
        if (string.IsNullOrEmpty(bubble.Text))
            bubble.Text = "(proposed " + response.ToolUses.Count + " action(s))";

        foreach (var toolUse in response.ToolUses)
            QueuePendingAction(toolUse);

        OnPropertyChanged(nameof(HasPendingActions));
        StatusMessage = response.WantsToolResults
            ? "Claude proposed " + response.ToolUses.Count + " action(s) — review on the right."
            : "";
    }

    private void QueuePendingAction(AgentToolUseBlock toolUse)
    {
        var action = ToAgentAction(toolUse);
        var validation = _agent.Validate(action);
        PendingActions.Add(new AgentActionRow(
            action,
            _agent.Describe(action),
            validation.IsValid,
            validation.IsValid ? "Ready" : "Blocked: " + validation.Message)
        {
            ToolUseId = toolUse.Id,
        });
    }

    private static AgentAction ToAgentAction(AgentToolUseBlock toolUse)
    {
        var input = toolUse.Input;
        return toolUse.Name switch
        {
            "create_file" => new AgentAction
            {
                Kind = AgentActionKind.CreateFile,
                Path = ReadString(input, "path"),
                Content = ReadString(input, "content"),
            },
            "create_folder" => new AgentAction
            {
                Kind = AgentActionKind.CreateFolder,
                Path = ReadString(input, "path"),
            },
            "move" => new AgentAction
            {
                Kind = AgentActionKind.Move,
                Path = ReadString(input, "path"),
                DestinationPath = ReadString(input, "destination_path"),
            },
            _ => new AgentAction { Kind = AgentActionKind.CreateFile, Path = "" },
        };
    }

    private static string ReadString(JsonElement input, string name)
        => input.ValueKind == JsonValueKind.Object &&
           input.TryGetProperty(name, out var prop) &&
           prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? ""
            : "";

    // ─── execute → tool_result → continue ──────────────────────────────

    [RelayCommand]
    private async Task ExecuteActionsAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        var ready = PendingActions.Where(r => r.Status != "Done").ToList();
        if (ready.Count == 0)
        {
            StatusMessage = "No pending actions to execute.";
            return;
        }

        IsBusy = true;
        try
        {
            var resultBlocks = new List<AgentContentBlock>();
            foreach (var row in ready)
            {
                ct.ThrowIfCancellationRequested();
                if (!row.IsValid)
                {
                    row.Status = "Blocked";
                    resultBlocks.Add(new AgentToolResultBlock(
                        row.ToolUseId,
                        "Blocked by policy: " + row.Description,
                        IsError: true));
                    continue;
                }
                try
                {
                    await _agent.ExecuteAsync(row.Action, ct);
                    row.Status = "Done";
                    resultBlocks.Add(new AgentToolResultBlock(
                        row.ToolUseId, "Action succeeded."));
                }
                catch (Exception ex)
                {
                    row.Status = "Failed: " + ex.Message;
                    resultBlocks.Add(new AgentToolResultBlock(
                        row.ToolUseId, ex.Message, IsError: true));
                }
            }

            RefreshHistory();

            // Send tool_result back as a user turn and let Claude continue.
            _history.Add(new AgentTurn { Role = AgentTurn.UserRole, Content = resultBlocks });
            PendingActions.Clear();
            OnPropertyChanged(nameof(HasPendingActions));

            StatusMessage = "Executed — waiting for Claude to continue…";
            await RunAssistantTurnAsync(ct);
        }
        catch (Exception ex)
        {
            Messages.Add(new NotebookMessage(ChatMessage.AssistantRole, "[Error] " + ex.Message));
            StatusMessage = "Continuation failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearActions()
    {
        if (PendingActions.Count == 0) return;

        // Synthesize tool_result blocks marked is_error so the conversation
        // history stays consistent — Claude will see the cancellations on the
        // next user message instead of an orphaned tool_use.
        var cancellations = PendingActions
            .Select(row => (AgentContentBlock)new AgentToolResultBlock(
                row.ToolUseId, "User cancelled the action.", IsError: true))
            .ToList();
        _history.Add(new AgentTurn { Role = AgentTurn.UserRole, Content = cancellations });

        PendingActions.Clear();
        OnPropertyChanged(nameof(HasPendingActions));
        StatusMessage = "Cancelled proposed actions.";
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

    // ─── notes ──────────────────────────────────────────────────────────

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

    // ─── tool schemas exposed to Claude ────────────────────────────────

    private static readonly AgentTool[] Tools =
    {
        new("create_file",
            "Create a new file at the given absolute path with the given UTF-8 text content. Path must be inside a scoped project root.",
            ParseSchema("""
                {
                  "type": "object",
                  "properties": {
                    "path":    { "type": "string", "description": "Absolute file path inside a scoped project root." },
                    "content": { "type": "string", "description": "Initial UTF-8 contents for the file." }
                  },
                  "required": ["path", "content"]
                }
                """)),
        new("create_folder",
            "Create a new directory at the given absolute path. Path must be inside a scoped project root.",
            ParseSchema("""
                {
                  "type": "object",
                  "properties": {
                    "path": { "type": "string", "description": "Absolute directory path inside a scoped project root." }
                  },
                  "required": ["path"]
                }
                """)),
        new("move",
            "Move (rename) a file or folder. Source and destination must both be inside a scoped project root.",
            ParseSchema("""
                {
                  "type": "object",
                  "properties": {
                    "path":             { "type": "string", "description": "Absolute source path (file or folder)." },
                    "destination_path": { "type": "string", "description": "Absolute destination path." }
                  },
                  "required": ["path", "destination_path"]
                }
                """)),
    };

    private static JsonElement ParseSchema(string json)
        => JsonSerializer.Deserialize<JsonElement>(json);
}
