# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
**Tier 1 non-roadmap optimizations — four commits.** A clean batch of
safety, test-coverage, UX, and documentation work pulled from the
HANDOFF wishlist. Build green, 45 / 45 tests pass.

- `b7ac51f` **Safety hardening: symlink resolution + 429/503/529
  retry backoff.** `AgentActionService.TryConfine` now walks the
  requested path segment-by-segment and resolves any existing
  segment that is a symlink/junction to its final target (same for
  the roots in `SetScopedRoots`), so a junction planted under a
  scoped root can no longer be used to escape it. New test creates
  a real symlink and confirms the validator rejects writes through
  it (graceful skip if the test process lacks the privilege).
  `AnthropicChatService` gains `SendWithRetryAsync` — up to 3
  retries on 429 / 503 / 529, honors `Retry-After` when present,
  otherwise exponential backoff from 1 s with jitter (capped at
  1 min). Both `CompleteAsync`/`ChatAsync` (non-streaming) and
  `AgentChatAsync` (streaming) flow through it; the request gets
  rebuilt each attempt because `HttpRequestMessage` is single-use.

- `00e82e4` **Test coverage for audit JSON parsing.** Nine
  golden-input tests against `AuditAsync` covering the response
  shapes Claude actually returns: clean JSON, ```json``` fenced
  JSON, JSON with leading prose, JSON with trailing prose, mixed
  casing + trailing commas, malformed JSON, no JSON at all, items
  with blank titles, and severity-sorted inconsistencies. Routes
  through the public method (mocked `IAiService`) so
  `ExtractJsonObject` + `ParseAuditPayload` stay private — no
  `InternalsVisibleTo`.

- `d37aa74` **UX micros: thinking-skeleton bubble + Ctrl+Enter
  send + Ctrl+S save.** `NotebookMessage.ShowThinkingPlaceholder`
  (`IsAssistant && !HasText`) drives an italic "thinking…"
  placeholder in the empty bubble between Send and the first
  streamed character. `Ctrl+Enter` KeyBinding on the Notebook
  input fires `SendCommand` (watermark updated). `Ctrl+S`
  KeyBinding on the doc editor TextBox fires `SaveEditorCommand`.

- *(this commit)* **`docs/adr/` folder with five decision
  records.** ADR-0001 Markdig over Markdown.Avalonia (with the
  blanking history), ADR-0002 direct HTTPS over the Anthropic
  SDK, ADR-0003 DPAPI for API key storage (Windows-first),
  ADR-0004 no iteration cap on the Notebook auto-loop (with the
  guidance to add cycle detection — not a global cap — when M3
  "Apply with AI" lands), ADR-0005 audit as structured-JSON not
  tool_use. Plus a `docs/adr/README.md` index + "when to write a
  new ADR" guidance.

**Next:** Tier 2 of the non-roadmap bucket (theme dictionary →
prereq for M5 light theme; `MarkdownPresenter` as a reusable style
resource; extract VMs into per-module folders), or pick up the
roadmap with **M3 #11 "Apply with AI"** for documentation fix
prompts (smallest-scope highest-leverage item per HANDOFF).
NOTE: the handoff skill is named `cc-handoff` ("claude" is
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
- **End-of-milestone smoke test.** At the close of every roadmap milestone
  — and at the close of any batch of commits the user agreed on as a unit
  (e.g. "Tier 1 / A→B→C→D") — launch the app
  (`dotnet run --project src/ClaudePM.App`, background) and wait for the
  user to visually verify before declaring done. Build-green +
  tests-green prove code correctness, not feature correctness. Tell the
  user explicitly *what to verify* and then wait — don't queue the next
  task in the same turn. See HANDOFF.md for the full protocol.

## Gotchas / Do Not Touch
- CommunityToolkit.Mvvm source generators require `partial` classes — missing
  `partial` silently breaks `[ObservableProperty]` / `[RelayCommand]`.
- Agent filesystem actions are scoped to user-configured project roots only;
  do not widen this scope without an explicit decision.

## Reference Docs
See @SPEC.md for the full feature and architecture spec.
See @KICKOFF.md for the first-task prompt (verify the build first).
<add further @imports here as deeper docs are created next to their code>
