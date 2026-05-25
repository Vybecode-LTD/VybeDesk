using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Threading;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using ClaudePM.Services.Ai;
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
               "create_folder, edit_file, move). Always include conversational " +
               "text in your reply; never end a turn with only tool_use blocks.";
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

        string active;
        if (IsAllProjects(ActiveProject))
        {
            var lines = Projects
                .Where(p => !IsAllProjects(p))
                .Select(p => "- " + p.Name + ": " + p.FolderPath);
            active = "ALL PROJECTS mode — you have access to every registered project:\n" +
                     string.Join("\n", lines);
        }
        else
        {
            active = ActiveProject is null || string.IsNullOrWhiteSpace(ActiveProject.FolderPath)
                ? "(no active project selected)"
                : "Name: " + ActiveProject.Name + "\n" +
                  "Folder: " + ActiveProject.FolderPath;
        }

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
    private readonly IActiveProjectContext _activeProjectContext;

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

    /// <summary>
    /// Per-project conversation snapshots. When the user switches the active
    /// project, the current conversation is stashed here (keyed by project
    /// ID) and the target project's snapshot is restored — or a blank
    /// conversation starts if none exists. This keeps each project's chat
    /// thread isolated so switching back and forth doesn't mix context.
    /// </summary>
    private readonly Dictionary<string, ProjectConversation> _conversations = new();

    private sealed class ProjectConversation
    {
        public List<AgentTurn> History { get; } = new();
        public List<NotebookMessage> Messages { get; } = new();
        public List<AgentContentBlock> PendingReadResults { get; } = new();
        public List<AgentActionRow> PendingActions { get; } = new();
    }

    private static readonly Project AllProjectsSentinel = new()
    {
        Id = Guid.Empty,
        Name = "All projects",
        FolderPath = "(all)",
    };

    private bool IsAllProjects(Project? p) => p is not null && p.Id == Guid.Empty;

    public override string Title => "Notebook";
    public override string Glyph => "\U0001F4D3";
    public override string Description =>
        "Ask the AI for advice, save notes, and let it take scoped filesystem actions.";

    public ObservableCollection<NotebookMessage> Messages { get; } = new();
    public ObservableCollection<AgentActionRow> PendingActions { get; } = new();
    public ObservableCollection<string> ActionHistory { get; } = new();
    public ObservableCollection<Note> Notes { get; } = new();
    public ObservableCollection<Project> Projects { get; } = new();
    public ObservableCollection<ScopeProjectRow> ScopeRows { get; } = new();

    [ObservableProperty] private string _chatInput = "";
    [ObservableProperty] private Note? _selectedNote;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScopedRootsText))]
    private Project? _activeProject;

    /// <summary>Derived: the folder(s) the agent is currently scoped to.</summary>
    public string ScopedRootsText =>
        ActiveProject is null
            ? "No project selected — file actions will be blocked."
            : IsAllProjects(ActiveProject)
                ? string.Join("\n", Projects
                    .Where(p => !IsAllProjects(p))
                    .Select(p => p.Name + ": " + p.FolderPath))
                : ActiveProject.FolderPath;

    private void RebuildScopeRows()
    {
        ScopeRows.Clear();
        if (ActiveProject is null) return;

        if (IsAllProjects(ActiveProject))
        {
            foreach (var p in Projects.Where(p => !IsAllProjects(p)))
                ScopeRows.Add(new ScopeProjectRow(p.Name, p.FolderPath));
        }
        else if (!string.IsNullOrWhiteSpace(ActiveProject.FolderPath))
        {
            ScopeRows.Add(new ScopeProjectRow(ActiveProject.Name, ActiveProject.FolderPath));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;
    // Backed properties (rather than `=> _agent.CanUndo`) so the
    // source-generator-emitted setter fires PropertyChanged when the value
    // changes — derived-property notifications via manual
    // OnPropertyChanged(nameof(CanX)) worked for the Undo direction but
    // intermittently failed to refresh the button bindings on the Redo
    // direction. Setting these as plain bool fields with [ObservableProperty]
    // makes the change notification path identical to every other VM
    // property in the app and removes the ambiguity.
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private bool _canRedo;
    public bool HasPendingActions => PendingActions.Count > 0;

    public NotebookViewModel(
        IAiService ai, INoteStore noteStore, IProjectStore projects,
        IAgentActionService agent, IClipboardService clipboard,
        IActiveProjectContext activeProjectContext)
    {
        _ai = ai;
        _noteStore = noteStore;
        _projects = projects;
        _agent = agent;
        _clipboard = clipboard;
        _activeProjectContext = activeProjectContext;
        _projects.Changed += OnProjectsChanged;
        // M3 #10 Phase B: undo history is now persisted in SQLite. Re-bind
        // the side-panel list and the Undo Last button whenever the store
        // changes (executed action / undone action / project switch).
        _agent.RecentActionsChanged += OnAgentRecentActionsChanged;
        _ = InitializeAsync();
    }

    private void OnAgentRecentActionsChanged()
        => Dispatcher.UIThread.Post(RefreshHistory);

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
        var withFolders = all.Where(p => !string.IsNullOrWhiteSpace(p.FolderPath)).ToList();
        if (withFolders.Count > 0)
            Projects.Add(AllProjectsSentinel);
        foreach (var p in withFolders)
            Projects.Add(p);

        ActiveProject = (keepId is not null
            ? Projects.FirstOrDefault(p => p.Id == keepId)
            : null) ?? Projects.FirstOrDefault();
    }

    partial void OnActiveProjectChanged(Project? oldValue, Project? newValue)
    {
        SaveConversation(oldValue);
        RestoreConversation(newValue);

        if (IsAllProjects(newValue))
        {
            var allRoots = Projects
                .Where(p => !IsAllProjects(p) && !string.IsNullOrWhiteSpace(p.FolderPath))
                .Select(p => p.FolderPath)
                .ToArray();
            _agent.SetScopedRoots(allRoots);
            _agent.SetActiveProject(null);
            _activeProjectContext.SetCurrent(null);
        }
        else
        {
            var roots = newValue is null || string.IsNullOrWhiteSpace(newValue.FolderPath)
                ? Array.Empty<string>()
                : new[] { newValue.FolderPath };
            _agent.SetScopedRoots(roots);
            _agent.SetActiveProject(newValue?.Id);
            _activeProjectContext.SetCurrent(newValue);
        }
        RebuildScopeRows();
    }

    private static string ConversationKey(Project p) => p.Id.ToString();

    private void SaveConversation(Project? project)
    {
        if (project is null) return;
        var snap = new ProjectConversation();
        snap.History.AddRange(_history);
        snap.Messages.AddRange(Messages);
        snap.PendingReadResults.AddRange(_pendingReadResults);
        snap.PendingActions.AddRange(PendingActions);
        _conversations[ConversationKey(project)] = snap;
    }

    private void RestoreConversation(Project? project)
    {
        _history.Clear();
        _pendingReadResults.Clear();
        Messages.Clear();
        PendingActions.Clear();
        ChatInput = "";
        StatusMessage = "";

        if (project is not null &&
            _conversations.TryGetValue(ConversationKey(project), out var snap))
        {
            _history.AddRange(snap.History);
            foreach (var m in snap.Messages) Messages.Add(m);
            _pendingReadResults.AddRange(snap.PendingReadResults);
            foreach (var a in snap.PendingActions) PendingActions.Add(a);
        }
        OnPropertyChanged(nameof(HasPendingActions));
    }

    private async Task LoadNotesAsync()
    {
        var all = await _noteStore.GetAllAsync();
        Notes.Clear();
        foreach (var n in all) Notes.Add(n);
    }

    /// <summary>
    /// Discard all in-memory conversation state — agent history (so the
    /// next API call starts clean), UI message bubbles, pending action
    /// proposals, the pending read-result buffer, and the chat input.
    /// Notes and the persisted agent-action log (which feeds
    /// ActionHistory and Undo Last) are NOT touched — those are records /
    /// cross-conversation state, not chat state.
    ///
    /// Used by <see cref="INotebookOpener"/> when an outside caller hands
    /// in a focused prompt (today: Documentation's "Apply with AI"
    /// buttons). The alternative — appending to whatever state was there
    /// before — breaks the Anthropic tool_use protocol whenever an earlier
    /// assistant turn left unresolved tool_use blocks (every tool_use
    /// REQUIRES a tool_result in the immediately-following message). A
    /// clean reset is also the right semantic: "apply these fixes" is a
    /// focused new task, not a continuation of the prior conversation.
    ///
    /// Bails out if a chat call is in flight so we don't yank state out
    /// from under the streaming response handler.
    /// </summary>
    public void BeginFreshConversation()
    {
        if (IsBusy) return;
        _history.Clear();
        _pendingReadResults.Clear();
        Messages.Clear();
        PendingActions.Clear();
        OnPropertyChanged(nameof(HasPendingActions));
        ChatInput = "";
        StatusMessage = "";
        if (ActiveProject is not null)
            _conversations.Remove(ActiveProject.Id.ToString());
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
            // .NET wraps low-level transport / serialization failures in
            // generic outer exceptions like "Error while copying content to
            // a stream." — the actual cause (TLS reset, JSON serialization
            // failure, malformed history block, etc) is in InnerException.
            // Unwrap the chain so the user sees something diagnostic.
            var cause = ex;
            while (cause.InnerException is not null) cause = cause.InnerException;
            var detail = ReferenceEquals(cause, ex)
                ? ex.Message
                : ex.Message + " — " + cause.GetType().Name + ": " + cause.Message;
            Messages.Add(new NotebookMessage(ChatMessage.AssistantRole, "[Error] " + detail));
            StatusMessage = "Chat failed: " + detail;
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
        AiUsage cumulativeUsage = AiUsage.Empty;
        string resolvedModel = "";

        try
        {
        bool anyTextThisCall = false;
        for (int iter = 0; ; iter++)
        {
            // No iteration cap — the Cancel button is the brake.
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
                usage => Dispatcher.UIThread.Post(() => bubble.StreamingUsage = usage),
                ct);

            if (response.Model.Length > 0) resolvedModel = response.Model;

            _history.Add(new AgentTurn
            {
                Role = AgentTurn.AssistantRole,
                Content = response.Blocks.ToList(),
            });

            cumulativeUsage = new AiUsage(
                cumulativeUsage.InputTokens + response.Usage.InputTokens,
                cumulativeUsage.OutputTokens + response.Usage.OutputTokens,
                cumulativeUsage.CacheCreationInputTokens + response.Usage.CacheCreationInputTokens,
                cumulativeUsage.CacheReadInputTokens + response.Usage.CacheReadInputTokens);

            // Use response.TextOutput (synchronous, race-free) rather than
            // bubble.Text.Length — text deltas are posted to the dispatcher
            // and may not have been applied to bubble.Text by the time we
            // hit this check.
            if (response.TextOutput.Length > 0) anyTextThisCall = true;

            // End of conversation — Claude didn't ask for more tool results.
            if (!response.WantsToolResults)
            {
                StatusMessage = anyTextThisCall ? "" : "(no response)";
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
            bubble.Usage = cumulativeUsage;
            bubble.StreamingUsage = null;
            if (resolvedModel.Length > 0 && cumulativeUsage.TotalTokens > 0)
                bubble.CostEstimate = AnthropicChatService.EstimateCost(resolvedModel, cumulativeUsage);
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
            "edit_file"     => "Proposed: Edit file " + path,
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
            "edit_file" => new AgentAction
            {
                Kind = AgentActionKind.EditFile,
                Path = ReadString(input, "path"),
                OldString = ReadString(input, "old_string"),
                NewString = ReadString(input, "new_string"),
                ReplaceAll = ReadBool(input, "replace_all"),
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

    private static bool ReadBool(JsonElement input, string name)
    {
        if (input.ValueKind != JsonValueKind.Object) return false;
        if (!input.TryGetProperty(name, out var prop)) return false;
        return prop.ValueKind == JsonValueKind.True;
    }

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

            // RefreshHistory is driven by IAgentActionService.RecentActionsChanged
            // (post-M3 #10 Phase B): every Execute that hits the persistent
            // log raises the event, which we route back to RefreshHistory on
            // the UI thread. No explicit call needed here.

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
        // RefreshHistory is driven by RecentActionsChanged — see ExecuteActionsAsync.
        StatusMessage = "Last action undone.";
    }

    [RelayCommand]
    private async Task RedoLastAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (!_agent.CanRedo)
        {
            StatusMessage = "Nothing to redo.";
            return;
        }
        try
        {
            await _agent.RedoLastAsync(ct);
            StatusMessage = "Last action redone.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Redo failed: " + ex.Message;
        }
    }

    /// <summary>
    /// Re-bind the side-panel list and the Undo Last button from the
    /// agent's cached snapshot. Newest-first, with a [Undone] suffix on
    /// already-reversed entries so the user sees the trail rather than a
    /// disappearing item. Called on the UI thread (via
    /// <see cref="OnAgentRecentActionsChanged"/>) so the
    /// ObservableCollection mutation is safe.
    /// </summary>
    private void RefreshHistory()
    {
        ActionHistory.Clear();
        foreach (var entry in _agent.RecentActions)
        {
            var label = entry.Status == AgentActionLogStatus.Undone
                ? entry.Description + "  [Undone]"
                : entry.Description;
            ActionHistory.Add(label);
        }
        // Push the agent's bool state through the [ObservableProperty]
        // setters so the button bindings receive a guaranteed PropertyChanged.
        CanUndo = _agent.CanUndo;
        CanRedo = _agent.CanRedo;
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
        new("edit_file",
            "Edit an EXISTING file by replacing exact text. Path must be inside a scoped project root and the file must already exist (use create_file for new files). The old_string MUST appear in the file; with replace_all=false (the default) it must appear EXACTLY ONCE — include enough surrounding context to make it unique, or set replace_all=true to replace every occurrence. The new_string may be empty (= deletion of the matched text). Read the file with read_file first so your old_string matches byte-for-byte (whitespace included).",
            ParseSchema("""
                {
                  "type": "object",
                  "properties": {
                    "path":        { "type": "string", "description": "Absolute file path inside a scoped project root. File must already exist." },
                    "old_string":  { "type": "string", "description": "Exact text to find. Must appear in the file (exactly once unless replace_all=true)." },
                    "new_string":  { "type": "string", "description": "Replacement text. May be empty to delete the matched text." },
                    "replace_all": { "type": "boolean", "description": "If true, replace every occurrence; if false (default), require exactly one." }
                  },
                  "required": ["path", "old_string", "new_string"]
                }
                """)),
    };

    private static JsonElement ParseSchema(string json)
        => JsonSerializer.Deserialize<JsonElement>(json);
}

public sealed partial class ScopeProjectRow : ObservableObject
{
    public string ProjectName { get; }
    public string FolderPath { get; }

    [ObservableProperty] private bool _isPathVisible;

    public ScopeProjectRow(string projectName, string folderPath)
    {
        ProjectName = projectName;
        FolderPath = folderPath;
    }

    [RelayCommand]
    private void TogglePath() => IsPathVisible = !IsPathVisible;

    [RelayCommand]
    private void OpenInExplorer()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = FolderPath,
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
