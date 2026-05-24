# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
**Milestone 1 (Out-of-the-box useful) shipped**, plus extras that grew
out of debugging it. Five commits over the milestone:

- `942d864` **Open in Claude Code button.** New `IClaudeCodeLauncher`
  in Core + `ClaudeCodeLauncher` in App that probes the PATH for
  `claude`, launches it in a new cmd window with the project as cwd,
  or falls back to copying `cd "<path>" && claude` to the clipboard.
  Button on the Projects tab.
- `3c2c6bc` **Cancel on long AI calls.** Every async `[RelayCommand]`
  that hits the API got `IncludeCancelCommand = true` plus an
  `OperationCanceledException` catch that surfaces "Cancelled." rather
  than an error. Cancel button next to each action, IsVisible bound
  to IsBusy. Covers Notebook (Send + ExecuteActions), PromptManager
  (Redesign + Generate), Documentation (RunSemantic), SessionBuilder
  (RunReview).
- `7c83547` **M1.4 + Notebook UX overhaul.** Two new read-only tools
  (`read_file`, `list_directory`) that auto-execute inside the loop;
  active-project dropdown narrowing scope from "all" to "one"; the
  full constitution-style system prompt loaded from
  `Assets/notebook-system-prompt.md` with `{{scoped_roots}}` /
  `{{active_project}}` substitution; one bubble per user turn (chips
  + prose accumulating across iterations); no iteration cap (Cancel
  is the brake); empty-response fallback note; ASCII validation of
  the saved API key to catch smart-quote paste bugs; non-Anthropic-
  SDK protections for header validation. We also briefly added then
  removed Markdown.Avalonia — it blanked the bubble in every binding
  variant we tried. Real markdown is now an M2 task (likely a
  custom fence-aware renderer instead of a third-party control).
  Tests: 27 → 32.
- *(this commit)* **M1.5 — curated prompts seed.** 30 prompts across 5
  categories (Doc & VCS hygiene, Testing & regression, Efficient task
  execution, New session starters, Common dev tasks) live in a new
  `SeedPromptsData.cs`. `Database.SeedPrompts` now upserts by title
  diff instead of "only run on empty table" — existing user DBs get
  the new content on next launch without losing legacy or user-
  created prompts. Made two FTS5 tests durable by inserting their
  own fixtures rather than depending on whatever the seed contains.

**M1.1 (light theme) was deferred to M5 polish** — it's M-scope not
S-scope (every view has hardcoded dark hex colors that need to
become `DynamicResource`-bound), and a hybrid would look bad. M5's
"UI consistency pass" is the right home.

**Next:** M2 ("Author & maintain docs in-app") — in-app text editor
in the Documentation tab, watch mode via FileSystemWatcher, and the
real markdown rendering pass we've deferred twice now. NOTE: the
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
