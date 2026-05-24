# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
**M2.5 (Project Audit) shipped + documentation maintenance pass +
handoff package.** Two commits this round:

- `31424a6` **M2.5 — Project Audit + clipboard + model picker + notes
  upgrade.** Audit is the synthesis pass: weighted doc bundle
  (CLAUDE/CHANGELOG/ROADMAP/SPEC/README/KICKOFF/docs/rest, capped at
  12 docs × 4000 chars) → structured-JSON Claude call → parsed
  `ProjectAuditReport` (Design / RoadmapItems with status /
  Inconsistencies) rendered in a full-pane overlay with its own
  AuditFixPrompt section. New `IClipboardService` powers Copy buttons
  in 8 places (audit / structural fix prompts, semantic result, per-
  prompt-library-row, FilledResult / GeneratedPrompt, SessionBuilder
  ReviewResult, per-Notebook-message). Notes section in Notebook
  reveals selected note body + 3 buttons (Insert into chat / Copy /
  Delete) — Insert prepends as a "Reference (from saved note ...)"
  block + `---` separator into ChatInput so saved Claude responses
  can ground future turns. Claude model dropdown in Settings (Opus
  4.7 / Sonnet 4.6 / Haiku 4.5 + legacy) with tier + price hints;
  freeform textbox kept for custom IDs. Initially shipped with fake
  `claude-sonnet-4-7` ID (no such model); corrected against
  Anthropic's official model overview.

- *(this commit)* **Documentation maintenance pass + handoff
  package.** Ran the new Project Audit on the ClaudePM repo itself
  and applied the resulting fix prompt. Specific changes: SPEC.md
  Notebook section corrected (`save_note` is a user button, not an
  AI tool; AI tools are `read_file`/`list_directory` auto-executed
  + `create_file`/`create_folder`/`move` approval-gated) and the
  AI stack note rewritten (direct HTTPS + SSE, not SDK). KICKOFF.md
  gets a "HISTORICAL DOCUMENT" header pointing readers at
  CHANGELOG/README. ROADMAP.md M2.8 updated to mention custom
  Markdig-backed presenter (not Markdown.Avalonia); M1, M2, and
  M2.5 marked SHIPPED with commit refs. README.md status rewritten
  for v0.17 + accurate feature list. CHANGELOG.md gains v0.10–v0.17
  entries reverse-chronologically. docs/USER_GUIDE.md and
  docs/ARCHITECTURE.md updated for editor, watch mode, audit,
  MarkdownPresenter, ClipboardService, model picker, notes UX. New
  HANDOFF.md authored for next-session handoff — read order,
  conventions, gotchas, optimization wishlist, and a ready-to-paste
  starting prompt.

**Next:** M3 — Smarter Notebook + telemetry. The highest-leverage
single item is M3.10 "Apply with AI" for documentation fix prompts
(routes the fix prompt directly into the Notebook against the
project root, no schema change). M3 also covers persistent agent
action log per project (SQLite `agent_actions` table replaces in-
memory UndoHistory), AI call log + cost tracking (`ai_calls` table +
Activity view in Settings), and streaming token meter in the busy
chip. NOTE: the handoff skill is named `cc-handoff` ("claude" is
reserved in skill names).

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
