# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
**v0.25: Skill Library module fully removed pending rewrite.**
The Module 5 sidebar page, View, ViewModel, service interface +
implementation, Core models (`SkillFile`, `SkillResource`),
DI registrations, and 8 SkillLibraryServiceTests were all
deleted. The unresolved Resources/Validation display bug
(9 failed layout iterations through v0.24, commits `13ed1c2` →
`16f9468`) is now moot — the page is gone and will be rewritten
from scratch after the rest of v1.0 ships. Docs (SPEC, ROADMAP,
HANDOFF, README, USER_GUIDE, ARCHITECTURE) updated to mark
Module 5 as deferred-for-rewrite. CHANGELOG history preserved.
Sidebar now has 7 entries; tests dropped from 53 to 44, all green.

### What shipped this turn

Single-commit removal of Module 5 (Skill Library Manager). Deleted
files: `SkillFile.cs`, `SkillResource.cs`, `ISkillLibraryService.cs`,
`SkillLibraryService.cs` (whole `Services/Skills/` folder),
`SkillLibraryViewModel.cs`, `SkillLibraryView.axaml` (+ `.cs`),
`SkillLibraryServiceTests.cs`. Scrubbed two DI registrations from
`Program.cs` (line 11 using, lines 52 + 65 registrations) and the
ctor param + `Pages` entry from `MainWindowViewModel.cs`. Docs
updated across the board. The v0.24 open bug is closed by deletion.

### Prior session retrospective (v0.18 → v0.24)

Kept in CHANGELOG.md; the headline was: v0.18 safety hardening
(symlink resolution + 429/503/529 retry backoff), v0.19 Tier 1
close-out (audit JSON tests + UX micros + 5 ADRs), v0.20
smoke-test convention + Notebook bubble fix, v0.21–v0.22 Skill
Library Browse + dual-format export + Rename + chips + Resources
concept, v0.23 Anthropic prompt caching + M6 added to roadmap +
per-finding Copy, v0.24 doc maintenance + the bug catalog. The
Resources display bug consumed 9 layout iterations and was the
trigger for upgrading the smoke-test rule from "milestone
boundaries" to "every update".

**Next:** **M3 #11 "Apply with AI"** for documentation fix prompts
(smallest-scope highest-leverage M3 item per HANDOFF), or the rest
of M3 (persistent agent action log, AI call log + cost tracking —
telemetry would surface the prompt-caching savings in-app). The
Bug Tracker module spec (`build-prompt-bug-tracker.md`, untracked)
is also ready to build. Tier 2 of the non-roadmap bucket (theme
dictionary, `MarkdownPresenter` style resource, VM folders) still
available. Skill Library rewrite is post-v1.0. NOTE: the handoff
skill is named `cc-handoff` ("claude" is reserved in skill names).

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
5. ~~Skill Library Manager~~ — REMOVED v0.25, deferred for rewrite post-v1.0.
   Original implementation in git history through commit `16f9468`.

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
- **Smoke test after EVERY update.** After every commit that changes
  user-visible behavior — every view edit, every VM-bound property,
  every new command, every layout tweak, every feature — launch the
  app (`dotnet run --project src/ClaudePM.App`, background) and wait
  for the user to visually verify before declaring done OR starting
  the next change. Doc-only and test-only commits exempt. Build-green
  + tests-green prove code correctness, not feature correctness. Tell
  the user explicitly *what to verify in THIS commit* (not generic
  "does everything still work"), then wait — don't queue the next
  change in the same turn. The v0.24 Resources bug saga is the
  cautionary tale: 9 layout iterations passed tests and burned the
  user's patience before the smoke-test rule was tightened from
  "milestone boundaries" to "every update" (and ultimately drove
  the v0.25 decision to delete and rewrite the module). See
  HANDOFF.md for the full protocol.

## Gotchas / Do Not Touch
- CommunityToolkit.Mvvm source generators require `partial` classes — missing
  `partial` silently breaks `[ObservableProperty]` / `[RelayCommand]`.
- Agent filesystem actions are scoped to user-configured project roots only;
  do not widen this scope without an explicit decision.

## Reference Docs
See @SPEC.md for the full feature and architecture spec.
See @KICKOFF.md for the first-task prompt (verify the build first).
<add further @imports here as deeper docs are created next to their code>
