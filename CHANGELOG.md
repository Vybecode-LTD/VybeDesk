# Changelog

> Reverse-chronological. Versions trail Git tags; commit hashes link to the
> work that landed each entry. Snapshot tag `AlphaV0.5.0` marks the end of
> Milestone 1.

## [v0.17] — 2026-05-24 — M2.5 Project Audit + UX bundle

The Documentation Manager gains a synthesis pass (`AuditAsync`) that reads
a signal-weighted bundle of project docs and produces a structured
`ProjectAuditReport` (design summary, roadmap items tagged complete /
incomplete / unknown, severity-ranked inconsistencies). Shipped together
with a real clipboard service + Copy buttons across the app, a Claude
model picker in Settings, and a major notes-section upgrade. (`31424a6`)

- **Added** `ProjectAuditReport`, `AuditRoadmapItem`, `AuditInconsistency`
  Core records. `IDocReconciliationService.AuditAsync` +
  `BuildAuditFixPrompt`.
- **Added** Full-pane "Audit Project" overlay in the Documentation tab —
  five sections (Design, all items, Completed, Incomplete, fix prompt,
  Inconsistencies) with a "Generate Fix Prompt" action wired to its own
  `AuditFixPrompt` state.
- **Added** `IClipboardService` + `AvaloniaClipboardService` (lazy
  TopLevel resolution). Copy buttons in 8 places: structural fix prompt,
  AI semantic result, audit fix prompt, prompt library rows (per-row
  Body copy), Fill Template result, Generated Prompt, per-Notebook
  assistant message, Session Builder review result.
- **Added** Notes section in Notebook now reveals the selected note's
  body in a preview pane with three buttons: **Insert into chat**
  (prepends as reference for next message), **Copy**, **Delete**.
- **Added** Claude model dropdown in Settings — Opus 4.7 / Sonnet 4.6 /
  Haiku 4.5 (latest) + previous-gen Opus 4.6 / Sonnet 4.5 / Opus 4.5 +
  legacy Opus 4.1. Each tagged with tier + pricing hint. Custom-ID
  textbox kept for preview models the dropdown hasn't caught up with.
- **Fixed** Model catalog initially shipped with fake `claude-sonnet-4-7`
  ID (no such model exists — latest Sonnet is 4.6); corrected against
  Anthropic's official model overview.

## [v0.16] — 2026-05-24 — M2.8 custom Markdown renderer

Custom `MarkdownPresenter` Avalonia control backed by Markdig (parser only,
no Avalonia coupling). Replaces the third-party `Markdown.Avalonia` attempt
that silently blanked the chat bubble in every binding mode we tried. Used
for assistant prose in the Notebook + the audit's Design section.
(`5810c49`)

- **Added** Markdig 0.42.0 package; `App/Controls/MarkdownPresenter.cs`
  walks the AST and emits native Avalonia controls.
- **Added** Block support: H1–H4, paragraphs, fenced code blocks, ordered
  + unordered lists, blockquotes, thematic breaks, tables. Tables use
  star-weighted columns by body content length AND a per-column `MinWidth`
  sized to the header so headers stay single-line while body wraps.
- **Added** Inline support: literal text, inline code (monospace pill),
  strong/emphasis, links (styled), autolinks, line breaks.
- **Removed** Earlier `Markdown.Avalonia` dependency attempts and the
  `IsStreaming`-toggled SelectableTextBlock / MarkdownScrollViewer pair
  that came with them.

## [v0.15] — 2026-05-24 — M2.6 + M2.7: inline doc editor + watch mode

Click a doc in the Documentation list → the right column swaps from
Findings to a monospace text editor with Save / Revert / Close. Watch
mode adds a `FileSystemWatcher` on the project folder that debounces
`.md` / `.txt` changes and re-runs the structural pass. (`b9a250d`)

- **Added** `SelectedDoc` + editor state on `DocumentationViewModel`;
  `IsDefaultViewVisible` gates the findings view vs editor.
- **Added** Watch-mode checkbox; 750 ms debounce via swap-and-cancel
  `CancellationTokenSource`; rebuild watcher on toggle or folder change.

