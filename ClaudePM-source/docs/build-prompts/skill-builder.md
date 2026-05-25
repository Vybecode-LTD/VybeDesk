# Build Task — Skill Builder Module (ClaudePM)

Add a Skill Builder module to the ClaudePM solution. This is the eighth feature
module, alongside Documentation, Prompts, Session Builder, Notebook, Skill
Library, Bug Tracker, and Testing Manager. Read `CLAUDE.md` first for project
conventions, then build this following the same layered pattern every existing
module uses.

Two skills are available and should be active — apply both:
- `skill-design-workflow` — governs the end-to-end process this module runs.
- `skill-file-authoring` — governs the craft applied during the drafting step.

## Purpose

The Skill Builder turns the activity of designing a new agent skill — taking a
name, a rough description, and some notes, and producing a finished, validated
skill — into a feature inside ClaudePM. It sits one level up from the other
modules: it does not help work on a project, it extends the tool itself by
creating new capabilities the AI-driven parts of the app can draw on.

There are two obvious-but-wrong versions to avoid. The first is building the
Skill Builder with its OWN validation logic for what a valid skill is. Do NOT
do this — the Skill Library module already has that validation, and a second
copy will silently diverge from it. The Skill Builder MUST call the SAME
validation the Skill Library uses (see Cross-cutting). The second is adding a
new database table for skills. Do NOT — skills are files on disk, browsed by the
Skill Library; this module's substance is process and AI orchestration, not
stored data.

The Skill Builder is GLOBAL, not project-scoped. A skill is a general
capability used across all projects. Do not scope it to a project, even though
the recently built modules were project-scoped.

## Build order (each layer compiles before the next)

### 1. Core interface — `ClaudePM.Core`
Add an `ISkillBuilderService`. This module is process-oriented and needs little
or no new persisted data — do NOT add a new database table or store interface
for it. If a small model type is needed to carry the in-progress skill draft
through the workflow, add it, but the finished output is files on disk, not
database rows.

### 2. Service implementation — `ClaudePM.Services`
Add `SkillBuilderService : ISkillBuilderService`. It orchestrates the workflow:
- **Collect inputs** — skill name, rough description, notes.
- **Research toggle.** OFF: drafting runs directly from the inputs in one pass.
  ON: the AI first runs an INTERACTIVE refinement pass — it asks the user a
  focused set of clarifying questions (intended triggers, scope, concrete use
  cases, what the skill should NOT do) and their answers feed the draft. Be
  precise: the app has no web access, so this toggle means interactive
  clarifying questions, not internet research. Label it accordingly in the UI.
- **Draft** — generate the skill applying `skill-file-authoring` craft: a
  routing-style description (what it does, "use when" scenarios, literal
  triggers) within the character limit, and an imperative body that leads with
  the core principle and ends with anti-patterns.
- **Validate** — call the SAME validation the Skill Library uses (see
  Cross-cutting). Surface warnings so the user can fix the skill before saving.
- **Emit outputs** — produce BOTH forms: the single `.skill` file (frontmatter
  plus body) and a skill folder containing a `SKILL.md`. Write the folder to the
  user's configured skills location. Reuse the Skill Library's existing
  serialization logic to render skill text.

### 3. View model — `ClaudePM.App`
`SkillBuilderViewModel` (a `PageViewModel`) guiding a staged workflow: collect
inputs → (if research toggle on) interactive questions → draft → validate &
review → emit outputs. Holds the inputs, toggle state, interactive Q&A, the
draft, the validation results, and the final output.

### 4. View — `ClaudePM.App`
`SkillBuilderView`, a staged/wizard layout like the Session Builder and the
Testing Manager's questionnaire. When the research toggle is on, the wizard has
an extra middle stage for the interactive clarifying questions. Validation
results reuse the EXISTING `SeverityToBrushConverter` so a builder warning looks
identical to a Skill Library warning — they are the same warnings from the same
logic.

### 5. Navigation
Wire the new page into the sidebar navigation shell alongside the other modules.

## Cross-cutting requirements

- **Share the Skill Library's validation.** The Skill Builder MUST validate
  generated skills using the exact same validation logic the Skill Library
  module already uses (description-length check, reserved-word/"claude" name
  check, trigger-quality heuristic, empty-body check, etc.). If that logic is
  not already in a shared, callable place, refactor it into one and have BOTH
  modules call it. Do not copy it. Two copies of a validation rule are two rules
  that will diverge.
- **Reuse the Skill Library's serialization** for rendering a skill to `.skill`
  text. Do not write a second serializer.
- The Skill Builder and the Skill Library are two halves of one lifecycle — the
  builder is a skill's birth, the library its ongoing life. A skill created in
  the builder, saved to the configured skills location, must be a skill the
  Skill Library can immediately browse and validate identically.
- Reuse `SeverityToBrushConverter` for validation-result colours.

## Tests — `ClaudePM.Tests`
Add xUnit tests: a generated skill that violates a rule (e.g. over-length
description, reserved word in name) is reported by the shared validation; the
emitted `.skill` file and `SKILL.md` folder are both produced; a skill produced
by the builder passes the Skill Library's validation identically (proving the
shared-validation requirement actually holds).

## Out of scope for this version
Do NOT build: true internet research for skill subject matter (the app has no
web access — the toggle does interactive Q&A; web research is a version-two
possibility if web access is later wired in); skill versioning/history (that is
the Skill Library's territory to grow into, not the builder's); batch generation
of multiple skills at once (it multiplies the risk of many subtly flawed skills
— the builder produces one skill at a time).

## When done
Run `dotnet build` and `dotnet test`, confirm green, and update the
"Last Completed Task" section of `CLAUDE.md` to record that the Skill Builder
module is complete, that it shares validation and serialization with the Skill
Library, and that all four "software factory" features are now built.
