# VybeDesk Architecture

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
VybeDesk.sln
├── src/
│   ├── VybeDesk.Core/           Domain models + service interfaces. Zero framework deps.
│   ├── VybeDesk.Services/       Concrete services: storage, AI, security, doc reconciliation, agent.
│   └── VybeDesk.App/            Avalonia UI — Views, ViewModels, DI composition root.
└── tests/
    └── VybeDesk.Tests/          xUnit + NSubstitute.
```

## Layered architecture

Dependencies flow in one direction only: **Core ← Services ← App**.

- **Core** is framework-free. It declares the domain models (`Project`,
  `PromptEntry`, `PromptVersion`, `Note`, `DocFile`, `Finding`,
  `AgentAction`, `AgentTurn` + content blocks, `ProjectAuditReport`,
  `SkillFile` + `SkillResource` (rebuilt v0.28), `Bug` + `BugSeverity` +
  `BugStatus`, `TestingPlan` + `TestKind` + `QuestionnaireAnswers`,
  `BugFixedEvent`, `VisionRecord` + `VisionStatement` + `StatementVerdict`
  + `AlignmentRank` + `AuditMode` + `AuditReport` + `AuditHistoryEntry`
  (v0.30), etc.) and the service interfaces (`IProjectStore`,
  `IPromptStore`, `INoteStore`, `ISkillLibraryService` (rebuilt v0.28),
  `ISkillBuilderService` (v0.29), `IBugStore`, `ITestingPlanStore`,
  `ITestingFrameworkCatalog`, `IBugFixedNotifier`, `IVisionStore`
  (v0.30), `IVisionAuditService` (v0.30), `IAuditHistoryStore` (v0.30),
  `IAiService`, `IDocReconciliationService`, `IAgentActionService`,
  `ISecureKeyStore`, `ISettingsService`, `ISessionBuilderService`,
  `IFilePickerService`, `IClipboardService`). Core has no dependency on
  Avalonia, SQLite, or any other framework.
- **Services** implements those interfaces. SQLite-backed stores
  (`SqliteProjectStore`, `SqlitePromptStore`, `SqliteNoteStore`,
  `SqliteBugStore`, `SqliteTestingPlanStore`, `SqliteVisionStore`,
  `SqliteAuditHistoryStore`) and in-memory stubs (still useful for
  tests). `AnthropicChatService` for the real AI calls + `StubAiService`
  for tests. `DpapiKeyStore`, `JsonSettingsService`,
  `DocReconciliationService`, `SessionBuilderService`, `AgentActionService`.
  Skills: `Services/Skills/SkillLibraryService.cs` (v0.28) — scans
  `<name>/SKILL.md` only, validates frontmatter, serialises back, plus
  Backup / Export (folder copy) and Rename (folder + frontmatter sync).
  `Services/Skills/SkillBuilderService.cs` (v0.29) — orchestrates the
  Builder workflow, depends on `IAiService` + `ISkillLibraryService` so
  validation and serialization are SHARED with the Manager (one source
  of truth for what a valid skill is and how it renders to text).
  Testing: `TestingFrameworkCatalog` (built-in, ships with the app —
  NOT user data), `BugFixedNotifier` (in-memory pub/sub),
  `StrategySelector` (pure function from `QuestionnaireAnswers` to a
  recommendation).
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

`VybeDesk.App.Program.Main`:

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

`Database` (in `VybeDesk.Services.Storage`) owns the connection string,
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

DB file lives at `%LOCALAPPDATA%\VybeDesk\vybedesk.db`.

### App settings

`JsonSettingsService` reads / writes `AppSettings` as JSON next to the
SQLite DB. Settings include Model, DefaultOutputPath, ProjectRoots,
Theme — no secrets here.

### API key

`DpapiKeyStore` (Windows-only, marked
`[SupportedOSPlatform("windows")]`) writes a DPAPI-encrypted blob to
`%LOCALAPPDATA%\VybeDesk\apikey.bin`. Read on every API call so saving a
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

**Prompt caching.** Both code paths attach a
`cache_control: { type: "ephemeral" }` breakpoint on the system block
(sent in array form so the breakpoint has somewhere to attach) and the
streaming path additionally puts one on the last tool in the `tools`
array — caching the whole tools block as a unit per Anthropic's
hierarchical cache order. See [ADR-0006](adr/0006-prompt-caching-on-system-and-last-tool.md)
for the reasoning, breakpoint budget, and gotchas.

**Retry policy.** `SendWithRetryAsync` wraps both paths; up to 3 retries
on 429 / 503 / 529 with `Retry-After` honored when present and
exponential backoff (1s → 1min cap) with jitter otherwise. The request
gets rebuilt each attempt because `HttpRequestMessage` is single-use.

## Agent action safety model (Module 4)

Filesystem actions proposed by Claude pass through three gates:

1. **Allow-list of kinds**: `CreateFile`, `CreateFolder`, `Move`, and
   `EditFile` (`AgentActionKind` enum). Read-only tools (`read_file`,
   `list_directory`) shipped in v0.13 and bypass the preview gate but
   stay inside scoped roots.
2. **Scoped roots**: `AgentActionService.SetScopedRoots` is called with
   the folder paths of all registered projects on app start and on every
   `IProjectStore.Changed` event. Validation canonicalizes the action
   path via `Path.GetFullPath` and rejects anything not equal to or
   under one of the roots. Symlink resolution shipped in v0.18
   (segment-walking via `FileSystemInfo.ResolveLinkTarget`).
3. **Preview / Execute / Undo**: `tool_use` blocks → `AgentActionRow`s in
   PendingActions UI → user clicks Execute → `AgentActionService.ExecuteAsync`
   runs and pushes an undo closure onto a stack. Undo replays the
   inverse of the most recent action. Persisted to the `agent_actions`
   SQLite table since v0.32, enabling cross-session undo and redo.

Cancellation (Clear button) synthesizes `tool_result` blocks with
`is_error=true` and content `"User cancelled the action."`, so the
agent's conversation history stays consistent — Claude sees the
cancellation when the user sends their next message.

## Cross-module project context (`ActiveProjectContext`)

`IActiveProjectContext` is a cross-cutting singleton that synchronizes
the selected project across all project-scoped modules. Three members:
`Current` (the active `Project?`), `SetCurrent(Project?)`, and a
`Changed` event.

**Idempotent and null-safe (v0.32 fix):** `SetCurrent(null)` is a
no-op — it returns immediately without clearing `Current` or firing
`Changed`. This prevents passive null writes from TwoWay ComboBox
bindings (which fire null during initialization and collection rebuilds)
from cascading across modules. To intentionally clear the context, call
`ClearCurrent()` explicitly. `SetCurrent` with the same project ID
updates the internal reference (for object-identity freshness) but does
NOT fire `Changed`, avoiding redundant reload cascades.

Every project-scoped VM subscribes to `Changed` and syncs its local
`SelectedProject` to whatever the context says, with guards:
`_reloadingProjects` flag during `Projects.Clear()/Add()`,
`_lastSelectedProjectId` for restoration on `OnActivated()`, and
null-write suppression in `OnSelectedProjectChanged`.

**"Choose a project" landing overlays (v0.32):** All six project-scoped
views (Documentation, Notebook, Bug Tracker, Testing Manager, Vision
Audit, plus Prompts) show a centered overlay when no project is selected
(`HasProject` is false). The overlay uses Avalonia Grid z-order (overlay
Border is the LAST child in its Grid cell, with `FallbackValue=True` on
the `!HasProject` visibility binding so it defaults to visible before
DataContext propagates). Each overlay has a solid `#1A1A2E` background so
it fully occludes the content panels beneath it.

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

