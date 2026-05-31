# CLAUDE.md — Project Constitution

> **Mandatory reading. Every rule is imperative. No exceptions.**
>
> This is the **entry point**. It states the overall goals for every project and
> `@include`s one binding directive per domain. **To start a new project:** copy
> this file plus the six directive files (`DOCUMENTATION_MANAGER.md`,
> `TESTING_PROCEDURES.md`, `DEBUG_PROTOCOL.md`, `VERSION_CONTROL.md`,
> `SEO_OPTIMIZATION.md`, `SOFTWARE_RELEASE.md`) and `seo-research-catalog.md` into
> the project root, then fill in the `REPLACE_WITH_*` placeholders below. The
> `@include` paths are local, so the set is self-contained once copied.

---
document: CLAUDE
version: 0.3.0
last-updated: 2026-05-31
last-audit: 2026-05-31
managed-by: session-orchestrator/memory-updater
---

## Project Overview  *(fill in per project)*

- **Name:** VybeDesk
- **Description:** AI-powered project manager for Claude-Code-driven work (Windows desktop app)
- **Stack:** Avalonia 11.3 / .NET 9 / CommunityToolkit.Mvvm / SQLite / direct Anthropic HTTPS
- **Repo:** https://github.com/Vybecode-LTD/VybeDesk.git
- **Type:** desktop download app (Software Release directive applies) + web-facing marketing site
  (SEO directive applies to the `Vybecode-LTD/VybeDesk-Website` repo)

## Current State  *(fill in per project; kept current by `update memory`)*

- **Phase:** Shipping (v1.1.0 released; plugin/extension system landed on `main`, unreleased)
- **Last completed task:** Plugin/extension system (2026-05-31). New `VybeDesk.Plugin.Abstractions`
  SDK, catalog-driven sidebar, collectible-`AssemblyLoadContext` loader (`plugin.json` manifest +
  host-version gate), Settings→Plugins management UI, `samples/HelloWorldPlugin`, a CI guard
  (`plugin-sdk.yml`), 19 unit tests, `docs/PLUGINS.md` + ADR-0007, and NuGet + `dotnet new
  vybeplugin` packaging. Two commits on `main` (`d280463`, `9c73167`) — NOT pushed. See the
  top STOP block in `HANDOFF.md`.
- **Active task:** none — clean stopping point. Candidate next: push + publish the SDK/template
  nupkgs; finish the uncommitted Claude-Kit retrofit; roadmap M3 #12-13 / M5 #18 / M6 / M7;
  cross-platform (DPAPI `ISecureKeyStore` is the blocker).
- **Coverage:** 323/323 tests pass · **Open bugs:** 0 · **Doc version:** 0.3.0

---

# THE DIRECTIVE SYSTEM — overall goals + binding directives

Six domains. Each has a one-line **goal** (the standing intent for every project)
and an `@include` that pulls in the binding directive. Two are **conditional** —
they apply only to matching project types.

## 1. Documentation & knowledge continuity
**Goal:** documentation is a living, versioned system — never stale, never
contradictory, never out of sync with the code. Every session ends with a handoff.

@include DOCUMENTATION_MANAGER.md

## 2. Testing
**Goal:** evidence over confidence. Tests are part of the implementation, not
follow-up. Every bug fix gets a regression test that fails before and passes after.

@include TESTING_PROCEDURES.md

## 3. Debugging (anti-loop circuit breaker)
**Goal:** never loop on blind fixes. On the 2nd failed attempt (or `BREAKLOOP`),
freeze edits and diagnose with evidence; verify with proof, not assertion.

@include DEBUG_PROTOCOL.md

## 4. Version control
**Goal:** clean, recoverable history; **no secret ever committed**; no surprise
force-pushes; commits a future reader can understand and bisect.

@include VERSION_CONTROL.md

## 5. SEO / GEO  —  `ONLY_IF_WEB_FACING`
**Goal:** every public page is optimized for both classic search (Googlebot) and
AI answer engines (GEO). *Skip if the project has no public HTML.*

@include SEO_OPTIMIZATION.md

## 6. Software release automation  —  `ONLY_IF_DESKTOP_DOWNLOAD_APP`
**Goal:** ship desktop releases through one race-free, automated pipeline (local
build → CI is the single release creator → website reads live). *Skip for web
apps / services / libraries — they deploy via their own path.*

@include SOFTWARE_RELEASE.md

---

## MANDATORY WORKFLOWS — READ AND FOLLOW

### Session Start
1. Read this file completely, then `HANDOFF.md` if it exists.
2. Run `quick check` (lint + unit tests) to establish a green baseline.
3. If tests are red, **fix them before anything else.**
4. Announce: what the last session completed, what's active, any blockers.

