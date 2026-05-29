# Build Task — Vision Audit Module (VybeDesk)

Add a Vision Audit module to the VybeDesk solution. This is a new feature module
alongside the existing ones (Documentation, Prompts, Session Builder, Notebook,
Skill Library, Bug Tracker, Testing Manager). Read `CLAUDE.md` first for project
conventions, then build this following the same layered pattern every existing
module uses.

The `vision-drift-detection` skill is available — apply it for the vision
extraction, the audit logic, and the report structure so they stay consistent
with that skill.

## Purpose

The Vision Audit externalizes the one discipline no other VybeDesk module
touches: catching *drift*. Every other module catches a local problem the user
already suspects — a bug, a stale doc, a missing test. Drift is different. It is
the slow, invisible divergence where every individual prompt-and-generate step
seemed fine, and the user looks up after weeks to find the project is not what
they set out to build. An experienced developer feels drift early by constantly
comparing the growing codebase against the project's intent. This module
performs that comparison for a user who cannot.

The obvious-but-wrong version of this module sends the whole codebase to the AI
with the question "does this match the plan?" Do NOT build that. A real codebase
is far too large for one request — it will fail outright or cost heavily for a
shallow answer. The module must instead use a deliberate, size-independent
strategy (see Build order, layers 4 and 5).

The module is project-scoped: the audit always operates on the currently
selected project.

## Build order (each layer compiles before the next)

### 1. Core model — `VybeDesk.Core`
Add a `VisionRecord` entity and an audit-result model:
- `VisionStatement` — one concrete, testable claim about what the project must
  do or be (e.g. "users can create an account"). Fields: an id, the statement
  text.
- `VisionRecord` fields: `Id` (Guid), `ProjectId` (Guid), a list of
  `VisionStatement`, `ApprovedAt` (DateTimeOffset?), `Created`/`Modified`.
  The vision is modeled as a LIST of statements, not one block of prose,
  because the audit works statement by statement — the data model matches the
  grain of the analysis.
- `StatementVerdict` — for one statement in one audit run: which statement, an
  alignment rank (`OnTrack`, `AtRisk`, `OffTrack`), the supporting evidence
  (string), a recommendation (string).
- An audit-mode enum: `AuditMode` — `Structural`, `Targeted`.

### 2. Persistence interface — `VybeDesk.Core`
Add `IVisionStore` with async methods mirroring the other stores:
`GetByProjectAsync(Guid projectId, ...)` (returns the record or null — a project
has at most one vision), `SaveAsync` (insert or update), `RemoveAsync`.
Do NOT add storage for audit reports — see Out of scope.

### 3. Persistence implementation — `VybeDesk.Services`
- Add a `vision_records` table to the schema in `Database.cs`, scoped by
  `project_id` exactly as the `bugs` and `testing_plans` tables are. Store the
  statement list as JSON TEXT, consistent with how prompt tags are stored.
- Add `SqliteVisionStore : IVisionStore`, following `SqliteBugStore`.

### 4. Audit service — `VybeDesk.Services`
Add an `IVisionAuditService` / `VisionAuditService`. It has four jobs:

- **Extract vision.** Read the project's documentation and draft a vision as a
  list of concrete `VisionStatement`s. REUSE the existing documentation-scanning
  capability from the Documentation module to find and read the docs — do NOT
  write a second, independent doc scanner. This is a deliberate, approved
  dependency on the Documentation module's scan logic.
- **Structural audit.** Gather only the *shape* of the project — folder/file
  structure, file and module names, the dependency manifest, and the docs — and
  assess each vision statement against that shape. This is size-independent: a
  project's shape stays small even when its code is vast. It catches large
  drift (e.g. a vision promising saved user data, audited against a project with
  no database dependency anywhere) but cannot catch behavioral drift inside
  correctly-named files. State that limit honestly in the output.
- **Targeted audit.** A two-phase process: first reason about which files are
  most relevant to the approved vision, then read a BOUNDED set of the most
  relevant files (cap the count — do not read everything that might be
  relevant, or the request can grow too large) and assess each statement
  against both the shape and those file contents.
- **Build outputs.** For either mode, produce (a) a report ranking every
  statement `OnTrack` / `AtRisk` / `OffTrack` with evidence and a concrete
  recommendation, exportable as markdown like the Documentation module's
  reconciliation report, and (b) a Claude Code deep-dive prompt naming the
  flagged areas and asking the agent to investigate them in the actual code and
  confirm or correct the findings.

### 5. View model — `VybeDesk.App`
`VisionAuditViewModel` (a `PageViewModel`) guiding a four-stage workflow:
- **Extract** — draft the vision from the project's docs.
- **Approve** — show the drafted vision; the user edits, adds, or deletes
  statements and must EXPLICITLY approve before anything audits. Nothing audits
  against an unapproved vision. This gate is mandatory — it guarantees the
  measuring stick is correct and forces the user to articulate intent.
- **Choose mode** — the user picks Structural or Targeted. Present this in
  PLAIN LANGUAGE with the trade-off visible: a quick structural check looks at
  the project's shape and is fast, free, and works at any size; a deeper
  targeted check also reads the most important files — more thorough, slower,
  uses more API budget. Do NOT present bare "Structural / Targeted" radio
  buttons; the target user does not know those terms.
- **Run & review** — run the chosen audit, show the per-statement report and
  the generated Claude Code prompt.

### 6. View — `VybeDesk.App`
`VisionAuditView` with stages, like the Testing Manager: when the project has no
approved vision, the extract-and-approve flow; once a vision is approved, a
settled screen to re-run audits, switch modes, view the latest report, and
regenerate the vision if the project has fundamentally changed. Rank colours
reuse the EXISTING `SeverityToBrushConverter` — `OffTrack` to red, `AtRisk` to
amber, `OnTrack` to green/blue. The report and prompt panels reuse the existing
read-only, copyable, exportable panel pattern.

### 7. Navigation
Wire the new page into the sidebar navigation shell alongside the other modules,
respecting the project-based structure.

## Cross-cutting requirements

- **Reuse the Documentation module's doc-scanning logic** for vision extraction
  and for the documentation part of the structural audit. Do not duplicate
  file-scanning logic — duplicated scanners drift apart and become a maintenance
  problem. This dependency is intentional and approved.
- Reuse `SeverityToBrushConverter` for rank colours; reuse the existing
  copyable/exportable report-panel pattern. Do not invent new equivalents.
- The targeted audit must cap the number of files it reads — a bounded, most
  relevant set — so a request can never grow too large to send.

## Tests — `VybeDesk.Tests`
Add xUnit tests for `SqliteVisionStore` (save then get-by-project returns the
record; a vision for project A does not appear for project B; remove works).
Test that an audit cannot run against a `VisionRecord` whose `ApprovedAt` is
null. Test that the structural audit produces a verdict for every vision
statement.

## Out of scope for this version
Do NOT build: the deep assessment where the app itself orchestrates a full
line-by-line investigation (this is the version-two flagship — the Claude Code
handoff is its version-one stand-in); a stored history of audit reports over
time (version-two enhancement — reports are transient and exportable in v1, not
stored); any background drift monitoring (the audit is run deliberately by the
user, not a watcher).

## When done
Run `dotnet build` and `dotnet test`, confirm green, and update the
"Last Completed Task" section of `CLAUDE.md` to record that the Vision Audit
module is complete and that it depends on the Documentation module's
doc-scanning logic.
