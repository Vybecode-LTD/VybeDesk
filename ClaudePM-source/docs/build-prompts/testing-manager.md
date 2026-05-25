# Build Task — Testing Manager Module (ClaudePM)

Add a Testing Manager module to the ClaudePM solution. This is a new feature
module alongside the existing ones (Documentation, Prompts, Session Builder,
Notebook, Skill Library, Bug Tracker). Read `CLAUDE.md` first for project
conventions, then build this following the same layered pattern every existing
module uses.

The `test-strategy-selection` skill is available — apply it for the strategy
questionnaire logic and the recommendations so they stay consistent with that
skill.

## Purpose

The Testing Manager externalizes a discipline that lives invisibly in an
experienced developer's head: knowing what kind of testing a project needs, how
to get it set up, and how to keep it running so regressions are caught
automatically instead of discovered weeks later as a pile of mysterious bugs.
The target user has no development background and does not carry this knowledge.

The obvious-but-wrong version of this module is a settings page where the user
picks a framework from a dropdown. That fails the target user, because the
question "which framework?" already presumes knowledge they do not have. Do NOT
build a framework-picker. Build a guided experience that asks plain-language
questions, recommends a strategy with its reasoning, and only then deals with
frameworks.

This version MANAGES and GENERATES testing setup — it does NOT execute tests.
Test execution and result display are a deliberately deferred version-two
feature (see Out of scope).

The module is project-scoped: each project has its own testing configuration.

## Build order (each layer compiles before the next)

### 1. Core model — `ClaudePM.Core`
Add a `TestingPlan` entity and supporting enums:
- `TestKind` — e.g. `Unit`, `Integration`, `EndToEnd`, `ManualChecklist`.
- `TestingPlan` fields: `Id` (Guid), `ProjectId` (Guid), the chosen strategy
  summary (string), the framework(s) the strategy implies (a list), the
  `TestKind`s the strategy calls for (a list), the user's questionnaire answers
  (stored so the reasoning can be revisited and the questionnaire re-run later),
  and `Created`/`Modified` timestamps.
Store the questionnaire answers, not just the conclusion — a project's needs
change as it grows, and the user must be able to see why a strategy was chosen
and re-run the questionnaire if the project outgrew it.

### 2. Persistence interface — `ClaudePM.Core`
Add `ITestingPlanStore` with async methods mirroring the other stores:
`GetByProjectAsync(Guid projectId, ...)` (returns the plan or null — a project
has at most one), `SaveAsync` (insert or update), `RemoveAsync`.

### 3. Persistence implementation + framework catalog — `ClaudePM.Services`
- Add a `testing_plans` table to the schema in `Database.cs`, scoped by
  `project_id` exactly as the `bugs` table is. Store enums as INTEGER; store the
  framework list and questionnaire answers as JSON TEXT, consistent with how
  prompt tags are already stored.
- Add `SqliteTestingPlanStore : ITestingPlanStore`, following `SqliteBugStore`.
- Add a **framework setup-prompt catalog** as a built-in service. This is NOT
  user data and does NOT go in the database — it ships with the app. Structure
  it as an EXTENSIBLE, data-shaped collection: each entry is a self-contained
  record carrying the framework name, the language/ecosystem it belongs to, the
  `TestKind` it serves, and a parameterized setup-prompt template. Adding a
  framework later must mean adding one data entry, not editing logic.
  Ship these starting entries:
  - xUnit — .NET / C# — unit & integration
  - GoogleTest — C++ — unit
  - pytest — Python — unit & integration
  - Vitest — JavaScript / TypeScript — unit & integration
  - Jest — JavaScript / TypeScript — unit (noted as the established alternative)
  - React Testing Library — React (runs on top of Vitest/Jest) — component
  - Playwright — web end-to-end (any front end) — end-to-end
  Each setup-prompt template must instruct Claude Code not just to add the
  framework but to establish the test folder layout and write one example test
  that establishes the pattern.
  Represent **database testing** as integration testing within the project's
  language framework (xUnit, pytest, etc.) — NOT as a separate framework. The
  module should gently correct the user if they expect a standalone "database
  test framework".

### 4. View model — `ClaudePM.App`
`TestingManagerViewModel` (a `PageViewModel`) with three layers of behavior:

- **Strategy questionnaire (middle layer).** A short, friendly, plain-language
  interview — what kind of thing are you building, how important is reliability,
  solo or with others — with answers offered as clear choices, not free text,
  wherever possible. On completion, produce a recommended strategy AND show its
  reasoning. The user can accept or adjust it; accepting creates/updates the
  project's `TestingPlan`.
- **Framework setup-prompt generation (foundation layer).** Once a `TestingPlan`
  exists, a command generates the Claude Code setup prompt(s) for the framework
  the strategy implies, drawn from the catalog and parameterized by the project.
  Output goes to a read-only panel the user can copy — follow the existing
  fix-prompt panel pattern in the Documentation and Bug Tracker modules.
- **Regression discipline (top layer).** Generates a "write a regression test"
  prompt, and surfaces a post-significant-change reminder to run tests before
  deploying. See Cross-cutting for the bug-tracker connection.

### 5. View — `ClaudePM.App`
`TestingManagerView` with TWO states, not a master-detail list:
- If the selected project has no `TestingPlan`: a stepped questionnaire state,
  closer to the Session Builder's wizard layout than to a list.
- If the project has a `TestingPlan`: a calmer single screen showing the current
  strategy, its reasoning, the generate-setup-prompt action, and the regression
  prompts.
Which state shows depends on whether the project already has a `TestingPlan`.

### 6. Navigation
Wire the new page into the sidebar navigation shell alongside the other modules,
respecting the project-based structure.

## Cross-cutting requirements

- **Bug Tracker connection — keep it loosely coupled.** When a bug is marked
  `Fixed` in the Bug Tracker, the Testing Manager should offer to generate a
  regression-test prompt for that bug. Do NOT have the Bug Tracker call directly
  into the Testing Manager. Introduce a small shared notification/event — the
  Bug Tracker raises a "bug fixed in project X" event; the Testing Manager
  listens for it. The two modules share only the knowledge that this event
  exists, nothing of each other's internals.
- Reuse existing patterns: the read-only copyable prompt panel already exists in
  the Documentation and Bug Tracker modules — reuse that pattern, do not invent
  a new one. The stepped-wizard layout already exists in the Session Builder —
  follow it for the questionnaire state.

## Tests — `ClaudePM.Tests`
Add xUnit tests for `SqliteTestingPlanStore` (save then get-by-project returns
the plan; a plan for project A does not appear for project B; remove works) and
for the framework catalog (every entry has a non-empty name, language, and
setup-prompt template; looking up a framework by language returns the expected
entries).

## Out of scope for this version
Do NOT build: test execution (running `dotnet test` / `npm test` / etc. and
parsing results), any red/green result dashboard, individual-test tracking, or
test coverage metrics. Execution is the planned version-two flagship feature,
and the `TestingPlan` data model built here is deliberately the foundation it
will stand on. Do not chase coverage numbers — the strategy-selection skill
explicitly warns against that anti-pattern.

## When done
Run `dotnet build` and `dotnet test`, confirm green, and update the
"Last Completed Task" section of `CLAUDE.md` to record that the Testing Manager
module is complete and to note the new bug-fixed event that couples it to the
Bug Tracker.
