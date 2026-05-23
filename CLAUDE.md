# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
Git-aware staleness detection in the Documentation Manager — eighth
and final v1.1 polish item from the KICKOFF roadmap. The structural
pass now layers Git history on top of the existing FS-mtime
heuristics: docs that have been "frozen" in the index while the rest
of the project keeps moving show up as Warning, and docs with edits
that haven't been committed yet show up as Info.

New `ClaudePM.Services.Docs.GitInfo` shells out to `git log -1
--format=%ct` via `ProcessStartInfo.ArgumentList` (safe quoting, 5-
second timeout) and returns `DateTimeOffset?` — null when git is
missing, the folder isn't a repo, or the file has no commits. Every
failure path is a silent no-op, so non-Git projects don't gain false
findings.

`IDocReconciliationService.AnalyzeStructuralAsync` signature grew a
`string projectRoot` parameter (passed through from
`DocumentationViewModel`). New `CheckGitStalenessAsync` queries the
project's most recent commit once, then for each doc emits:
- **Warning "Stale doc (Git)"** — doc's last-commit time lags the
  project's most-recent commit by ≥ 60 days.
- **Info "Uncommitted changes"** — FS mtime is more than a minute
  newer than the doc's last commit (local edits not yet in Git).
- **Info "Untracked doc"** — the doc has no commits at all.

Build + 27/27 tests stay green; smoked against the ClaudePM repo
itself — clean on a fresh-committed tree, "Uncommitted changes"
fires the moment a tracked doc is edited.

**KICKOFF roadmap is now fully shipped.** Items closed today (in
order): Avalonia 11.3 fix, IFilePickerService, drag-and-drop staging,
FTS5 search, inline colored diff, version history, streaming +
tool_use plumbing, Notebook rewire + Projects tab, Git-aware
staleness. v2 candidates from SPEC.md remain: doc-vs-code semantic
reconciliation, macOS/Linux secure key stores (Keychain, libsecret),
tray + dashboard polish, commercial path (licensing, telemetry).
NOTE: the handoff skill is named `cc-handoff` ("claude" is reserved
in skill names).

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