## [v0.14] — 2026-05-24 — M1.5 curated prompts seed + M1 close-out

30 prompts across 5 categories (Doc & VCS hygiene, Testing & regression,
Efficient task execution, New session starters, Common dev tasks) land in
a new `SeedPromptsData.cs`. `Database.SeedPrompts` now upserts by title
diff instead of "only run on empty table" so existing DBs pick up the
curated set without losing user-created prompts. (`8044ea9`)

- **Added** `SeedPromptsData.All` with 30 `SeedPrompt` records.
- **Changed** `Database.SeedPrompts` from one-time seed to idempotent
  by-title diff.
- **Fixed** Two FTS5 tests made durable by inserting their own fixtures
  instead of depending on seed contents.

## [v0.13] — 2026-05-24 — M1.4 read-only Notebook tools + UX overhaul

The biggest M1 commit. Adds `read_file` + `list_directory` as
auto-executed read-only tools alongside the three approval-gated write
tools. Active-project dropdown in the sidebar narrows scope from "all
registered" to "one chosen". Full constitution-style system prompt
loaded from `Assets/notebook-system-prompt.md`. One bubble per user
turn (chips above prose, accumulating across iterations). Empty-response
fallback. Catches non-ASCII API keys on save and use. (`7c83547`)

- **Added** `ReadFile` / `ListDirectory` on `IAgentActionService` (scoped-
  roots-confined). Two read-only tool schemas declared on every
  `AgentChatAsync` call.
- **Added** `ToolActivity` chip type, `NotebookMessage.Activities`
  collection, `BoolToToolActivityBrushConverter`.
- **Added** `Assets/notebook-system-prompt.md` loaded at startup with
  `{{scoped_roots}}` / `{{active_project}}` / `{{provided_files}}`
  substitution per turn.
- **Added** Active-project dropdown bound to registered projects.
- **Changed** `RunAssistantTurnAsync` accumulates one bubble across all
  auto-loop iterations; iteration cap removed (Cancel button is the
  brake).
- **Changed** `DpapiKeyStore.SaveKey` + `AnthropicChatService.BuildRequest`
  reject non-ASCII characters in the API key with a clear message.
- **Added** Five `AgentActionServiceTests` covering ReadFile /
  ListDirectory scoping + truncation. Tests: 27 → 32.

## [v0.12] — 2026-05-24 — M1.3 Cancel on long AI calls

Every async `[RelayCommand]` that hits the Anthropic API gets
`IncludeCancelCommand = true` plus an `OperationCanceledException` catch
that surfaces "Cancelled." rather than an error. Cancel button next to
each action, `IsVisible` bound to `IsBusy`. (`3c2c6bc`)

- **Added** Cancel buttons for: Notebook Send + ExecuteActions,
  PromptManager Redesign + Generate, Documentation RunSemantic,
  SessionBuilder RunReview.

## [v0.11] — 2026-05-24 — M1.2 Open in Claude Code button

New `IClaudeCodeLauncher` in Core + `ClaudeCodeLauncher` in App that
probes the PATH for `claude`, launches it in a new cmd window with the
project as cwd, or falls back to copying `cd "<path>" && claude` to the
clipboard. Button on the Projects tab editor. (`942d864`)

## [v0.10] — 2026-05-23 — Full project documentation + v1.0 roadmap

Five docs land together to give the project a real documentation surface
before more code piles up. (`8ef8075`)

- **Added** `ROADMAP.md` (forward-looking v1.0 plan across five
  milestones, with a content sketch for the curated prompts seed).
- **Added** `CHANGELOG.md` (reverse-chrono history, this file).
- **Added** `docs/USER_GUIDE.md` (module-by-module walkthrough).
- **Added** `docs/ARCHITECTURE.md` (technical overview for contributors).
- **Changed** `README.md` rewritten as a landing page with a doc index.

## [v0.9] — 2026-05-23 — Git-aware staleness

Documentation Manager's structural pass now layers Git history on top of
filesystem mtime. (`a31c59f`)

