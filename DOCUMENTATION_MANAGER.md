# Documentation Manager — Operational Directive

> **This file is loaded via `@include` from CLAUDE.md. Every rule is imperative
> and must be followed without exception. These rules govern how project
> documentation is created, maintained, verified, and versioned using the
> session-orchestrator skill suite and its 8 subagents.**

> **Related directive — `ONLY_IF_DESKTOP_DOWNLOAD_APP → follow release directive`:** @SOFTWARE_RELEASE.md
> For a desktop app shipped as a downloadable installer/binary, releases follow that pipeline — and CHANGELOG.md (a managed doc) is the source for the release notes, so keep it current and correctly formatted. Web apps, services, and libraries skip it.

---

## Purpose

This directive ensures that project documentation is never stale, never
contradictory, and never out of sync with the actual codebase. Documentation
is treated as a living system — not a chore done at the end, but an integral
part of every code change. The session-orchestrator and its subagents handle
the mechanics; this file defines when and why they fire.

---

## The Documentation System

Seven managed documents form the project's knowledge base. Each has a
designated subagent responsible for its accuracy. Never edit these documents
by hand — always route changes through the responsible subagent via the
session-orchestrator.

### Document Registry

| Document | Owner Subagent | Purpose | Update Frequency |
|---|---|---|---|
| `CLAUDE.md` | `memory-updater` | Project constitution, current state, conventions | Every session end |
| `ROADMAP.md` | `roadmap-manager` | Tasks, milestones, goals, priorities | When tasks change |
| `BUGS.md` | `bug-fix-tracker` | Bug reports, fixes, regression test refs | When bugs found/fixed |
| `docs/TESTING.md` | `test-doc-manager` | Frameworks, procedures, test inventory, coverage | When tests change |
| `CHANGELOG.md` | `doc-versioner` | Versioned history of all project changes | Every version bump |
| `HANDOFF.md` | `handoff-builder` | Session-to-session continuity briefing | Every session end |
| `AUDIT-LOG.md` | `doc-reconciler` | Record of every audit and reconciliation | Every audit |

> **Path note (this project):** these managed docs live at the **repo root** —
> NOT under `docs/` — because `auto-release.yml` and the marketing site read
> root `CHANGELOG.md`. Only `TESTING.md` is under `docs/`. Read any `docs/<name>`
> mention elsewhere in this file as the repo-root `<name>`.
>
> **`CHANGELOG.md` is frontmatter-exempt** — it is consumed verbatim by
> `auto-release.yml` (release notes) and rendered by the marketing site, so it
> carries no YAML header; its version is implicit in its `## [x.y.z]` entries.

### Document Header Standard

Every managed document must begin with this YAML frontmatter. If a document
is missing its frontmatter, add it immediately before doing anything else
with that document.

```yaml
---
document: DOCUMENT_NAME
version: X.Y.Z
last-updated: YYYY-MM-DDTHH:MM:SSZ
last-audit: YYYY-MM-DDTHH:MM:SSZ
managed-by: session-orchestrator/SUBAGENT_NAME
---
```

### Version Synchronization Rule

All seven documents share a single version number. When any document
increments its version, every document must be updated to match. The
`doc-versioner` subagent enforces this. If you ever observe two documents
with different version numbers, that is a reconciliation failure — run
`perform audit` immediately.

---

## Automatic Triggers

The following rules define when documentation updates happen automatically
without the user asking. These are not suggestions — they are standing
orders.

### Trigger: Code Change Detected

When you modify, create, or delete any source file:

1. **New source file created** → Check if a corresponding test file exists. If not, note it as a documentation gap. When the test is created, the `test-doc-manager` must update `docs/TESTING.md` with the new test entry.

2. **Bug fix committed** → The `bug-fix-tracker` must update `docs/BUGS.md`: mark the bug as fixed, record the root cause, record which files changed, and verify a regression test exists. If no regression test exists, flag this as a P0 action item.

3. **New dependency added** → The `test-doc-manager` must update the Frameworks & Tools table in `docs/TESTING.md`. The `memory-updater` must update the Stack section of `CLAUDE.md` if it is a significant addition.

4. **Feature completed** → The `roadmap-manager` must mark the task complete in `docs/ROADMAP.md` with the completion date. The `session-chronicler` must record it in the session record. The `doc-versioner` must add a CHANGELOG entry.

5. **Architecture decision made** → If the team decides to switch a library, change an approach, or adopt a new pattern, record it in the session chronicle and ensure `CLAUDE.md` conventions section reflects the decision. Future sessions must not unknowingly reverse this decision.

### Trigger: Session Boundary

