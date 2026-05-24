# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
**v0.26: Bug Tracker module (new Module 5).** A project-scoped
defect log built from the `docs/build-prompts/bug-tracker.md`
spec. Bugs sort by severity (Critical → Major → Minor) and
within a severity, Open/Fixing rise above Fixed/WontFix — the
list answers "what do I fix next?" by reading top-down. The
three reproduction fields (Steps / Expected / Actual) are
deliberately separate to teach reproducible reporting. Includes
a Generate Fix Prompt command that packs selected bugs into a
Claude Code prompt (smallest-correct-change-per-bug instruction,
flag-rather-than-guess rule). Fixed-means-tested nudge fires on
status transition to Fixed. Severity color language reuses
`SeverityToBrushConverter` (extended to recognize `BugSeverity`
alongside `FindingSeverity`). The three build-prompt files
(`bug-tracker.md`, `testing-manager.md`, `_template.md`) moved
into `docs/build-prompts/` so the design intent rides with the
implementation. Build green, sidebar now 8 entries, tests 49/49
(44 prior + 5 new SqliteBugStoreTests).

### What shipped this turn

Single-commit Bug Tracker build (project-scoped defect log).
**Core:** new `Bug` entity + `BugSeverity` (Critical/Major/Minor)
+ `BugStatus` (Open/Fixing/Fixed/WontFix), `IBugStore` interface
with `GetByProjectAsync` filtering by project id.
**Services:** new `bugs` table in `Database.cs` with
`idx_bugs_project` index; new `SqliteBugStore` mirroring
`SqliteProjectStore`'s shape (column-mapped, enums as INTEGER,
Guids as TEXT, single-statement writes under the writer lock).
**App:** new `BugTrackerViewModel` + `BugTrackerView` (master-detail
like `PromptManagerView`, project picker + severity summary chips
on top, severity-sorted list, editor with three generously-tall
reproduction fields, fix-prompt output panel with Copy). DI
registration + sidebar entry wired. `SeverityToBrushConverter`
extended to map `BugSeverity` → same red/amber/blue language as
`FindingSeverity`. **Tests:** 5 new `SqliteBugStoreTests` (add /
project-scoped retrieval / update / remove / Changed event).

### Prior session retrospective (v0.18 → v0.25)

Kept in CHANGELOG.md; the headline arc was: v0.18 safety hardening
(symlink resolution + 429/503/529 retry backoff), v0.19 Tier 1
close-out (audit JSON tests + UX micros + 5 ADRs), v0.20
smoke-test convention + Notebook bubble fix, v0.21–v0.22 Skill
Library Browse + dual-format export + Rename + chips + Resources
concept, v0.23 Anthropic prompt caching + M6 added to roadmap +
per-finding Copy, v0.24 doc maintenance + the bug catalog, v0.25
Skill Library module removed pending rewrite. The Resources
display bug consumed 9 layout iterations and was the trigger for
upgrading the smoke-test rule from "milestone boundaries" to
"every update", and ultimately for the v0.25 delete-and-rewrite
decision.

**Next:** The user has a `docs/build-prompts/testing-manager.md`
spec already written — Testing Manager is the natural follow-on
since its spec explicitly references the Bug Tracker (the
fixed-means-tested nudge is its lightweight stand-in). Also still
on the table: **M3 #11 "Apply with AI"** for documentation fix
prompts (smallest-scope highest-leverage M3 item per HANDOFF), or
the rest of M3 (persistent agent action log, AI call log + cost
tracking — telemetry would surface the prompt-caching savings
in-app). Tier 2 of the non-roadmap bucket (theme dictionary,
`MarkdownPresenter` style resource, VM folders) still available.
Skill Library rewrite is post-v1.0. NOTE: the handoff skill is
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
5. Bug Tracker — project-scoped defect log with severity-sorted list,
   three-field reproduction structure, and a Generate Fix Prompt command
   that packs selected bugs into a Claude Code prompt. (Replaces the
   v0.24 Skill Library slot; that module is deferred for post-v1.0
   rewrite — see ROADMAP M6.)

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
