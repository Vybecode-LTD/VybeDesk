# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
**Milestone 2 (Author & maintain docs in-app) shipped.** Snapshot
`AlphaV0.5.0` was taken between M1 and M2. Three items, two commits
this milestone:

- `b9a250d` **M2.6 + M2.7 — inline doc editor + watch mode.**
  Documentation tab's right column now swaps from Findings → a full
  editor when the user clicks a doc in the list (`SelectedDoc` →
  `IsEditorOpen`, mutually exclusive via `IsDefaultViewVisible`).
  Editor has the relative path as a header, a monospace TextBox
  body, and Save / Revert / Close. Save writes via
  `File.WriteAllTextAsync`; Revert reloads from disk; Close clears
  state. No scoped-roots check — user-driven edits aren't agent
  actions. Watch mode is a checkbox in the controls row that attaches
  a `FileSystemWatcher` to FolderPath (subdirectories on, listening
  for Changed/Created/Deleted/Renamed on `.md`/`.txt`). Changes
  trigger a 750 ms debounce (swap-and-cancel `CancellationTokenSource`)
  then re-run the structural pass via `Dispatcher.UIThread.InvokeAsync`.
  Watcher rebuilds on toggle or folder change, cleans up on disable.

- *(this commit)* **M2.8 — custom Markdown renderer.** Added Markdig
  0.42.0 as the parser only; the AST walker is `App/Controls/
  MarkdownPresenter.cs`, a `ContentControl` with a bindable
  `Markdown` string property that re-renders the body on every
  change into a `StackPanel` of native Avalonia controls. Supports
  headings (#–####), paragraphs, fenced code blocks (boxed,
  monospaced, scrollable horizontally), inline code (monospace
  pill), bold/italic, ordered/unordered lists, blockquotes,
  thematic breaks, links (styled), and tables. Tables use star
  columns weighted by max body text length AND a per-column
  `MinWidth` sized to the header's text length (so headers always
  fit single-line while body cells wrap inside the rest of the
  width). Replaced `SelectableTextBlock` with `MarkdownPresenter`
  in the Notebook chat bubble. Try-catch falls back to plaintext if
  the parser ever throws so the bubble can't blank.

This finally closes the Markdown-rendering thread we deferred from
M1 (Markdown.Avalonia opacity blanking) and gives the Notebook a
proper formatted response surface — code blocks especially are now
visually distinct, which makes Claude's structured deliverables
(audits, plans, analyses) actually readable.

**Next:** M2.5 — Project Audit (the synthesis-pass feature: weighted
doc bundle → structured-JSON Claude call → ProjectAuditReport with
design summary / roadmap items / inconsistencies, rendered in a
full-pane overlay; bonus git cross-check for "claimed-complete but
no commits" findings). NOTE: the handoff skill is named `cc-handoff`
("claude" is reserved in skill names).

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
