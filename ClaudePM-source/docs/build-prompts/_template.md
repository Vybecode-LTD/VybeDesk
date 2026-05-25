# Build Prompt Template

> A reusable structure for writing a task prompt that hands a module or feature
> build to a coding agent (e.g. Claude Code). Replace every <PLACEHOLDER>, then
> delete this note. The structure matters: each section prevents a specific way
> a coding agent tends to build the wrong thing. Keep the section order.

# Build Task — <FEATURE OR MODULE NAME> (<PROJECT NAME>)

<One or two sentences naming what is being built and where it fits — e.g.
"a new feature module alongside the existing ones". Tell the agent to read the
project's context file (CLAUDE.md / AGENTS.md) first for conventions.>

<If a skill is relevant, name it here and say it should be active so the build
stays consistent with it.>

## Purpose

<This section is the most important and the most often skipped. State what the
thing is FOR — the underlying intent — not just what it is. Then name the
obvious-but-wrong version the agent will tend to build, and explicitly warn
against it. A coding agent builds the most obvious interpretation of a request;
if the obvious interpretation is wrong, only an explicit warning here will
steer it away before it writes code.>

<If the feature is scoped to a parent entity (a project, a user, a workspace),
state that scoping rule here plainly.>

## Build order (each layer compiles before the next)

<Decompose the work into ordered, individually-compilable layers. A coding agent
is far more reliable when a large task is checkpointed than when it is asked to
build everything at once. Typical layering for a layered app: data model →
persistence interface → persistence implementation → view model / logic →
view / UI → wiring into navigation or entry points. Adapt to the project.>

### 1. <Layer name — e.g. Core model>
<Exactly what to add. Name types, fields, and their purpose. Wherever the
obvious implementation would diverge from the intended one, name the divergence
and explain it — e.g. "keep these three fields separate; do not merge them,
because <reason>".>

### 2. <Layer name>
<...>

### 3. <Layer name>
<...continue for as many layers as the feature needs...>

## Cross-cutting requirements

<Anything that spans layers: reuse an existing component rather than writing a
new one (name it), follow an existing pattern (point to where it already lives
in the codebase), keep coupling between modules loose via events rather than
direct calls, etc. Naming existing assets to reuse is critical — left unsaid,
an agent writes a fresh, slightly-divergent duplicate.>

## Tests

<What to test and at what level. Be specific about the cases that matter — do
not just say "add tests".>

## Out of scope for this version

<List what NOT to build. Without this, an agent that finishes early adds
deferred features and reintroduces complexity you intentionally cut. Name the
deferred items and, if useful, when they are planned for.>

## When done

<Tell the agent to build and test, confirm green, and update the project's
context file (the "Last Completed Task" section) so the next session has
current state. This keeps the context file accurate automatically.>
