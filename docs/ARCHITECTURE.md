# ClaudePM Architecture

Technical overview for contributors. Pairs with
[USER_GUIDE.md](USER_GUIDE.md) (the per-module walkthrough), the
top-level [SPEC.md](../SPEC.md) (the original product spec), and
[adr/](adr/README.md) (standalone records of significant technical
decisions — the "why" behind the non-obvious choices).

## Tech stack

| Layer | Tech |
|---|---|
| UI | Avalonia 11.3, CommunityToolkit.Mvvm 8.4 (source generators), compiled bindings |
| Runtime | .NET 9.0 (`net9.0`; App project marked `[SupportedOSPlatform("windows")]`) |
| AI | Direct HTTPS calls to `https://api.anthropic.com/v1/messages` (no SDK); SSE streaming for the agent path |
| Persistence | SQLite via Microsoft.Data.Sqlite 9.0; WAL mode; FTS5 for prompt search |
| Secrets | Windows DPAPI (current-user scope) via `System.Security.Cryptography.ProtectedData` |
| Diffs | DiffPlex 1.7.2 (`InlineDiffBuilder`) |
| Markdown | Markdig 0.42.0 (parser only) + custom Avalonia renderer (`App/Controls/MarkdownPresenter.cs`) |
| Clipboard | `IClipboardService` (Core) / `AvaloniaClipboardService` (App) — lazy `TopLevel.Clipboard` |
| Testing | xUnit 2.9 + NSubstitute 5.3 |

## Solution layout

```
ClaudePM.sln
├── src/
│   ├── ClaudePM.Core/           Domain models + service interfaces. Zero framework deps.
│   ├── ClaudePM.Services/       Concrete services: storage, AI, security, doc reconciliation, agent.
│   └── ClaudePM.App/            Avalonia UI — Views, ViewModels, DI composition root.
└── tests/
    └── ClaudePM.Tests/          xUnit + NSubstitute.
```

## Layered architecture

Dependencies flow in one direction only: **Core ← Services ← App**.

- **Core** is framework-free. It declares the domain models (`Project`,
  `PromptEntry`, `PromptVersion`, `Note`, `DocFile`, `Finding`,
  `AgentAction`, `AgentTurn` + content blocks, `SkillFile`, etc.) and the
  service interfaces (`IProjectStore`, `IPromptStore`, `INoteStore`,
  `IAiService`, `IDocReconciliationService`, `IAgentActionService`,
  `ISecureKeyStore`, `ISettingsService`, `ISessionBuilderService`,
  `ISkillLibraryService`, `IFilePickerService`). Core has no dependency
  on Avalonia, SQLite, or any other framework.
- **Services** implements those interfaces. SQLite-backed stores
  (`SqliteProjectStore`, `SqlitePromptStore`, `SqliteNoteStore`) and
  in-memory stubs (still useful for tests). `AnthropicChatService` for
  the real AI calls + `StubAiService` for tests. `DpapiKeyStore`,
  `JsonSettingsService`, `DocReconciliationService`,
  `SessionBuilderService`, `SkillLibraryService`, `AgentActionService`.
- **App** holds Avalonia Views and ViewModels, the DI composition root
  (`Program.cs`), and the Avalonia-specific `AvaloniaFilePickerService`.
  ViewModels depend only on Core interfaces — they never see SQLite or
  the Anthropic API directly.

## MVVM

- Every page is a `PageViewModel` (abstract: Title, Glyph, Description).
- Concrete ViewModels are `partial` classes using
  `CommunityToolkit.Mvvm` source generators — `[ObservableProperty]`,
  `[RelayCommand]`, `[NotifyPropertyChangedFor]`.
- Compiled bindings everywhere (`x:DataType="vm:FooViewModel"`,
  `AvaloniaUseCompiledBindingsByDefault=true`).
- No hand-rolled `INotifyPropertyChanged`; missing `partial` silently
  breaks code generation, so all VM files must start with
  `public sealed partial class …`.