**Session start (every time):**
1. Read `CLAUDE.md` and `docs/HANDOFF.md`.
2. Run a quick staleness check: is the `last-updated` timestamp on any document older than 7 days while code has changed since then? If so, flag it immediately.
3. Run `quick check` (test orchestrator Gates 0-2) to verify the codebase is in a known-good state.

**Session end (every time, no exceptions):**
1. Run the full `perform handoff` flow:
   - `session-chronicler` captures all changes, features, bugs, decisions.
   - `doc-reconciler` cross-checks all documents for consistency.
   - `doc-versioner` increments version and timestamps on all changed docs.
   - `roadmap-manager` verifies task statuses match actual code state.
   - `bug-fix-tracker` verifies bug statuses match actual code state.
   - `test-doc-manager` verifies test inventory matches actual test files.
   - `handoff-builder` creates/updates `docs/HANDOFF.md` for the next session.
   - `memory-updater` syncs `CLAUDE.md` with final state.
2. If the user does not explicitly say "perform handoff" before ending a session, **remind them.** Say: "Before we wrap up, should I perform the handoff so the next session has full context?"

### Trigger: Pre-Deploy

Before any deployment:
1. Run `perform audit` to reconcile all documentation.
2. Verify `docs/CHANGELOG.md` has entries for everything being deployed.
3. Verify `docs/TESTING.md` coverage numbers match the actual latest test run.
4. Verify `docs/BUGS.md` has no CRITICAL/HIGH bugs marked open.
5. If any verification fails, block the deploy and report what is wrong.

### Trigger: Weekly Freshness (during nightly/scheduled work)

If a document's `last-updated` timestamp is older than 14 days and the
codebase has had commits in that period, the document is presumed stale.
Run `perform audit` to reconcile.

---

## Reconciliation Rules

Reconciliation is the process of cross-checking all documents against each
other and against the codebase to detect and fix drift. The `doc-reconciler`
subagent performs this, but these rules define what it checks.

### Cross-Document Consistency Checks

1. **ROADMAP ↔ Code:** Every task marked "complete" in ROADMAP.md must correspond to actual implemented code. Every task marked "in progress" must have corresponding work visible in the codebase (branch, modified files, or partial implementation). If a task is marked complete but the feature does not exist, that is a CRITICAL reconciliation failure.

2. **BUGS ↔ Code:** Every bug marked "fixed" in BUGS.md must have its fix present in the codebase and a regression test that passes. Every bug marked "open" must still be reproducible. If a bug is marked open but has been silently fixed (perhaps as a side effect of other work), update its status.

3. **TESTING ↔ Test Suite:** Every test file listed in TESTING.md must actually exist on disk. Every framework listed must be in the project's dependency file. Coverage percentages must match the most recent test run output. If TESTING.md says coverage is 82% but the last run shows 78%, that is a HIGH reconciliation failure.

4. **CLAUDE.md ↔ Reality:** The "Last completed task" must match the actual most recent work. The "Active task" must match what is currently being worked on. The "Open bugs" count must match BUGS.md. The project structure tree must match the actual directory layout. Referenced paths must exist on disk.

5. **HANDOFF ↔ Current State:** The "Next Steps" in HANDOFF.md must still be relevant. The "Blockers" must still be active. The "Warnings" must still apply. Stale handoff items mislead the next session.

6. **CHANGELOG ↔ Versions:** Every version number that appears in any document's frontmatter must have a corresponding entry in CHANGELOG.md. If version 1.5.0 exists in frontmatter but CHANGELOG.md has no [1.5.0] entry, that is a HIGH reconciliation failure.

7. **Cross-references:** Any document that references another document by name or path must reference something that exists. Broken cross-references are MEDIUM reconciliation failures.

### Reconciliation Severity Levels

| Severity | Definition | Required Response |
|---|---|---|
| CRITICAL | Documents contradict observable code state | Fix immediately, do not proceed with other work |
| HIGH | Document references nonexistent items or has wrong data | Fix before session ends |
| MEDIUM | Stale but not actively misleading | Fix during next audit |
| LOW | Cosmetic, formatting, minor timestamp issues | Fix when convenient |

### Audit Logging

Every reconciliation, whether triggered manually via `perform audit` or
automatically at session boundaries, must be recorded in `docs/AUDIT-LOG.md`
with the date, the findings, the severity of each finding, and the resolution
applied. This creates an accountability trail — you can always answer "when
did this document last get verified?"

---

## Version Increment Rules

The `doc-versioner` subagent handles version numbers, but these rules define
when each increment type applies.

