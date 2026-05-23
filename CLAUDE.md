# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
Drag-and-drop file staging landed in the Session Builder — second v1.1
polish item. New public `SessionBuilderViewModel.AddFiles(IEnumerable<string>)`
mirrors `AddFile`'s validation in bulk (exists + not-duplicate) and reports
counts via `StatusMessage` ("N staged, N duplicate, N missing."). Step 3 of
the wizard wraps its file ListBox in a named drop zone (`FileDropZone`
Border with `DragDrop.AllowDrop=True`), with a faded "Drop files here" hint
that's only visible while `FilePaths` is empty. `SessionBuilderView.axaml.cs`
attaches `DragOverEvent` / `DropEvent` handlers in the constructor: drag-over
sets `DragDropEffects.Copy` when `DataFormats.Files` is present, drop pulls
`IStorageItem.TryGetLocalPath()` off each dropped item and pushes the list
into the VM. Build + 11/11 tests stay green; manually smoked single-file
and multi-file drops. Next v1.1 candidates in rough order: FTS5-backed
search in Prompt Manager (currently in-memory filtering); prompt redesign
diff view + version history; streaming `tool_use` for the Notebook (this
one will force us to revisit `AnthropicChatService`, which today is a
direct REST call with `anthropic-version: 2023-06-01`, no caching, no
streaming); Git-aware staleness detection in Documentation Manager.
NOTE: the handoff skill is named `cc-handoff` ("claude" is reserved in
skill names).

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