- `ViewLocator` (`App/ViewLocator.cs`) maps `FooViewModel` →
  `Views.FooView` so DataTemplates can render any VM by type.

## Composition root

`ClaudePM.App.Program.Main`:

1. Builds a `ServiceCollection`.
2. Registers infrastructure singletons (`Database`, `ISecureKeyStore`,
   `ISettingsService`, all stores, `IAiService`, the per-module services,
   `IFilePickerService`).
3. Registers all page ViewModels as singletons (so the sidebar
   navigation preserves state when you tab away).
4. Registers `MainWindowViewModel` (the shell).
5. Builds the provider and starts Avalonia.

`App.OnFrameworkInitializationCompleted` pulls `MainWindowViewModel` from
the provider and assigns it to `desktop.MainWindow`. ViewModels are never
`new`ed from code-behind.

## Persistence

### SQLite

`Database` (in `ClaudePM.Services.Storage`) owns the connection string,
applies the schema migrations, and exposes:
- `ReadAsync<T>(Func<SqliteConnection, Task<T>>)` — pooled, no lock.
- `WriteAsync(Func<SqliteConnection, Task>)` — guarded by a
  `SemaphoreSlim` so only one writer runs at a time. Multi-statement
  writes that need atomicity wrap themselves in a transaction inside the
  callback (see `SqlitePromptStore.UpdateAsync` for the snapshot-on-
  update pattern).

Pragmas applied per connection: `journal_mode=WAL`,
`synchronous=NORMAL`, `foreign_keys=ON`, `busy_timeout=5000`.

### Schema highlights

- `projects (id, name, description, folder_path, status, last_activity)`
  — STRICT.
- `prompts (id, title, body, category, tags, usage_count, is_favorite,
  created, modified)` — STRICT.
- `prompt_versions (id, prompt_id, title, body, category, tags,
  captured)` — STRICT, FK to prompts with `ON DELETE CASCADE`.
- `prompts_fts (title, body, tags)` — FTS5 external-content virtual
  table over `prompts.rowid`. Synced via AFTER INSERT / UPDATE / DELETE
  triggers. First-time creation backfills from `prompts`.
- `notes (id, title, body, tags, project_id, created)` — STRICT,
  indexed on `project_id`.

DB file lives at `%LOCALAPPDATA%\ClaudePM\claudepm.db`.

### App settings

`JsonSettingsService` reads / writes `AppSettings` as JSON next to the
SQLite DB. Settings include Model, DefaultOutputPath, ProjectRoots,
Theme — no secrets here.

### API key

`DpapiKeyStore` (Windows-only, marked
`[SupportedOSPlatform("windows")]`) writes a DPAPI-encrypted blob to
`%LOCALAPPDATA%\ClaudePM\apikey.bin`. Read on every API call so saving a
new key takes effect without a restart.

## AI client abstraction

`IAiService` is the single interface ViewModels touch. ViewModels never
import Anthropic-specific types or the SDK directly.

Three methods:
- `Task<string> CompleteAsync(systemPrompt, userPrompt, ct)` — single
  shot, non-streaming. Used by DocReconciliation, PromptManager (Redesign
  + Generate), SessionBuilder (Review).
- `Task<string> ChatAsync(systemPrompt, history, ct)` — multi-turn,
  non-streaming. Currently unused after the Notebook switched to the
  agent path; kept for callers that don't need tools.
- `Task<AgentChatResponse> AgentChatAsync(systemPrompt, history, tools,
  onTextDelta, ct)` — streaming + tool_use. Used exclusively by Notebook.

`AnthropicChatService` implements all three:
- POSTs to `/v1/messages` with `x-api-key` and `anthropic-version:
  2023-06-01` headers.
- Non-streaming methods build a typed `MessagesRequest`, parse a
  `MessagesResponse`, concatenate text blocks.
- `AgentChatAsync` builds a payload as `JsonObject` (so tool input
  schemas pass through verbatim), POSTs with `stream:true` and
  `HttpCompletionOption.ResponseHeadersRead`, then runs an SSE parser
  that dispatches `content_block_start` / `content_block_delta` /
  `content_block_stop` / `message_delta` / `message_stop` / `error`
  events. Text deltas fire through the `onTextDelta` callback as they
  arrive; tool_use `input_json_delta` fragments are reassembled and
  parsed once when the block ends.

