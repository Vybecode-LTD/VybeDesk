# Changelog

> Reverse-chronological. Versions trail Git tags; commit hashes link to the
> work that landed each entry.

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
