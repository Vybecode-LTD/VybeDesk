# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
Inline colored diff view for the AI Redesign flow — fourth v1.1 polish
item (first half of "redesign diff + version history"; version history
itself is the planned next commit). Added a `DiffPlex` 1.7.2 package
reference; new `DiffLine` record + `DiffLineKind` enum live in
`App/ViewModels`. `PromptManagerViewModel.RedesignAsync` now feeds
`(EditBody, RedesignResult)` through `InlineDiffBuilder.Diff` and
populates an `ObservableCollection<DiffLine> RedesignDiff`. New
`DiffLineKindToBrushConverter` (registered as `DiffLineBrush` in
`App.axaml`) maps Inserted/Deleted/Unchanged to translucent
green / red / transparent row backgrounds; each row renders `+ / - /
space` markers in a monospaced gutter. Replaced the plain readonly
TextBox in the redesign panel with an ItemsControl over RedesignDiff.

New `ApplyRedesignAndSaveAsync` command — when the diff is acceptable,
one click pushes the redesigned text into the editor and persists it
through `SaveAsync` (status: "Redesign applied and saved."). The
existing "Apply to editor only" remains for users who want to keep
tweaking before saving; "Dismiss" still discards. The diff buttons
panel exposes all three.

Right pane was restructured to a Grid with two mutually exclusive
children: the default editor ScrollViewer is hidden when
`IsRedesignPanelOpen` is true, and a sibling DockPanel takes over the
full pane — title docks top, action buttons dock bottom, the diff
fills the middle and scrolls internally — so the action buttons are
always reachable regardless of diff length. Also capped the body
editor `TextBox` with `MaxHeight=320` so long bodies no longer push
the editor's action row off the bottom of the outer ScrollViewer.
Build + 18/18 tests stay green; smoked the redesign flow end to end
including Apply & Save with a long redesigned body. Next v1.1
candidates: prompt version history (persistent prior body versions +
viewer + Restore — second half of the Module 2 redesign item);
streaming `tool_use` for the Notebook (forces a revisit of
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