The class has a test-only constructor that accepts a pre-configured
`HttpClient`; tests use a fake `HttpMessageHandler` to return canned SSE
bytes.

## Agent action safety model (Module 4)

Filesystem actions proposed by Claude pass through three gates:

1. **Allow-list of kinds**: only `CreateFile`, `CreateFolder`, `Move`
   are even modelable (`AgentActionKind` enum has no other values).
   Read-only tools (`read_file`, `list_directory`) are planned for v1.0
   and will bypass the preview gate but stay inside scoped roots.
2. **Scoped roots**: `AgentActionService.SetScopedRoots` is called with
   the folder paths of all registered projects on app start and on every
   `IProjectStore.Changed` event. Validation canonicalizes the action
   path via `Path.GetFullPath` and rejects anything not equal to or
   under one of the roots. (Symlink resolution is v1.1+.)
3. **Preview / Execute / Undo**: `tool_use` blocks → `AgentActionRow`s in
   PendingActions UI → user clicks Execute → `AgentActionService.ExecuteAsync`
   runs and pushes an undo closure onto a stack. Undo replays the
   inverse of the most recent action. UndoHistory is currently
   in-memory; v1.0 M3 moves it to a SQLite `agent_actions` table for
   cross-session persistence.

Cancellation (Clear button) synthesizes `tool_result` blocks with
`is_error=true` and content `"User cancelled the action."`, so the
agent's conversation history stays consistent — Claude sees the
cancellation when the user sends their next message.

## File picker abstraction

`IFilePickerService` lives in Core. `AvaloniaFilePickerService` (App
layer) resolves the active `MainWindow` lazily through
`IClassicDesktopStyleApplicationLifetime`, so the picker doesn't capture
a window reference at construction and ViewModels never see an Avalonia
type. Three modules wire Browse buttons through it: Documentation,
Session Builder, Settings.

## Threading

UI work runs on Avalonia's UI thread (Win32 message pump under the hood).
Anywhere a background task mutates an `ObservableCollection` or
observable property, we marshal back via
`Avalonia.Threading.Dispatcher.UIThread.Post`.

Key spots:
- `NotebookViewModel.RunAssistantTurnAsync` — the `onTextDelta` callback
  fires on the SSE-reading thread; each chunk is dispatched to the UI
  thread to mutate `bubble.Text` (an observable property on
  `NotebookMessage`).
- VMs that subscribe to `IProjectStore.Changed` dispatch their reload
  callback to the UI thread before touching `ObservableCollection`.

Long-running work (doc scans, session builder generation, AI calls) runs
off the UI thread; the busy flag flips while it's in flight.

## Custom Markdown rendering (`MarkdownPresenter`)

`App/Controls/MarkdownPresenter.cs` is a `ContentControl` with a
bindable `Markdown` string property. On every change it parses with
Markdig (parser-only — no Avalonia coupling in the package) and walks
the AST to emit native Avalonia controls into the `Content` slot.
Blocks supported: H1–H4, paragraphs, fenced code blocks (boxed,
monospaced, horizontally scrollable), ordered + unordered lists,
blockquotes, thematic breaks, tables. Inlines: literal text, inline
code (monospace pill), bold/italic via `Span` + `FontWeight`/`FontStyle`,
styled links (with `(url)` suffix), autolinks, line breaks.

Tables get star-weighted columns sized by max body text length plus a
per-column `MinWidth` derived from the header's character count, so
headers stay single-line while body cells wrap inside the rest of the
column. Parser failures fall back to a `SelectableTextBlock` rather
than blanking the surface.

We tried `Markdown.Avalonia` 11.0.2 first — it silently blanked the
chat bubble in every binding mode (always-visible, IsStreaming-toggled,
HasText-gated). The package ships only DLLs with no obvious style-
include path, and debugging would have outweighed writing our own
walker against Markdig directly.

