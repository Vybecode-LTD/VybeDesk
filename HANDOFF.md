# Handoff — ClaudePM

> A handoff package for a new Claude Code session agent picking up
> ClaudePM development cold. If you're that agent: read this all the
> way through *before* touching any other doc.

## TL;DR

ClaudePM is a Windows desktop app (Avalonia 11.3 + .NET 9) that helps
you manage Claude-Code-driven work — documentation reconciliation, a
curated prompt library, claude.ai → Claude Code handoff packages, a
streaming `tool_use` agent for scoped filesystem actions, and `.skill`
file management. **Three of six roadmap milestones (M1, M2, M2.5) are
shipped**; M3 / M4 / M5 remain. Build is green; 32 / 32 tests pass.

## Read order

In this order, skip nothing:

1. **This file** (you're reading it) — orientation, conventions,
   gotchas, roadmap pointers, starting prompt.
2. **[CLAUDE.md](CLAUDE.md)** — "Last Completed Task" tells you exactly
   what shipped last and what's next.
3. **[ROADMAP.md](ROADMAP.md)** — the full v1.0 plan, with completed
   milestones marked. Items have S/M/L scope tags.
4. **[CHANGELOG.md](CHANGELOG.md)** — versioned history of every commit
   with Added/Changed/Fixed/Removed. v0.17 is current.
5. **[SPEC.md](SPEC.md)** — original product spec for the eight
   modules. Some details have evolved (e.g. AI client now direct HTTPS,
   not the SDK; `save_note` is a user button, not an AI tool); the
   `## Last Completed Task` in CLAUDE.md is authoritative for current
   state when in doubt.
6. **[docs/USER_GUIDE.md](docs/USER_GUIDE.md)** — module-by-module
   walkthrough. Read at least the modules touched by your next task.
7. **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** — technical
   reference. Stack, layering, persistence schemas, AI client,
   `MarkdownPresenter`, Project Audit flow, threading.
8. **[KICKOFF.md](KICKOFF.md)** — historical. The original bootstrap
   prompt. Most "deferred" items there have shipped; included for
   archaeology, not for current planning.

If you're being asked to do feature work, **first verify the build
works** (next section). Don't trust that the repo is in a runnable
state just because it builds — there can be data-state issues (DB
schema bumps that haven't been migrated yet, stale `.claude/`
settings, etc.).

## Quick verify

```pwsh
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ClaudePM.App
```

Expect: 32 / 32 xUnit tests pass, the app window opens with eight
sidebar entries (Home / Projects / Documentation / Prompts / Session
Builder / Notebook / Skill Library / Settings).

If you can't get the app running, **stop and ask the user before
touching anything else**. A broken build is a strong signal that
context has drifted.

## Conventions (NON-NEGOTIABLE)

These are encoded in `CLAUDE.md` and enforced socially via code
review. Violations should be caught early, not at PR time.

- **Layering**: strict one-directional dependency. `Core ← Services ←
  App`. Core has no framework deps (no Avalonia, no SQLite). Services
  knows persistence + AI client. App owns the UI and DI composition
  root.
- **MVVM**: every ViewModel is a `partial` class using
  `CommunityToolkit.Mvvm` source generators. Missing `partial` silently
  breaks `[ObservableProperty]` and `[RelayCommand]` — easy to miss.
- **Compiled bindings everywhere**: every `DataTemplate` has
  `x:DataType="..."`. Without this Avalonia falls back to reflection
  and silently swallows binding errors.
- **AI calls through `IAiService` only.** Never instantiate
  HttpClient against `/v1/messages` from a ViewModel. Never import
  the Anthropic SDK directly.
- **Filesystem actions from AI must use the preview/execute/undo
  gate.** `AgentActionService.ExecuteAsync` is the only path that
  mutates disk on behalf of Claude. Direct writes from VMs to user
  project folders are forbidden (user-driven Save in the doc editor
  is the only exception, and that's user input, not agent action).
- **Scoped roots**: agent file actions must canonicalize inside one
  of `IAgentActionService.ScopedRoots`. Anything outside is rejected
  by the validator.
- **API key**: stored via `DpapiKeyStore` (Windows DPAPI). Never write
  it to disk in plaintext, never to CLAUDE.md, never to git. Validate
  ASCII on save and use (Anthropic rejects non-ASCII header chars).
- **Update CLAUDE.md "Last Completed Task" at the end of every
  session.** New agents read it first to orient.
- **Naming**: Views end in `View`, ViewModels in `ViewModel`, services
  in `Service`. Models are noun phrases.

## Gotchas & paper cuts (current state)

Things that bit us in development and might bite you:

- **App locks DLLs while running.** A build during a running session
  hits `MSB3027` / `MSB3021` lock errors. Always stop the app
  (`Get-Process ClaudePM.App | Stop-Process`) before rebuilding if
  it's been launched in this session.
- **Markdown.Avalonia is a no-go.** We tried 11.0.2 in every binding
  mode (always-visible, IsStreaming-toggled, HasText-gated); it
  silently blanks the bubble. Don't reintroduce it. The custom
  `MarkdownPresenter` (Markdig parser + native Avalonia walker) is
  what works.
- **Star columns in Avalonia don't grow to fit content.** They take
  proportional share regardless of what's inside. To make a column
  fit content, use `MinWidth` on the `ColumnDefinition` (see
  `MarkdownPresenter.RenderTable` for the header-width-estimate
  pattern).
- **Run elements don't have `Opacity`.** Use `Foreground="#7F7F8A"`
  for "dim" inline text instead.
- **Static fields aren't visible to compiled bindings.** If you want
  a dropdown bound to a static list, expose it as an instance
  property: `public IReadOnlyList<T> Foo => Catalog;` (see
  `SettingsViewModel.AvailableModels`).
- **Smart-quote / em-dash API keys.** Anthropic rejects non-ASCII
  header values. `DpapiKeyStore.SaveKey` and `AnthropicChatService`
  both validate and surface a clear error. Don't relax that — it
  catches real copy-paste typos.
- **`ToolSearch` for deferred tools.** When the agent needs a tool
  like `WebFetch` that's deferred, load via `ToolSearch` first;
  schemas aren't loaded by default.
- **Iteration cap on the Notebook auto-loop is OFF.** The Cancel
  button is the brake. If a complex audit / inspection task spins
  forever, that's the user's call — don't reintroduce the cap silently.
- **README has user smoke-test edits sometimes.** If `git status`
  shows README dirty, check before committing — the user may have
  added test characters while verifying watch mode. Leave their edits
  alone unless they ask you to clean them up.
- **Anthropic model IDs change.** The dropdown catalog in
  `SettingsViewModel.ModelsCatalog` was caught with a fabricated ID
  (`claude-sonnet-4-7` doesn't exist). When adding new models, verify
  against `https://docs.claude.com/en/docs/about-claude/models/overview`
  — don't guess.

## Where to start

The right next move depends on what the user wants. Default per the
roadmap is **M3 — Smarter Notebook + telemetry**:

> Persistent agent action log per project (move `UndoHistory` from
> in-memory to a SQLite `agent_actions` table), "Apply with AI"
> button on documentation fix prompts (routes through the Notebook),
> AI call log + cost tracking (SQLite `ai_calls` table + Activity
> view in Settings), streaming token meter in the busy chip.

The single highest-leverage standalone item is **"Apply with AI" for
fix prompts** — it closes the doc-reconciliation loop (audit → fix
prompt → Notebook executes the fixes through the existing safety
gate). Small VM change, no schema migration, big UX win.

Other reasonable directions if M3 isn't the user's priority:

- **M4 — Real project hub**: importing existing `.claude/` directories,
  Session Builder templates, per-project model/output overrides.
- **M5 — Landing dashboard + polish**: Home health cards, light theme
  done properly (every dark hex in the views moves to
  `DynamicResource`), v1.0 release polish.

## Optimizations / improvements worth considering

Things I'd want to do that aren't on the roadmap explicitly:

### Code quality
- **Extract per-module ViewModels into folders.** Currently flat under
  `App/ViewModels/`; once we have ~15 VMs the namespace will get noisy.
- **Pull all hardcoded hex colors into a theme dictionary.** Required
  for the M5 light theme anyway; doing it incrementally now is cheaper
  than a Big Bang.
- **Test coverage for `MarkdownPresenter`.** It renders to Avalonia
  controls so end-to-end testing is awkward, but the parsing layer
  (Markdig configuration, fence handling) is unit-testable in isolation.
- **Test coverage for `AuditAsync` JSON parsing.** `ParseAuditPayload`
  + `ExtractJsonObject` should have golden-input tests against a
  variety of Claude response shapes (clean JSON, fenced JSON, JSON
  with leading prose).

### UX
- **Loading skeletons for long AI calls.** Currently the bubble is
  empty until streaming starts; a small "thinking…" placeholder would
  be friendlier.
- **Keyboard shortcuts.** Ctrl+Enter to send in Notebook, Ctrl+S to
  save in doc editor, Ctrl+K for a command palette.
- **Copy buttons could use a small icon (📋)** instead of "Copy" text.
  Easy win but requires font support; Cascadia Code has emoji
  fallback on Windows.
- **Per-project conversation history.** Notebook conversation resets
  on app restart; persisting per-project would let users resume mid-
  thought.

### Architecture
- **AI cost tracking is a real need.** The user spent $10 in a day on
  Opus 4.7; even a simple "tokens this session" counter would help.
  M3 already has the SQLite table planned — make sure to count
  cached + streaming tokens correctly.
- **Two-layer system prompt.** The notebook constitution
  (`Assets/notebook-system-prompt.md`) is loaded at startup, but the
  per-turn context substitution happens in code. A more flexible
  system would let users edit per-project system prompts too.
- **Markdown rendering pluggability.** Currently `MarkdownPresenter`
  is used in Notebook + audit Design section. As more places want
  rendered markdown, consider making it a reusable Avalonia style
  resource rather than per-view inclusion.

### Safety
- **Symlink resolution in `AgentActionService.TryConfine`.** Currently
  uses `Path.GetFullPath` which collapses `..` but doesn't resolve
  symlinks. A malicious symlink could escape scope. Low risk in
  practice (user-controlled project folders), but worth hardening
  before commercialization.
- **Audit + Apply with AI cycle limit.** When M3 ships "Apply with
  AI" for fix prompts, make sure it can't infinitely loop (audit →
  apply → re-audit → ...). The user's wallet and patience both
  appreciate this.
- **API rate limiting awareness.** No backoff or retry on 429/529
  currently. Anthropic doesn't usually trigger them at low rates,
  but heavy audit usage might. Add exponential backoff to
  `AnthropicChatService` if you see it become a problem.

### Documentation
- **A `docs/adr/` folder.** SPEC.md captures intent but key decisions
  (Markdig over Markdown.Avalonia, direct HTTPS over the SDK,
  DPAPI for keys, no iteration cap on Notebook loop, audit using
  structured-JSON instead of tool_use) deserve standalone ADRs.
- **CONTRIBUTING.md.** Planned for v1.0; the conventions section here
  + Architecture doc are a decent start when extracted.

## Starting prompt for the next session agent

Paste this into a new Claude Code session running against this repo:

```
You're picking up development on ClaudePM, a Windows desktop app
(Avalonia + .NET 9) for managing Claude-Code-driven work. The repo
has a complete handoff package — read it carefully before doing
anything else.

Read in this order:
1. HANDOFF.md (the orientation package, read it all)
2. CLAUDE.md (Last Completed Task tells you exactly where we are)
3. ROADMAP.md (what's left for v1.0 — M3, M4, M5 remain)
4. CHANGELOG.md (versioned history; v0.17 is current)
5. docs/ARCHITECTURE.md (technical reference for the modules you'll touch)

After reading, do these in order:
1. Verify the build: `dotnet restore && dotnet build && dotnet test`.
   All 32 tests should pass.
2. Run the app: `dotnet run --project src/ClaudePM.App`.
   Confirm it launches with eight sidebar entries.
3. Tell me a one-paragraph summary of: (a) what shipped most recently,
   (b) what you think the highest-leverage next item is, and (c) any
   conventions or gotchas from HANDOFF.md you want me to confirm
   before you touch the code.

Then wait for me to direct the next task. Don't start work until I
confirm the direction.

If anything in the repo looks wrong (build fails, docs contradict
each other, the audit overlay surfaces inconsistencies), tell me
before patching. Don't make documentation changes without showing
me first.

You can run the Project Audit yourself from the Documentation tab
on this very repo as a sanity check — pick the ClaudePM project,
click Scan, then Audit Project. The audit's "Generate Fix Prompt"
output is a good starting point if you need to clean up doc drift.

Conventions you must respect (full list in HANDOFF.md):
- Layered architecture: Core ← Services ← App, strict one-direction.
- MVVM via CommunityToolkit source generators; all ViewModels
  `partial`, all bindings compiled.
- All AI calls through IAiService; never instantiate HttpClient
  against the Anthropic endpoint from a ViewModel.
- Agent filesystem actions only through AgentActionService — preview
  / execute / undo gate, scoped to registered project roots.
- API key validated ASCII-only on save and on use.
- Update CLAUDE.md "Last Completed Task" at the end of every session.

If you're unsure about scope on a task, ask. Bigger blast radius
than expected = pause and check, every time.
```

## Repo state at handoff

```
Branch:    main
Latest:    [whichever commit lands the doc maintenance + this file]
Tag:       AlphaV0.5.0 (end of M1)
Build:     ✓ clean
Tests:     32 / 32 pass
Modules:   8 sidebar pages, all functional
```

Welcome to ClaudePM. The shape is solid; the rest is just shipping.
