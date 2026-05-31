# Claude Project Kit — install guide

A drop-in set of binding directives that guide Claude Code across **six domains**:
documentation, testing, debugging, version control, SEO/GEO, and software release
automation. `CLAUDE.md` is the entry point; it `@include`s one directive per domain.

## Install (per project)

1. **Unzip the contents of this kit into your project root** (next to your source).
   All files sit at the top level so the `@include` paths (local `@FILE.md`) resolve.
2. **Open `CLAUDE.md` and fill in every `REPLACE_WITH_*` placeholder** — project
   name, stack, repo, type, current state, structure.
3. **Set the project `Type`** in CLAUDE.md → Project Overview. It controls the two
   conditional directives:
   - `ONLY_IF_WEB_FACING` → SEO/GEO applies (skip for CLIs, libraries, backend-only).
   - `ONLY_IF_DESKTOP_DOWNLOAD_APP` → Software Release applies (skip for web apps /
     services / libraries — they deploy via their own path).
4. That's it. Start a Claude Code session in the project; `CLAUDE.md` loads
   automatically and pulls in the directives.

## What's in the kit

| File | Domain / role |
|---|---|
| `CLAUDE.md` | **Entry point.** Per-project header + goal-per-domain + the six `@include`s + mandatory workflows + quality gates. |
| `DOCUMENTATION_MANAGER.md` | Documentation lifecycle: managed docs, versioning, session-end handoff, reconciliation. |
| `TESTING_PROCEDURES.md` | Test-first, per-stack test stacks, required checks, evidence ledger. (`@include`s the debug protocol.) |
| `DEBUG_PROTOCOL.md` | Anti-loop circuit breaker: 2-strike rule → diagnostic mode; `BREAKLOOP`; verify with proof. |
| `VERSION_CONTROL.md` | Git discipline: never commit a secret, branching, conventional commits, history-rewrite protocol. |
| `SEO_OPTIMIZATION.md` | `ONLY_IF_WEB_FACING` — per-page SEO + GEO (AI-citation) rules. Deep reference: the catalog below. |
| `SOFTWARE_RELEASE.md` | `ONLY_IF_DESKTOP_DOWNLOAD_APP` — 3-stage, single-creator release pipeline. |
| `seo-research-catalog.md` | SEO encyclopedia (200+ techniques) referenced by the SEO directive. |
| `_CLAUDE-KIT-README.md` | This file. |

## Notes

- **Self-contained:** every cross-reference uses a local `@FILE.md` path, so the kit
  works the moment it's unzipped — no shared parent folder required.
- **Key phrases** like `perform handoff`, `perform audit`, `run tests`, `SEO audit`
  assume the matching **skill suites are installed globally** (`~/.claude/skills`,
  `~/.claude/agents`). The directives still apply and guide work even without the
  skills — you just run the steps manually.
- **Updating the kit:** edit the master copies, re-zip, and re-drop into projects.
  (The directives are vendored per project by design — self-contained beats DRY here.)