- **Added** `GitInfo` helper that shells out to `git log -1 --format=%ct`
  with safe argument quoting and a 5-second timeout; silently no-ops when
  git is missing, the folder isn't a repo, or a file has no commits.
- **Added** new findings: `Stale doc (Git)` (Warning) when a doc's last
  commit lags the project's most recent commit by ≥ 60 days,
  `Uncommitted changes` (Info) when FS mtime is newer than the last
  commit, `Untracked doc` (Info) when a doc has no commits.
- **Changed** `IDocReconciliationService.AnalyzeStructuralAsync` signature
  to take a `projectRoot` parameter.

## [v0.8] — 2026-05-23 — Real `tool_use` Notebook + Projects tab

Notebook switched from a structured-JSON shim onto Anthropic streaming
`tool_use`, and a Projects tab landed alongside so the new flow is testable
from the UI. (`3ec12f3`)

- **Added** Projects tab with full CRUD (Name / Description / FolderPath
  via picker / Status).
- **Added** `IProjectStore.Changed` event; Home, Documentation, and
  Notebook subscribe and live-update.
- **Changed** Notebook uses `AgentChatAsync` with three real tools
  (`create_file`, `create_folder`, `move`) declared with explicit JSON
  Schemas. Streaming text deltas appear live via a new `NotebookMessage`
  class with an observable `Text`.
- **Changed** `tool_use` blocks land in PendingActions; Execute posts
  `tool_result` blocks back and re-calls `AgentChatAsync` to continue the
  turn. Clear injects `is_error=true` results so history stays valid.
- **Removed** the JSON-regex shim and its system-prompt instructions.

## [v0.7] — 2026-05-23 — Streaming + tool_use plumbing

Foundation for v0.8 — touched the AI client without changing user-facing
behavior. (`0e23423`)

- **Added** `AgentTurn` / `AgentContentBlock` (Text / ToolUse / ToolResult),
  `AgentTool`, `AgentChatResponse` in Core.
- **Added** `IAiService.AgentChatAsync` (streaming + tool_use).
- **Added** SSE parsing in `AnthropicChatService` over an injectable
  `HttpClient`. Test-only constructor accepts a pre-configured client.
- **Added** five `AnthropicChatServiceTests` covering text-delta streaming,
  tool_use reassembly, request body shape, error events, and
  non-streaming fallback.
- **Kept** existing `CompleteAsync` / `ChatAsync` for non-streaming
  callers (DocReconciliation, PromptManager, SessionBuilder).

## [v0.6] — 2026-05-23 — Prompt version history

Every content-changing save now snapshots the prior row for restore.
(`bd9354c`)

- **Added** `PromptVersion` Core model + `IPromptStore.GetVersionsAsync`.
- **Added** STRICT `prompt_versions` table with `FK ON DELETE CASCADE`
  and a `(prompt_id, captured DESC)` index.
- **Added** `SqlitePromptStore.UpdateAsync` runs in a transaction:
  conditional `INSERT … SELECT … WHERE content changed`, then the UPDATE.
- **Added** History view in Prompt Manager (full-pane, same DockPanel
  pattern as the redesign view); Restore loads a version into the editor.
- **Added** four tests for snapshot-on-content-change, no-snapshot-on-
  usage-count-only, descending order, and FK cascade.

## [v0.5] — 2026-05-23 — Inline colored diff for AI Redesign

(`0fa7690`)

- **Added** `DiffPlex` 1.7.2 dependency.
- **Added** `DiffLine` record + `DiffLineKind` enum +
  `DiffLineKindToBrushConverter` (translucent green / red / transparent).
- **Changed** Redesign panel renders an `ItemsControl` over diff lines
  instead of a plain readonly TextBox.
- **Added** `Apply & Save` command alongside the existing "Apply to
  editor only" and "Dismiss".
- **Changed** Right pane restructured to a `Grid` with mutually-exclusive
  children; redesign view docks header top, action buttons bottom, diff
  fills middle and scrolls internally.
- **Changed** `EditBody` capped at `MaxHeight=320` to keep the action row
  visible when bodies grow long.

