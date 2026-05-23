# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
Prompt version history — fifth v1.1 polish item and the second half of
the Module 2 "redesign diff + version history" bundle. New
`PromptVersion` Core model (Id, PromptId, Title, Body, Category, Tags,
Captured). `IPromptStore` gained `GetVersionsAsync(Guid)`. `Database.cs`
adds a STRICT `prompt_versions` table with `FOREIGN KEY(prompt_id)
REFERENCES prompts(id) ON DELETE CASCADE` (foreign-keys pragma was
already on) and a `(prompt_id, captured DESC)` index.

`SqlitePromptStore.UpdateAsync` now runs inside an explicit transaction:
an `INSERT … SELECT … FROM prompts WHERE id=$id AND (title!=$title OR
body!=$body OR category!=$cat OR tags!=$tags)` snapshots the *prior*
row into `prompt_versions` only when content actually changed, then the
UPDATE applies. Usage-count- or favorite-only updates (e.g.
`BuildFilledAsync`) don't create snapshot noise. `RemoveAsync` relies
on the FK cascade to drop the history rows. `InMemoryPromptStore`
mirrors the same content-changed guard and cascade.

`PromptManagerViewModel` exposes `Versions` (ObservableCollection),
`IsHistoryPanelOpen`, `IsDefaultViewVisible` (computed:
`!IsRedesignPanelOpen && !IsHistoryPanelOpen`), and
`OpenHistory` / `Restore(PromptVersion)` / `CloseHistory` commands.
Restore loads the version's title/body/category/tags into the editor
and prompts the user to Save — matching the "Apply to editor only"
pattern from the redesign flow. The right-pane Grid grew a third
sibling DockPanel for the history view (same top-docked header /
bottom-docked close button / middle-fills-and-scrolls layout as the
redesign view); each version row shows title, captured timestamp, a
3-line body excerpt, and an inline Restore button.

Four new tests in `SqlitePromptStoreTests` cover snapshot-on-content-
change, no-snapshot-on-usage-count-only, descending-order over
multiple edits, and FK cascade on delete. Test count: 18 → 22, all
green; build clean. Smoked end-to-end: edit twice, open History,
restore an older version, edit again. Next v1.1 candidates in rough
order: streaming `tool_use` for the Notebook (forces a revisit of
`AnthropicChatService`, today a direct REST call with
`anthropic-version: 2023-06-01`, no caching, no streaming); Git-aware
staleness detection in Documentation. NOTE: the handoff skill is
named `cc-handoff` ("claude" is reserved in skill names).

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
