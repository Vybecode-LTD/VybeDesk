# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
**Long session-3 (v0.18 → v0.24): non-roadmap polish bucket
shipped wide, plus a stubborn Skill Library Resources bug that
nine layout iterations couldn't resolve.** Build green, 53 / 53
tests pass. End-of-session doc maintenance + handoff package is
this commit.

### What shipped this session

- **v0.18 (`b7ac51f`) Safety hardening.**
  `AgentActionService.TryConfine` walks every existing path
  segment via `FileSystemInfo.ResolveLinkTarget(returnFinalTarget:
  true)` — junctions planted under a scoped root can no longer
  escape it. `AnthropicChatService.SendWithRetryAsync` retries
  429 / 503 / 529 up to 3 times with `Retry-After` support and
  exponential backoff + jitter (capped 1 min). 4 new tests.

- **v0.19 (`00e82e4` + `d37aa74` + `9bf2e69`) Tier 1 close-out.**
  9 golden-input tests for `AuditAsync` JSON parsing across the
  response shapes Claude actually returns. UX micros:
  `NotebookMessage.ShowThinkingPlaceholder` (italic "thinking…"
  between Send and first delta), Ctrl+Enter to send in Notebook,
  Ctrl+S to save in the doc editor. `docs/adr/` folder with five
  ADRs (Markdig over Markdown.Avalonia, direct HTTPS over the
  SDK, DPAPI for API keys, no iteration cap on the Notebook loop,
  audit as structured-JSON not tool_use).

- **v0.20 (`e1dcf18` + `bcf0f3d`) Smoke-test convention + bubble fix.**
  Non-negotiable convention added to HANDOFF + CLAUDE: at the close
  of every milestone (or any batch the user agreed on as a unit),
  launch the app and wait for the user to visually verify before
  declaring done. Build-green proves code correctness, not feature
  correctness. Also a Notebook bubble fix — the stale
  `*(Claude ended without producing a final response)*` note was
  racing with dispatcher-posted text deltas; replaced with a
  race-free check against `response.TextOutput` and a quiet
  `StatusMessage = "(no response)"` instead of in-bubble noise.

- **v0.21 (`abfe26a` + `be11670`) Skill Library: Browse +
  dual-format scan/export.**
  Browse… button using the existing `IFilePickerService`.
  `ScanAsync` finds both `.skill` files AND `<name>/SKILL.md`
  folders (the modern Claude Code layout under
  `~/.claude/skills/`). `ExportAsync` writes both formats so the
  same skill loads in either runtime. 3 new tests + SPEC.md +
  USER_GUIDE.md updates.

- **v0.22 (`829e09b` + `21826cf` + `13ed1c2`) Skill Library polish.**
  Rename button (handles both formats with collision check).
  Clickable Critical / Warning / Info severity chips that filter
  the right pane to every finding of that severity across all
  scanned skills. App opens Maximized on startup. Initial
  Resources concept (folder-format skills surface their
  alongside-SKILL.md files via a new `SkillResource` Core record +
  `ISkillLibraryService.GetResources`).

- **v0.23 (`dee7f17` + `1e53911` + `e9f6464`) Caching + M6 +
  per-finding Copy.**
  Anthropic prompt caching: `cache_control: { type: "ephemeral" }`
  on the system block (array-form) in BOTH `AgentChatAsync` and
  `CompleteAsync`, plus on the last tool in the streaming path.
  Silently no-ops below model minimum (4096 tokens for Opus 4.7)
  but kicks in as Notebook history grows — estimated ~70% savings
  on the system+tools prefix for multi-turn sessions. ADR-0006
  documents the strategy. Roadmap M6 "Skill Library Builder"
  (items 19–22) added before "After v1.0". Per-finding 📋 Copy
  button in the Skill Library's filtered view yanks
  "\[SEVERITY\] file (category): message" to the clipboard.

- **v0.24 (this commit, plus the failed Resources iterations and
  the doc maintenance pass).** Documentation reconciled
  end-to-end: README status updated to v0.24, CHANGELOG entries
  for v0.18–v0.24, HANDOFF expanded with the "Critical open bug"
  catalog + updated starting prompt + repo state, this Last
  Completed Task rewritten.

### Open critical bug — Skill Library Resources/Validation display

Nine layout iterations on the Resources list (and the Validation
list directly below it) all failed user smoke-test:
`13ed1c2` → `db9f214` → `21826cf` → `b7fd46d` → `66921ad` →
`28bd5c0` → `f68e219` → `2b141b7` → `7a29ec5` → `47b4710` →
`16f9468`. The data flow is correct
(`SkillLibraryServiceTests.GetResources_*` verify) but the user
reports content as "cut off". See HANDOFF "Critical open bug"
for the full pattern catalog and suggested next-investigation
steps. **DO NOT** attempt another layout tweak without first
asking the user for a precise failure description.

### Notes from the session worth keeping

- A full-codebase audit (Explore agent, session-3) catalogued 26
  findings across Bug-causing / Future-hazard / Inefficient
  buckets. Key ones already applied; the rest are listed in the
  HANDOFF "Optimizations" section with ✅ markers next to the ones
  that shipped this session.
- The audit's claim that `[NotifyPropertyChangedFor]` "auto-detects"
  what derived properties read is wrong — the attribute MUST be on
  the source `[ObservableProperty]` field for the notification to
  fire. Dropping manual `OnPropertyChanged` calls based on that
  audit finding briefly broke editor visibility (`7a29ec5` →
  fixed in `47b4710`). Convention added to HANDOFF's starting
  prompt.

**Next:** First, address the open Resources bug — but ASK before
another layout pass. Otherwise: **M3 #11 "Apply with AI"** for
documentation fix prompts (smallest-scope highest-leverage item per
HANDOFF), or the rest of M3 (persistent agent action log, AI call
log + cost tracking — telemetry would surface the prompt-caching
savings in-app). Tier 2 of the non-roadmap bucket (theme dictionary,
`MarkdownPresenter` style resource, VM folders) still available.
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
  "milestone boundaries" to "every update". See HANDOFF.md for the
  full protocol.

## Gotchas / Do Not Touch
- CommunityToolkit.Mvvm source generators require `partial` classes — missing
  `partial` silently breaks `[ObservableProperty]` / `[RelayCommand]`.
- Agent filesystem actions are scoped to user-configured project roots only;
  do not widen this scope without an explicit decision.

## Reference Docs
See @SPEC.md for the full feature and architecture spec.
See @KICKOFF.md for the first-task prompt (verify the build first).
<add further @imports here as deeper docs are created next to their code>