### While Working
1. After every significant change: lint + affected tests.
2. Bug fix → write the failing test FIRST (`@DEBUG_PROTOCOL.md` / `@TESTING_PROCEDURES.md`).
3. New file → create its test file immediately.
4. New dependency → update `docs/TESTING.md` frameworks table + the Stack above.
5. Update docs **at the point of change**, not later. Never commit code that fails lint.

### Session End — NEVER SKIP
When the user says "perform handoff" or the session is ending: reconcile all docs,
update `HANDOFF.md` + `CLAUDE.md`, log the audit. If the user forgets, **remind them.**
A session is not complete until the handoff is done.

### Pre-Deploy / Pre-Release
Run the full quality gate. ALL gates pass — no "fix it after." Security scan shows
zero CRITICAL/HIGH. Coverage meets the deploy threshold. Docs audited. For desktop
releases, follow `@SOFTWARE_RELEASE.md`.

---

## QUALITY GATES (quick reference)

**Coverage thresholds:** PR 85% line · Deploy 95% · new code 95% · security-critical
(auth/payment/data) 95%. No commit reduces coverage. *(Full methodology: `@TESTING_PROCEDURES.md`.)*

**Key phrases** (session-orchestrator, if installed): `perform audit` · `perform
handoff` · `display tasks` · `update roadmap` · `update memory` · `run tests` ·
`quick check` · `security scan` · `pre-deploy`.

**Managed docs** (one shared version, YAML frontmatter, never hand-edited): `CLAUDE.md`,
`ROADMAP.md`, `BUGS.md`, `docs/TESTING.md`, `CHANGELOG.md`, `HANDOFF.md`, `AUDIT-LOG.md`.
These live at the **repo root** (except `docs/TESTING.md`) because `auto-release.yml`
and the marketing site read root `CHANGELOG.md`. *(Details: `@DOCUMENTATION_MANAGER.md`.)*

---

## CODE CONVENTIONS

- Use `python -m pip install` (never bare `pip`).
- Commits reference task IDs; conventional format — see `@VERSION_CONTROL.md`.
- **Per-stack (auto-detected):**
  - **Python/FastAPI:** ruff (lint) · mypy (types) · pytest · Black.
  - **C#/.NET:** dotnet format · xUnit + NSubstitute + FluentAssertions · nullable enabled.
  - **React/TypeScript:** strict mode · Vitest + RTL · Playwright (E2E) · ESLint zero-warnings.
  - **C++/JUCE:** Catch2 · pluginval strictness 10 · ASan/UBSan (debug) · clang-tidy.

---

## PROJECT STRUCTURE

```
VybeDesk/  (on-disk folder …/Development/claudePM; GitHub repo: VybeDesk)
├── CLAUDE.md                 ← entry point (this file)
├── DOCUMENTATION_MANAGER.md  ┐
├── TESTING_PROCEDURES.md     │
├── DEBUG_PROTOCOL.md         │ the six binding directives
├── VERSION_CONTROL.md        │ (vendored per project)
├── SEO_OPTIMIZATION.md       │
├── SOFTWARE_RELEASE.md       ┘
├── seo-research-catalog.md   ← SEO deep reference   ·   _CLAUDE-KIT-README.md ← kit install guide
│
├── ROADMAP.md  BUGS.md  CHANGELOG.md  HANDOFF.md  AUDIT-LOG.md   ← managed docs (root)
├── README.md  AGENTS.md  SPEC.md  KICKOFF.md                     ← project docs
├── VybeDesk.sln  global.json  installer.iss  build-installer.bat
├── .github/workflows/        ← auto-release.yml · plugin-sdk.yml
│
├── src/
│   ├── VybeDesk.Core/                 ← models + service interfaces (no framework deps)
│   ├── VybeDesk.Services/             ← SQLite · AI client · DPAPI · Plugins/ (loader)
│   ├── VybeDesk.Plugin.Abstractions/  ← the public plugin SDK
│   └── VybeDesk.App/                  ← Avalonia UI · ViewModels · Views · DI root
├── tests/VybeDesk.Tests/     ← xUnit + Avalonia.Headless (AppSmoke/, Doubles/)
├── samples/HelloWorldPlugin/ ← reference plugin
├── templates/                ← `dotnet new vybeplugin` template
├── docs/                     ← TESTING.md (managed) + ARCHITECTURE, USER_GUIDE,
│                                PLUGINS, adr/, LAYOUT_REGRESSION, build-prompts/…
├── releases/latest/          ← committed Windows installer
└── scripts/                  ← Invoke-Release.ps1 (Stage 1 release)
```

---

## REMINDERS

- **A session is not complete without a handoff.**
- **Code is not ready without tests** (fail-before, pass-after).
- **Never `git add -A` without checking the staged diff for secrets.**
- **One release creator** — never let the local script and CI both create the release.
- **Documentation is not optional.**
