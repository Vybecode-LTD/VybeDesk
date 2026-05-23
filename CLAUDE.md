# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
Real Anthropic streaming + tool_use for the Notebook, plus a dedicated
Projects tab with CRUD — sixth and seventh v1.1 polish items bundled
together because the Notebook flow is only meaningfully testable once
users can register a real project root through the UI. Net new tab
count: 7 → 8 (Home / Projects / Documentation / Prompts / Session
Builder / Notebook / Skill Library / Settings).

**AI client (`AnthropicChatService`):** previously a single non-
streaming POST that asked Claude to append a structured-JSON action
proposal in a fenced code block. Now the file:
- POSTs `stream: true` with `tools: [...]` to `/v1/messages` and reads
  the SSE response with `HttpCompletionOption.ResponseHeadersRead`.
- Dispatches the standard event types (`content_block_start/delta/
  stop`, `message_delta`, `message_stop`, `error`); text deltas fire
  through an `onTextDelta` callback as they arrive, `tool_use` input
  JSON is reassembled from `input_json_delta` fragments.
- Accepts an injectable `HttpClient` for tests; serializes outgoing
  payloads as `JsonObject` so tool input schemas pass through verbatim.
- Keeps the existing non-streaming `CompleteAsync` / `ChatAsync`
  helpers for DocReconciliation, PromptManager, and SessionBuilder
  callers — they have no reason to stream.

**New Core types** for richer agent conversations: `AgentTurn` /
`AgentContentBlock` (Text / ToolUse / ToolResult) so a turn can carry
mixed blocks, `AgentTool` (name + description + JSON Schema input),
`AgentChatResponse` (blocks + stop_reason + a `WantsToolResults`
shortcut for `stop_reason: "tool_use"`). `IAiService.AgentChatAsync`
exposes the new streaming + tool-using path.

**Notebook (`NotebookViewModel`):** old JSON-regex shim removed. The
VM now holds a dual conversation: an `ObservableCollection<NotebookMessage>`
for the chat UI (new VM class with an *observable* `Text` so the
streaming row updates live via `Dispatcher.UIThread.Post`) and a
`List<AgentTurn>` as the authoritative agent history. `SendAsync`
calls `AgentChatAsync` with the three real tools — `create_file`,
`create_folder`, `move` — each declared with an explicit JSON Schema.
`tool_use` blocks become `AgentActionRow`s in the existing
"Proposed actions" pane (now carrying their `ToolUseId`); Execute
runs each through `AgentActionService` and posts a follow-up user
turn of `tool_result` blocks (with `is_error=true` on failure),
then re-calls `AgentChatAsync` to let Claude continue. `Clear`
injects synthetic `is_error=true` tool_result blocks so the
conversation history stays consistent if the user cancels.

**Projects tab:** new `ProjectsViewModel` + `ProjectsView` with full
CRUD — list left, editor right (Name / Description / FolderPath +
Browse / Status), wired through `IFilePickerService`. `IProjectStore`
gained an `event Action? Changed` that fires after Add / Update /
Remove; Home, Documentation, and Notebook subscribe and reload (via
`Dispatcher.UIThread.Post` for thread safety), so adding a project in
the Projects tab live-updates the Documentation project picker and
the Notebook "Sandbox roots" + scoped-roots configuration without a
restart. The Notebook tool_use flow is now end-to-end usable without
touching the database directly.

Five new `AnthropicChatServiceTests` exercise the SSE path against
canned bytes via a fake `HttpMessageHandler`: text deltas streamed via
callback, tool_use input reassembled across multiple fragments,
request body shape (`stream=true` + `tools[]` + JSON Schema
passthrough), `error` event throwing `InvalidOperationException`, and
the non-streaming `CompleteAsync` still working. Total tests: 22 → 27,
all green; build clean. Smoked end-to-end: registered a real project
through the new Projects tab, asked the Notebook to create a file
inside it, watched streaming text + tool_use, executed, file landed
on disk, Claude continued, Undo Last reverted. Next v1.1 candidate
on the KICKOFF list: Git-aware staleness detection in Documentation
(Git mtime / log-based signals in the structural pass). NOTE: the
handoff skill is named `cc-handoff` ("claude" is reserved in skill
names).

## Overview
ClaudePM is a cross-platform desktop app that acts as an AI-driven project
manager for Claude-based work. It helps keep project documentation reconciled,
manages reusable prompts, builds Claude Code repos from claude.ai web sessions,
and provides an AI notebook that can take file/folder actions. Currently
single-user; designed so it can become a commercial product.

## Architecture
Layered, strict one-directional dependencies (Core ← Services ← App):
- **ClaudePM.Core** — domain models, interfaces. No framework dependencies.
- **ClaudePM.Services** — AI client (Microsoft.Extensions.AI / Anthropic SDK),
  file scanning, doc analysis/reconciliation, repo generation, prompt store.
- **ClaudePM.App** — Avalonia 11 UI (Views/ViewModels), DI composition root in
  App.axaml.cs, system-tray integration.
- **ClaudePM.Tests** — xUnit + NSubstitute.

MVVM via CommunityToolkit.Mvvm source generators. Compiled bindings (x:DataType)
everywhere. No INotifyPropertyChanged by hand; no new-ing ViewModels in
code-behind.

### Modules
1. Documentation Manager — scan/list/analyze project docs. Structural pass
   (local, no AI) + semantic pass (AI, doc-vs-doc only in v1). Emits a
   reconciliation report + a ready-to-paste Claude Code fix prompt; can draft
   missing docs via the preview/execute/undo flow.
2. Prompt Manager — store/tag/categorize prompts (SQLite + FTS5); `{{variable}}`
   templates; AI redesigns prompts (shown as diff, versioned); AI generates new
   prompts from a description.
3. Claude -> Claude Code Session Builder — wizard collects description,
   transcripts, and files; generates a HANDOFF PACKAGE (organized folder,
   CLAUDE.md, README, .gitignore, staged files) + a kickoff prompt. Does NOT
   write the app's code itself — Claude Code does that.
4. AI Notebook — conversational advice; saves notes; performs filesystem
   actions (create/move files & folders) via tool-calling, gated by
   preview/execute/undo and scoped to registered project roots.
5. Skill Library Manager — browse/edit/dedupe/validate `.skill` files; export
   valid `.skill` files.

## Build, Test, Run
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project ClaudePM.App`
- Publish (per platform): see `dotnet-installer-publishing` skill.

## Conventions
- ALWAYS keep this file's "Last Completed Task" current at the end of a session.
- All AI calls go through an `IChatClient` abstraction — never call the SDK
  directly from a ViewModel.
- Any AI-initiated filesystem action MUST go through the Preview → Execute →
  Undo pattern. No direct writes from an agent action.
- API keys are stored via OS-native secure storage (DPAPI / Keychain /
  libsecret). NEVER write keys to disk in plaintext or into this file.
- Long-running work (scans, generation) runs off the UI thread.
- Naming: Views end in `View`, ViewModels in `ViewModel`, services in `Service`.

## Gotchas / Do Not Touch
- CommunityToolkit.Mvvm source generators require `partial` classes — missing
  `partial` silently breaks `[ObservableProperty]` / `[RelayCommand]`.
- Agent filesystem actions are scoped to user-configured project roots only;
  do not widen this scope without an explicit decision.

## Reference Docs
See @SPEC.md for the full feature and architecture spec.
See @KICKOFF.md for the first-task prompt (verify the build first).
<add further @imports here as deeper docs are created next to their code>
