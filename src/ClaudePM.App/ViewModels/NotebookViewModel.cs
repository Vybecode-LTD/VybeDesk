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
    /// <summary>
    /// The notebook assistant's full system prompt — the "constitution"
    /// document the user authored at <c>Assets/notebook-system-prompt.md</c>.
    /// Loaded once at type init from disk so the prompt can be edited in
    /// place (next to the binary) without recompiling. Falls back to a
    /// minimal inline prompt if the asset can't be read.
    /// </summary>
    private static readonly string SystemPromptTemplate = LoadSystemPromptTemplate();

    private static string LoadSystemPromptTemplate()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory, "Assets", "notebook-system-prompt.md");
        try
        {
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        catch { /* swallow — fall back */ }

        // Minimal fallback so the Notebook still works even if the asset is
        // missing (e.g. a broken publish).
        return "You are an assistant inside a desktop project-manager app. " +
               "You have read-only tools (read_file, list_directory, " +
               "auto-executed) and approval-gated tools (create_file, " +
               "create_folder, move). Always include conversational text in " +
               "your reply; never end a turn with only tool_use blocks.";
    }

    private static readonly HashSet<string> ReadOnlyTools = new(StringComparer.Ordinal)
        { "read_file", "list_directory" };

    /// <summary>
    /// Build the system prompt for the current turn by substituting runtime
    /// context (registered roots, active project, provided files) into the
    /// template loaded from <c>Assets/notebook-system-prompt.md</c>.
    /// </summary>
    private string BuildSystemPrompt()
    {
        var roots = _agent.ScopedRoots.Count == 0
            ? "(none — no project folders registered; file actions will be blocked)"
            : string.Join("\n", _agent.ScopedRoots);

        var active = ActiveProject is null || string.IsNullOrWhiteSpace(ActiveProject.FolderPath)
            ? "(no active project selected)"
            : "Name: " + ActiveProject.Name + "\n" +
              "Folder: " + ActiveProject.FolderPath;

        return SystemPromptTemplate
            .Replace("{{scoped_roots}}", roots)
            .Replace("{{active_project}}", active)
            .Replace("{{provided_files}}", "(none in this turn)");
    }

    private readonly IAiService _ai;
    private readonly INoteStore _noteStore;
    private readonly IProjectStore _projects;
    private readonly IAgentActionService _agent;
    private readonly IClipboardService _clipboard;

    /// <summary>
    /// Authoritative agent conversation history (user/assistant turns with
    /// rich content blocks). The UI <see cref="Messages"/> collection mirrors
    /// only the prose; tool_use / tool_result blocks live exclusively here so
    /// Claude sees a consistent transcript across turns.
    /// </summary>
    private readonly List<AgentTurn> _history = new();

    /// <summary>
    /// Tool_results from read-only tools auto-executed in the current
    /// assistant turn, stashed while we wait for the user to approve any
    /// write tools in the same turn. Drained (along with write results) when
    /// the user clicks Execute, or (without action follow-through) when the
    /// user clicks Clear.
    /// </summary>
    private readonly List<AgentContentBlock> _pendingReadResults = new();

    public override string Title => "Notebook";
    public override string Glyph => "\U0001F4D3";
    public override string Description =>
        "Ask the AI for advice, save notes, and let it take scoped filesystem actions.";

    public ObservableCollection<NotebookMessage> Messages { get; } = new();
    public ObservableCollection<AgentActionRow> PendingActions { get; } = new();
    public ObservableCollection<string> ActionHistory { get; } = new();
    public ObservableCollection<Note> Notes { get; } = new();
    public ObservableCollection<Project> Projects { get; } = new();

    [ObservableProperty] private string _chatInput = "";
    [ObservableProperty] private Note? _selectedNote;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScopedRootsText))]
    private Project? _activeProject;

    /// <summary>Derived: the folder the agent is currently scoped to (or a hint when none).</summary>
    public string ScopedRootsText =>
        ActiveProject is null || string.IsNullOrWhiteSpace(ActiveProject.FolderPath)
            ? "No project selected — file actions will be blocked."
            : ActiveProject.FolderPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;
    public bool CanUndo => _agent.CanUndo;
    public bool HasPendingActions => PendingActions.Count > 0;

    public NotebookViewModel(
        IAiService ai, INoteStore noteStore, IProjectStore projects,
        IAgentActionService agent, IClipboardService clipboard)
    {
        _ai = ai;
        _noteStore = noteStore;
        _projects = projects;
        _agent = agent;
        _clipboard = clipboard;
        _projects.Changed += OnProjectsChanged;
        _ = InitializeAsync();
    }

    [RelayCommand]
    private async Task CopyMessageAsync(NotebookMessage? message)
    {
        if (message is null || string.IsNullOrEmpty(message.Text)) return;
        if (await _clipboard.SetTextAsync(message.Text))
            StatusMessage = "Copied to clipboard.";
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
        var keepId = ActiveProject?.Id;
        var all = await _projects.GetAllAsync();

        Projects.Clear();
        foreach (var p in all.Where(p => !string.IsNullOrWhiteSpace(p.FolderPath)))
            Projects.Add(p);

        // Preserve the selection if it still exists; otherwise pick the first
        // project (or leave null when there are none).
        ActiveProject = (keepId is not null
            ? Projects.FirstOrDefault(p => p.Id == keepId)
            : null) ?? Projects.FirstOrDefault();
    }

    partial void OnActiveProjectChanged(Project? value)
    {
        var roots = value is null || string.IsNullOrWhiteSpace(value.FolderPath)
            ? Array.Empty<string>()
            : new[] { value.FolderPath };
        _agent.SetScopedRoots(roots);
    }

    private async Task LoadNotesAsync()
    {
        var all = await _noteStore.GetAllAsync();
        Notes.Clear();
        foreach (var n in all) Notes.Add(n);
    }

    // ─── send → stream → propose actions ────────────────────────────────

    [RelayCommand(IncludeCancelCommand = true)]
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
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
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
    /// Run assistant turns against the current <see cref="_history"/> in a
    /// loop. Plain text or all-read tool calls flow without user intervention
    /// (reads auto-execute, results post back, Claude continues). The loop
    /// stops on end_turn, on any write tool that needs approval, or when the
    /// user cancels.
    /// </summary>
    private async Task RunAssistantTurnAsync(CancellationToken ct)
    {
        // ONE bubble per user turn — chips and text from every auto-loop
        // iteration accumulate into it so the user sees a single coherent
        // assistant reply rather than a chain of empty stub bubbles.
        var bubble = new NotebookMessage(ChatMessage.AssistantRole, "")
        {
            IsStreaming = true,
        };
        Messages.Add(bubble);

        try
        {

        for (int iter = 0; ; iter++)
        {
            // No iteration cap — the Cancel button is the brake. Track text
            // added per-iteration so we can detect Claude going silent.
            ct.ThrowIfCancellationRequested();

            var preTextLength = bubble.Text.Length;
            var insertedBreak = false;

            var response = await _ai.AgentChatAsync(
                BuildSystemPrompt(),
                _history,
                Tools,
                chunk => Dispatcher.UIThread.Post(() =>
                {
                    if (!insertedBreak && preTextLength > 0)
                    {
                        bubble.Text += "\n\n";
                        insertedBreak = true;
                    }
                    bubble.Text += chunk;
                }),
                ct);

            _history.Add(new AgentTurn
            {
                Role = AgentTurn.AssistantRole,
                Content = response.Blocks.ToList(),
            });

            var addedTextThisIter = bubble.Text.Length > preTextLength;

            // End of conversation — Claude didn't ask for more tool results.
            if (!response.WantsToolResults)
            {
                StatusMessage = "";
                if (!addedTextThisIter)
                {
                    // Claude ended the turn without producing fresh prose
                    // in this iteration. Surface it so the user isn't left
                    // staring at an unchanged intro wondering what happened.
                    var note = "*(Claude ended without producing a final " +
                               "response. Stop reason: " + response.StopReason +
                               ". Ask again to continue.)*";
                    bubble.Text = string.IsNullOrEmpty(bubble.Text)
                        ? note
                        : bubble.Text + "\n\n" + note;
                }
                return;
            }

            var reads = response.ToolUses.Where(t => ReadOnlyTools.Contains(t.Name)).ToList();
            var writes = response.ToolUses.Where(t => !ReadOnlyTools.Contains(t.Name)).ToList();

            // Auto-execute read-only tools; their chips stack at the top of
            // the bubble (same NotebookMessage across iterations).
            var readResultBlocks = new List<AgentContentBlock>();
            foreach (var r in reads)
            {
                var block = ExecuteReadOnlyTool(r);
                readResultBlocks.Add(block);
                bubble.Activities.Add(new ToolActivity(
                    DescribeReadActivity(r, block),
                    !block.IsError));
            }

            if (writes.Count == 0)
            {
                _history.Add(new AgentTurn
                {
                    Role = AgentTurn.UserRole,
                    Content = readResultBlocks,
                });
                StatusMessage = "Inspected " + reads.Count
                    + " resource(s) (iter " + (iter + 1) + ") — continuing…";
                continue;
            }

            foreach (var w in writes)
            {
                QueuePendingAction(w);
                bubble.Activities.Add(new ToolActivity(DescribeWriteActivity(w), true));
            }
            _pendingReadResults.AddRange(readResultBlocks);

            OnPropertyChanged(nameof(HasPendingActions));
            StatusMessage = "Claude proposed " + writes.Count
                + " action(s) — review on the right.";
            return;
        }

        // Unreachable — the for loop only exits via return.

        }
        finally
        {
            // Flip out of streaming mode so the view swaps to the markdown
            // renderer for the final, well-formed text.
            bubble.IsStreaming = false;
        }
    }

    /// <summary>
    /// Run one read-only tool (read_file / list_directory) and shape its
    /// outcome as a tool_result block. Scope confinement lives in
    /// AgentActionService; errors surface as is_error=true tool_results so
    /// Claude can recover.
    /// </summary>
    private static string DescribeReadActivity(AgentToolUseBlock toolUse, AgentToolResultBlock result)
    {
        var path = ReadString(toolUse.Input, "path");
        var label = ShortenPath(path);
        var verb = toolUse.Name switch
        {
            "read_file" => result.IsError ? "Couldn't read" : "Read",
            "list_directory" => result.IsError ? "Couldn't list" : "Listed",
            _ => toolUse.Name,
        };
        return result.IsError
            ? verb + " " + label + " — " + result.Content
            : verb + " " + label;
    }

    private static string DescribeWriteActivity(AgentToolUseBlock toolUse)
    {
        var path = ShortenPath(ReadString(toolUse.Input, "path"));
        return toolUse.Name switch
        {
            "create_file"   => "Proposed: Create file " + path,
            "create_folder" => "Proposed: Create folder " + path,
            "move"          => "Proposed: Move " + path + " → " +
                               ShortenPath(ReadString(toolUse.Input, "destination_path")),
            _ => "Proposed: " + toolUse.Name,
        };
    }

    private static string ShortenPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return "(empty)";
        var trimmed = fullPath.TrimEnd('\\', '/');
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    private AgentToolResultBlock ExecuteReadOnlyTool(AgentToolUseBlock toolUse)
    {
        switch (toolUse.Name)
        {
            case "read_file":
            {
                var path = ReadString(toolUse.Input, "path");
                var r = _agent.ReadFile(path);
                return r.Success
                    ? new AgentToolResultBlock(toolUse.Id, r.Content)
                    : new AgentToolResultBlock(toolUse.Id, r.ErrorMessage, IsError: true);
            }
            case "list_directory":
            {
                var path = ReadString(toolUse.Input, "path");
                var r = _agent.ListDirectory(path);
                return r.Success
                    ? new AgentToolResultBlock(toolUse.Id, string.Join("\n", r.Entries))
                    : new AgentToolResultBlock(toolUse.Id, r.ErrorMessage, IsError: true);
            }
            default:
                return new AgentToolResultBlock(toolUse.Id,
                    "Unknown read-only tool: " + toolUse.Name, IsError: true);
        }
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

    [RelayCommand(IncludeCancelCommand = true)]
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
            // Drain stashed read_file / list_directory results first — Claude
            // emitted them in the same assistant turn as the write tool_uses,
            // so all of them need to appear together in the follow-up user
            // turn or the conversation history breaks.
            var resultBlocks = new List<AgentContentBlock>(_pendingReadResults);
            _pendingReadResults.Clear();

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
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
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
        if (PendingActions.Count == 0 && _pendingReadResults.Count == 0) return;

        // Real results from already-executed reads + cancellation results for
        // queued writes — all of them have to be posted together so the
        // assistant's last tool_use turn is fully answered.
        var blocks = new List<AgentContentBlock>(_pendingReadResults);
        _pendingReadResults.Clear();
        foreach (var row in PendingActions)
            blocks.Add(new AgentToolResultBlock(
                row.ToolUseId, "User cancelled the action.", IsError: true));
        _history.Add(new AgentTurn { Role = AgentTurn.UserRole, Content = blocks });

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

    [RelayCommand]
    private async Task CopySelectedNoteAsync()
    {
        if (SelectedNote is null) return;
        if (await _clipboard.SetTextAsync(SelectedNote.Body))
            StatusMessage = "Copied note to clipboard.";
    }

    [RelayCommand]
    private void InsertNoteIntoChat()
    {
        if (SelectedNote is null) return;
        var prefix = "Reference (from saved note \"" + SelectedNote.Title + "\"):\n";
        var separator = "\n---\n\n";
        ChatInput = prefix + SelectedNote.Body + separator + ChatInput;
        StatusMessage = "Note prepended to your next message — edit and Send when ready.";
    }

    // ─── tool schemas exposed to Claude ────────────────────────────────

    private static readonly AgentTool[] Tools =
    {
        new("read_file",
            "Read the UTF-8 text contents of a file. Path must be inside a scoped project root. Content is truncated past ~50 KB with a marker.",
            ParseSchema("""
                {
                  "type": "object",
                  "properties": {
                    "path": { "type": "string", "description": "Absolute file path inside a scoped project root." }
                  },
                  "required": ["path"]
                }
                """)),
        new("list_directory",
            "List the immediate children of a directory (folders suffixed with '/'). Path must be inside a scoped project root. Truncated past 200 entries with a marker.",
            ParseSchema("""
                {
                  "type": "object",
                  "properties": {
                    "path": { "type": "string", "description": "Absolute directory path inside a scoped project root." }
                  },
                  "required": ["path"]
                }
                """)),
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
