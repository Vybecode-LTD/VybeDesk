# Build Task — Bug Tracker Module (VybeDesk)

Add a Bug Tracker module to the VybeDesk solution. This is a new feature module
alongside the existing five (Documentation, Prompts, Session Builder, Notebook,
Skill Library). Read `CLAUDE.md` first for project conventions, then build this
following the same layered pattern every existing module uses.

The `bug-triage` skill is available — apply it for the severity model and the
fix-prompt generation logic so they stay consistent with that skill.

## Purpose

The Bug Tracker is not a passive log. It exists to quietly teach two disciplines
to users who lack a development background: triage (bugs are not equal and must
be sequenced) and reproducibility (a bug nobody can reliably trigger cannot be
reliably fixed). The structured entry form and the severity-sorted list ARE the
teaching — do not reduce this to a flat notepad.

Bugs are project-scoped: every bug belongs to exactly one project, and the
tracker shows bugs for the currently-selected project. There is no global bug
list.

## Build order (each layer compiles before the next)

### 1. Core model — `VybeDesk.Core`
Add a `Bug` entity and two enums:
- `BugSeverity` — `Critical`, `Major`, `Minor` (matches the bug-triage skill).
- `BugStatus` — `Open`, `Fixing`, `Fixed`, `WontFix`.
- `Bug` fields: `Id` (Guid), `ProjectId` (Guid), `Title` (string),
  `Severity` (BugSeverity), `Status` (BugStatus), `StepsToReproduce` (string),
  `ExpectedResult` (string), `ActualResult` (string), `Area` (string, short
  free-text for which screen/part), `Created` (DateTimeOffset).
The three reproduction fields are SEPARATE fields on purpose — the form
structure teaches the user to think reproducibly. Do not merge them into one
description field.

### 2. Persistence interface — `VybeDesk.Core`
Add `IBugStore` with async methods, mirroring `IProjectStore`'s shape:
`GetByProjectAsync(Guid projectId, ...)`, `AddAsync`, `UpdateAsync`,
`RemoveAsync`. Note `GetByProjectAsync` filters by project — bugs are scoped,
unlike the global prompt/note stores.

### 3. Persistence implementation — `VybeDesk.Services`
- Add a `bugs` table to the schema in `Database.cs`. Columns mirror the entity;
  store both enums as INTEGER exactly as `projects.status` already is. Index the
  `project_id` column.
- Add `SqliteBugStore : IBugStore` following `SqliteProjectStore` exactly —
  explicit column mapping, Guids as TEXT, timestamps as Unix INTEGER.
- Register `IBugStore -> SqliteBugStore` in `Program.cs`.

### 4. View model — `VybeDesk.App`
`BugTrackerViewModel` (a `PageViewModel`). Standard CRUD like
`PromptManagerViewModel`, plus three behaviors that matter:

- **Sequenced display.** The bug list MUST sort by severity first
  (Critical, then Major, then Minor), and within each severity, Open/Fixing
  bugs above Fixed/WontFix. The list answers "what do I fix next?" just by being
  looked at — do not sort by creation order.
- **Generate Fix Prompt command.** The user selects one or more bugs; this
  builds a Claude Code fix prompt — each bug with its full reproduction steps
  and expected-vs-actual, ordered by severity, instructing the agent to make the
  smallest correct change per bug and to flag anything it cannot reproduce
  rather than guessing. Follow the existing pattern in the Documentation
  module's fix-prompt generation. Output goes to a read-only panel the user can
  copy.
- **Fixed-means-tested nudge.** When a bug's status changes to `Fixed`, set a
  one-line status message asking whether a test exists that would catch the bug
  if it returned. This is a nudge only — it performs no action.

### 5. View — `VybeDesk.App`
`BugTrackerView`, master-detail like `PromptManagerView`/`SkillLibraryView`:
- Left: the bug list, severity-color-coded using the EXISTING
  `SeverityToBrushConverter` (red/amber/blue) so the color language matches the
  Documentation module.
- Right: the editor — title, severity selector, status selector, and the three
  reproduction fields, each clearly labeled with generous vertical space. If
  those fields look cramped or optional, users skip them — give them room.
- Top: summary chips counting bugs by severity (this count will later feed a
  project health dashboard).
- Near the list: the "Generate Fix Prompt" button and its read-only output
  panel.

### 6. Navigation
Wire the new page into the sidebar navigation shell alongside the other modules,
respecting the project-based structure (the tracker shows the
currently-selected project's bugs).

## Tests — `VybeDesk.Tests`
Add xUnit tests for `SqliteBugStore` covering: add then get-by-project returns
the bug; a bug from project A does not appear in project B's results; update
and remove behave correctly.

## Out of scope for this version
Do NOT build: screenshot attachments, per-bug activity history, or direct
bug-to-test linking. The fixed-means-tested nudge is the lightweight stand-in
until the Testing Manager module exists.

## When done
Run `dotnet build` and `dotnet test`, confirm green, and update the
"Last Completed Task" section of `CLAUDE.md` to record that the Bug Tracker
module is complete.