**PATCH increment (0.0.x):** Reconciliation fixes, typo corrections, formatting
updates, timestamp refreshes where content did not meaningfully change. A patch
increment means "the docs were tidied up but no new information was added."

**MINOR increment (0.x.0):** Session-level work that adds meaningful content.
This includes new features documented, bugs fixed and recorded, new tests
documented, roadmap tasks completed or added, and any session that involved
actual development work. Most session-end handoffs trigger a MINOR increment.

**MAJOR increment (x.0.0):** Project milestone completed, phase transition,
or a significant structural change to the documentation system itself. Major
increments should be rare — roughly aligned with the milestones in ROADMAP.md.

When incrementing, reset all lower segments to zero. Version 1.5.3 with a
MINOR increment becomes 1.6.0, not 1.6.3.

---

## Document Creation

When any of the seven managed documents does not yet exist, the responsible
subagent must create it with the correct frontmatter, initial structure, and
version 0.1.0. This happens automatically the first time `perform handoff`
or `perform audit` is run on a new project.

The creation order matters because later documents reference earlier ones:

1. `CLAUDE.md` — must exist first (it is the project root context)
2. `docs/ROADMAP.md` — defines what the project is building
3. `docs/BUGS.md` — tracks defects (starts empty)
4. `docs/TESTING.md` — documents the test suite
5. `docs/CHANGELOG.md` — records version history
6. `docs/HANDOFF.md` — built from all of the above
7. `docs/AUDIT-LOG.md` — records reconciliation history

If the `docs/` directory does not exist, create it.

---

## Proactive Staleness Prevention

Rather than waiting for documents to become stale and then fixing them, these
rules prevent staleness from occurring in the first place.

**Rule 1: Update at the point of change, not later.** When you fix a bug, update BUGS.md in the same logical step — not "after we finish everything." When you complete a task, mark it in ROADMAP.md immediately. The session-orchestrator subagents handle this, but the principle is that documentation updates are part of the work, not a separate phase after the work.

**Rule 2: Every session ends with verification.** The `perform handoff` flow includes a reconciliation pass. This means that even if something was missed during the session, it gets caught at the boundary. No session should end with unverified documentation.

**Rule 3: Timestamps are enforceable contracts.** The `last-updated` field in each document's frontmatter is not decorative. If a document claims it was last updated on May 28 but the code it describes changed on May 30, the document is stale. The reconciler checks for this.

**Rule 4: The HANDOFF.md is the canary.** If HANDOFF.md accurately describes the project state, the documentation system is healthy. If a new session starts and HANDOFF.md is wrong about what was last completed or what the current blockers are, the documentation system has failed. Treat HANDOFF.md accuracy as the primary health indicator.

**Rule 5: When in doubt, audit.** If there is any uncertainty about whether documentation is current — after a long break between sessions, after a particularly chaotic session, after merging a large PR — run `perform audit`. It is cheap (< 1 minute) and catches everything.

---

## Key Phrases Reference

These phrases activate the session-orchestrator and its subagents. They can
be used at any time during a session.

| Phrase | What happens |
|---|---|
| `perform audit` | Full reconciliation of all 7 docs, version bump, audit log entry |
| `perform handoff` | Chronicle + audit + HANDOFF.md + CLAUDE.md sync + version bump |
| `display tasks` | Read-only display of current ROADMAP.md |
| `update roadmap` | Modify tasks, milestones, or priorities in ROADMAP.md |
| `update memory` | Sync CLAUDE.md with current project state |
| `update goals with <items>` | Add new goal items to ROADMAP.md goals section |
| `initialize project docs` | Create all 7 managed documents from scratch |

---

## Anti-Patterns — What NOT to Do

**Do not edit managed documents directly.** If you modify ROADMAP.md by hand instead of going through the `roadmap-manager`, the version number won't increment, the changelog won't get an entry, and the audit trail breaks. Always route through the orchestrator.

**Do not skip the session-end handoff.** Every session without a handoff is context permanently lost. The next session will start blind, waste time rediscovering what was done, and risk duplicating or contradicting work.

**Do not treat documentation as optional.** In this system, documentation is a first-class artifact with versioning, auditing, and reconciliation. Skipping it is equivalent to skipping tests — it creates debt that compounds.

**Do not audit after every small change.** Auditing is for session boundaries and pre-deploy gates. Over-auditing wastes time and creates noise in the audit log. The automatic triggers defined above are sufficient.

**Do not use HANDOFF.md as a scratchpad.** HANDOFF.md is specifically for inter-session context transfer. It gets overwritten at every session end. Notes, ideas, and research belong in the conversation or in dedicated project docs, not in HANDOFF.md.