## [v0.4] — 2026-05-23 — SQLite FTS5 search in Prompt Manager

(`be760ab`)

- **Added** FTS5 external-content virtual table `prompts_fts` over
  `prompts.rowid`, plus AFTER INSERT / UPDATE / DELETE triggers.
- **Added** first-time backfill so an existing user DB migrates without
  losing prompts.
- **Added** `IPromptStore.SearchAsync`; sanitizes user input via token
  regex (`[\p{L}\p{N}_]+`) → double-quoted + prefix-matched tokens.
- **Changed** `PromptManagerViewModel.ApplyFilter` is async; routes
  through the store. Category filter still applied in-memory.
- **Added** `SqlitePromptStoreTests` (7) covering empty query, title /
  tag match, INSERT / UPDATE / DELETE trigger sync, FTS5 operator
  sanitization.

## [v0.3] — 2026-05-23 — Drag-and-drop file staging

Session Builder Step 3 accepts dropped files. (`aa957d9`)

- **Added** named `FileDropZone` Border with `DragDrop.AllowDrop=True`
  and a faded "Drop files here" hint visible while the staged list is
  empty.
- **Added** `SessionBuilderViewModel.AddFiles(IEnumerable<string>)` —
  bulk version of `AddFile`; reports added / duplicate / missing counts.
- **Added** code-behind handlers for `DragOverEvent` / `DropEvent`;
  resolves files via `IStorageItem.TryGetLocalPath`.

## [v0.2] — 2026-05-23 — `IFilePickerService` + Browse buttons

Native folder/file pickers in three modules. (`7f696b7`)

- **Added** `IFilePickerService` in Core (framework-free) with
  `PickFolderAsync` and `PickFileAsync`; `FilePickerFileType` record.
- **Added** `AvaloniaFilePickerService` resolving the active `MainWindow`
  lazily via `IClassicDesktopStyleApplicationLifetime`.
- **Added** Browse buttons in Documentation (folder), Session Builder
  (output folder + new file), Settings (output folder).
- **Changed** Marked `DpapiKeyStore`, `Program`, and `App` with
  `[SupportedOSPlatform("windows")]` — CA1416 warnings went from 4 to 0.
- **Fixed** Stale `README.md` status sentence ("navigable stubs" →
  "feature-complete v1").

## [v0.1] — 2026-05-23 — Initial scaffold + build

First real commit. (`4220bf0`)

- **Added** Four-project solution: `Core`, `Services`, `App`, `Tests`.
  Layered Core ← Services ← App.
- **Added** All five modules (Documentation, Prompts, Session Builder,
  Notebook, Skill Library) plus shell / Home / Settings.
- **Added** SQLite stores (Project / Prompt / Note), JSON settings,
  DPAPI key store, stub + Anthropic AI services, AgentActionService
  with scoped-roots + undo.
- **Fixed** Bumped Avalonia 11.2.1 → 11.3.0 so `Grid.RowSpacing` and
  `Grid.ColumnSpacing` (used in four views) resolve.
- 11 / 11 xUnit tests green, `dotnet run` launches.

---

[v0.1]: https://example.com/commit/4220bf0
[v0.2]: https://example.com/commit/7f696b7
[v0.3]: https://example.com/commit/aa957d9
[v0.4]: https://example.com/commit/be760ab
[v0.5]: https://example.com/commit/0fa7690
[v0.6]: https://example.com/commit/bd9354c
[v0.7]: https://example.com/commit/0e23423
[v0.8]: https://example.com/commit/3ec12f3
[v0.9]: https://example.com/commit/a31c59f
[v0.10]: https://example.com/commit/8ef8075
[v0.11]: https://example.com/commit/942d864
[v0.12]: https://example.com/commit/3c2c6bc
[v0.13]: https://example.com/commit/7c83547
[v0.14]: https://example.com/commit/8044ea9
[v0.15]: https://example.com/commit/b9a250d
[v0.16]: https://example.com/commit/5810c49
[v0.17]: https://example.com/commit/31424a6