Tests cover:
- `ProjectStoreTests` — InMemory store CRUD.
- `SqlitePromptStoreTests` — FTS5 search behavior (empty/title/tag
  match, INSERT/UPDATE/DELETE trigger sync, operator sanitization,
  version snapshot-on-content-change, usage-count-only skips snapshot,
  descending order, FK cascade on delete).
- `SqliteBugStoreTests` — add-then-get-by-project, project-scoped
  retrieval (project A's bugs do not appear under project B), update
  round-trip, remove, Changed event fires on every mutating call.
- `SqliteTestingPlanStoreTests` — null-for-unsaved, round-trip all
  fields incl. nested JSON answers and lists, project-scoped retrieval,
  upsert behaviour via `ON CONFLICT(project_id) DO UPDATE`, remove,
  Changed event.
- `TestingFrameworkCatalogTests` — seven seed entries present, every
  entry has non-empty Name/Language/SetupPromptTemplate and at least one
  Kind, language lookup returns expected entries (incl. cross-language
  Playwright), name lookup, drift guard against "Database" appearing as
  a separate framework.
- `StrategySelectorTests` — .NET API with DBs recommends
  xUnit+unit+integration with the database-as-integration note, critical
  React recommends Vitest+RTL+Playwright, personal React omits
  Playwright, personal solo pure logic adds ManualChecklist, no external
  systems omits Integration, unknown language still returns kinds but
  empty Frameworks list with the catalog explainer, summary includes the
  friendly language name.
- `SqliteVisionStoreTests` (v0.30) — round-trips a VisionRecord with
  nullable ApprovedAt, project-scoped retrieval, upsert via
  ON CONFLICT(project_id), remove.
- `VisionAuditServiceTests` (v0.30) — refuses to audit an unapproved
  vision, refuses an empty-statements vision, parses extract responses,
  fills missing verdicts as OffTrack so every statement gets one,
  markdown report leads with off-track items, deep-dive prompt names
  flagged statements.
- `SqliteAuditHistoryStoreTests` (v0.30) — add + newest-first ordering
  via ORDER BY generated_at DESC, project-scoped retrieval, remove
  single entry, clear-all-for-project, Changed event fires per mutation.
- `SkillBuilderServiceTests` (v0.29) — proves the Builder's `Validate`
  is byte-identical to the Manager's (shared via delegation); proves
  `EmitAsync` writes both `.skill` flat file and `<name>/SKILL.md`
  folder forms with identical text; proves an emitted skill scanned
  by the Library validates identically to the in-memory draft;
  proves emit refuses to overwrite an existing target.
- `SessionBuilderServiceTests` — handoff package generation.
- `AgentActionServiceTests` — scoped roots, validation, execute, undo,
  read_file / list_directory truncation, symlink escape rejection.
- `AnthropicChatServiceTests` — SSE streaming with a fake
  `HttpMessageHandler`, tool_use reassembly, request body shape with
  cache_control wire format, error events, non-streaming fallback,
  429 / 503 / 529 retry behavior + give-up-after-max.
- `DocReconciliationServiceTests` — `AuditAsync` JSON parsing across
  the response shapes Claude actually returns (clean JSON, fenced
  JSON, leading prose, trailing prose, mixed casing + trailing
  commas, malformed JSON, no JSON at all, blank-titled items,
  severity-sorted inconsistencies).
- `ActiveProjectContextTests` (v0.32) — SetCurrent null no-ops,
  SetCurrent same-ID updates reference without firing Changed,
  ClearCurrent fires Changed and resets Current to null.
- `ProjectHealthServiceTests` (v0.32) — 6 cases for per-project
  metric computation.
- `SessionTemplatesTests` (v0.32) — 6 cases for template catalog.
- `AgentActionServiceEditFileTests` (v0.32) — 6 cases for the
  edit_file tool's preview/execute/undo cycle.
- `SqliteAgentActionLogStoreTests` (v0.32) — 6 cases for persistent
  agent action log CRUD.
- `ProjectImportServiceTests` (v0.32) — 4 cases for `.claude/` + git
  project import.
- `HomeViewLayoutTests` (v0.32) — 6 VM-level regression tests for
  Home dashboard pagination: page size capping, multi-page splits,
  partial last page, zero-card edge case, valid project data on all
  cards, round-trip navigation back to first page.
- `ProjectsViewLayoutTests` (v0.32) — 6 VM-level regression tests
  for Projects editor form: all form fields populate on selection
  (including M4 #16 additions), HasSelection toggle, null model/
  output/logo → empty edit fields, deselection clears all fields,
  Save writes all fields back to store, empty string → null mapping
  for Model/DefaultOutputPath.
- `ProjectSelectionPersistenceTests` (v0.32) — 6 regression tests
  locking in the ActiveProjectContext passive-null-write protection:
  SetCurrent(null) after a real project preserves the project,
  initial null stays null, different projects fire Changed, only
  ClearCurrent resets to null, multiple passive nulls after switches
  preserve the last project, passive null does not fire Changed.
- `SqliteProjectStoreCascadeDeleteTests` (v0.32) — 10 cases proving
  `RemoveAsync` cascade-deletes all 7 project-scoped tables (bugs,
  testing_plans, vision_records, audit_history, agent_actions, notes,
  ai_calls) in a single transaction. Includes isolation (other
  project's rows survive) and Changed event verification.

Tests run via `dotnet test VybeDesk.sln` and complete in ~2 seconds.
207 tests passing as of v0.32.

## Build, run, publish

```pwsh
dotnet restore               # one-time / on package changes
dotnet build                 # incremental
dotnet test                  # full suite
dotnet run --project src/VybeDesk.App
```

Single-file publish (Windows):
```pwsh
dotnet publish src/VybeDesk.App -c Release -r win-x64 --self-contained
```

## Plugin architecture (extension system)

VybeDesk loads third-party **plugins** at startup and lets them contribute
sidebar pages, view models, and services without recompiling the host. Authoring
guide: [PLUGINS.md](PLUGINS.md). Design rationale:
[ADR-0007](adr/0007-plugin-architecture-collectible-alc.md).

**Assemblies.** A new `VybeDesk.Plugin.Abstractions` (namespace `VybeDesk.Plugin`)
sits between Core and App and is the public SDK: `IVybeModule`, `ModuleManifest`,
`PluginCapabilities`, `IModuleHost`, and the base VMs `ViewModelBase` /
`PageViewModel` / `ProjectScopedViewModel` (moved here from App). Plugins
reference this + `VybeDesk.Core` only. `VybeDesk.Services` references the SDK and
hosts the loader (`Services/Plugins/`).

**Catalog-driven sidebar.** `IModuleCatalog` (`App/Modules/`) yields the ordered
page set — built-in modules, then plugin pages, then Settings last.
`MainWindowViewModel` consumes the catalog instead of a hard-coded constructor
list, so built-ins and plugins reach the sidebar through one mechanism.

**Loading.** At composition time `PluginLoader.LoadInto(IServiceCollection)`
(called from `Program.ConfigureServices`) scans
`%LOCALAPPDATA%\VybeDesk\plugins\*\plugin.json`, host-version-gates each manifest,
and loads every enabled + compatible plugin into its own **collectible
`AssemblyLoadContext`**. The context defers shared contracts
(`VybeDesk.Plugin.Abstractions`, `VybeDesk.Core`, `Avalonia*`, `CommunityToolkit*`,
`Microsoft.Extensions.DependencyInjection*`) to the host's default context so type
identity matches across the boundary. Each plugin's `IVybeModule` is registered as
a singleton; the catalog later calls `GetPages` to collect its pages.

**View resolution.** `ViewLocator` resolves `FooView` from the view model's own
assembly first (`vmType.Assembly.GetType(name)`), so a plugin's views — co-located
with its VMs — are found; `Type.GetType` remains a host fallback.

**Management + trust.** Settings → Plugins (`PluginsViewModel`, nested under a
`SettingsSectionViewModel` group like Skills) lists discovered plugins with
status, enable/disable (persisted to `plugins-state.json`, effective next launch),
install-from-`.vybeplugin`, and open-folder. The trust model is explicit: plugins
are in-process, full-trust code, NOT sandboxed — disclosed via declared
capabilities and prominent UI copy.

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