## Project Audit (M2.5)

`IDocReconciliationService.AuditAsync` is the synthesis pass —
distinct from `AnalyzeSemanticAsync`, which only flags contradictions.
The implementation:

1. Sort the scanned docs by signal priority (`CLAUDE.md` → `CHANGELOG.md`
   → `ROADMAP.md` → `SPEC.md` → `README.md` → `KICKOFF.md` → `docs/*` →
   the rest), cap at 12 docs × 4000 chars per doc.
2. Build a labeled bundle and POST to `IAiService.CompleteAsync` with
   a structured-JSON system prompt.
3. Parse the response with `ExtractJsonObject` (balanced-brace scan so
   leading markdown fences and trailing prose don't break parsing),
   deserialize via `AuditPayload` DTOs (case-insensitive + trailing-comma
   tolerant), and project into `ProjectAuditReport`.
4. Fall back to `ProjectAuditReport.Empty` on any parser exception.

`BuildAuditFixPrompt` produces a Claude Code prompt from the
inconsistencies list, separate from the structural fix prompt so the
two don't stomp each other.

## Documentation reconciliation (Module 1)

`DocReconciliationService.AnalyzeStructuralAsync` runs six checks
sequentially against the loaded doc contents, plus a Git-aware check:

- `CheckDeadLinks` — internal markdown links that don't resolve to a
  real file or folder.
- `CheckMarkers` — TODO / FIXME / XXX / HACK / WIP / [DRAFT] occurrences.
- `CheckOrphans` — `.md` files that aren't linked from any other doc
  (excluding entry docs: README, CLAUDE, AGENTS, INDEX).
- `CheckVersionDrift` — multiple version strings disagreeing across docs.
- `CheckMissingDocs` — no README or no CLAUDE.md/AGENTS.md.
- `CheckClaudeMdStaleness` — CLAUDE.md older than the newest other doc
  by > 1 day (FS mtime).
- `CheckGitStalenessAsync` — when the folder is a Git repo (detected by
  running `git log -1` from it), emits Warning/Info findings based on
  per-doc commit times vs the project's most recent commit.

The semantic pass (`AnalyzeSemanticAsync`) caps each doc at 3000 chars
and the total at 12 docs, then asks Claude for contradictions only.
Doc-vs-code is v2 territory.

## Testing

27 tests covering:
- `ProjectStoreTests` — InMemory store CRUD.
- `SqlitePromptStoreTests` — FTS5 search behavior (empty/title/tag
  match, INSERT/UPDATE/DELETE trigger sync, operator sanitization,
  version snapshot-on-content-change, usage-count-only skips snapshot,
  descending order, FK cascade on delete).
- `SkillLibraryServiceTests` — `.skill` validation behavior.
- `SessionBuilderServiceTests` — handoff package generation.
- `AgentActionServiceTests` — scoped roots, validation, execute, undo.
- `AnthropicChatServiceTests` — SSE streaming with a fake
  `HttpMessageHandler`, tool_use reassembly, request body shape, error
  events, non-streaming fallback.

Tests run via `dotnet test ClaudePM.sln` and complete in ~2 seconds.

## Build, run, publish

```pwsh
dotnet restore               # one-time / on package changes
dotnet build                 # incremental
dotnet test                  # full suite
dotnet run --project src/ClaudePM.App
```

Single-file publish (Windows):
```pwsh
dotnet publish src/ClaudePM.App -c Release -r win-x64 --self-contained
```

## Key conventions (also enforced in CLAUDE.md)

- All AI calls go through `IAiService` — never the SDK or HTTP directly
  from a ViewModel.
- Any AI-initiated filesystem action MUST go through preview / execute /
  undo and stay within scoped roots.
- API key never written to disk in plain text or to CLAUDE.md.
- Long-running work off the UI thread; mutations on the UI thread via
  Dispatcher when needed.
- Naming: Views end in `View`, ViewModels in `ViewModel`, services in
  `Service`.
- Update CLAUDE.md "Last Completed Task" at the end of every session.
