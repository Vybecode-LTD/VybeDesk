# ClaudePM Roadmap — v1.0

> Forward-looking. For what's already shipped, see [CHANGELOG.md](CHANGELOG.md).
> For what exists today, see [docs/USER_GUIDE.md](docs/USER_GUIDE.md) and
> [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

The road from v0.9 (today) to v1.0 is five milestones. Each milestone stands
alone — you can stop after any of them and have a coherent app — but they
compound into the cohesive v1.0 experience.

Scope tags: **S** = single session, **M** = a couple sessions, **L** = bigger.

## Milestone 1 — Out-of-the-box useful  ✓ SHIPPED

> Commits: `942d864` (M1.2 Open in Claude Code), `3c2c6bc` (M1.3 Cancel),
> `7c83547` (M1.4 read-only tools + Notebook UX overhaul), `8044ea9` (M1.5
> curated prompts + M1 close-out). M1.1 (light theme) deferred to M5 polish
> — proper `DynamicResource` migration is M-scope, not S-scope.

First-launch experience should feel populated, not empty.

1. **Curated prompts library seed** *(M)* — ~30 prompts in 5 categories seeded
   on first DB creation. Categories: Doc/VCS hygiene, Testing & regression,
   Efficient task execution, New session starters, Common dev tasks.
   See appendix at the bottom of this file for the prompt content sketch.
2. **Read-only Notebook tools `read_file` + `list_directory`** *(S)* —
   Auto-execute (no preview gate, they're read-only). Lets Claude inspect
   before proposing edits and kills "Claude guessed wrong because it couldn't
   see what was there."
3. **"Open in Claude Code" button per project** *(S)* — Launch `claude-code`
   if on PATH; copy `cd <path> && claude` to clipboard otherwise.
4. **Cancel button on long AI calls** *(S)* — `CancellationTokenSource` per
   active call wired to a visible Cancel chip in the busy state.
5. **Light theme** *(S)* — `Application.RequestedThemeVariant` driven by a
   new `AppSettings.Theme`, exposed as a toggle in Settings.

## Milestone 2 — Author & maintain docs in-app  ✓ SHIPPED

> Commits: `b9a250d` (M2.6 inline editor + M2.7 watch mode), `5810c49`
> (M2.8 Markdig-backed custom MarkdownPresenter + M2 close-out).

ClaudePM stops being a viewer and becomes an editor.

6. **Inline text editor in Documentation tab** *(M)* — Click a doc in the
   list → loads into an editor pane with save/discard buttons. Saves go
   through scoped roots so the same safety model covers manual edits.
7. **Watch mode for Documentation** *(S)* — `FileSystemWatcher` on the
   project root. Debounce + re-run the structural pass on `.md` / `.txt`
   changes. Toggle in the controls row.
8. **Markdown rendering** *(M)* — custom Markdig-backed `MarkdownPresenter`
   control that walks the AST and emits native Avalonia controls (we tried
   `Markdown.Avalonia` 11.0.2 first; it silently blanked the bubble in every
   binding mode and ships only DLLs with no obvious style-include path).
   Renders headings, paragraphs, fenced code blocks, lists, blockquotes,
   inline code, bold/italic, links, and tables (with header-aware column
   widths). Used in the Notebook chat; can be reused anywhere prose needs
   rendering.

## Milestone 2.5 — Project Audit  ✓ SHIPPED

> Commit: `31424a6` (Project Audit + clipboard service + model picker +
> notes UX upgrade). The clipboard / model / notes work was bundled
> opportunistically since it touched the same UI surface.

The existing Documentation pass only finds doc-vs-doc contradictions.
This milestone adds the missing synthesis pass: ClaudePM reads a project's
docs and answers *"what's the state, what's complete, what's not, where do
the docs disagree?"* — the strategic snapshot you'd otherwise assemble by
hand from six separate files.

9. **Project Audit** *(M)* — New **"Audit Project"** button on the
   Documentation tab, next to "Run AI Analysis." Loads a signal-weighted,
   capped bundle of the project's docs (priority order: `CLAUDE.md` →
   `CHANGELOG.md` → `ROADMAP.md` → `SPEC.md` → `README.md` → `KICKOFF.md`
   → `docs/*.md` → everything else) and asks Claude for structured JSON:
   a design summary, a flat list of roadmap items
   (`title, status, category, source, evidence`), and an inconsistencies
   list (`severity, docs, issue`). Parses into a new `ProjectAuditReport`
   Core type. New full-pane overlay (same Grid pattern as Redesign /
   History) renders five sections:
   - **A) Project Design** — synthesized prose summary
   - **B) Roadmap — all items** — full table with status / category /
     source / evidence
   - **C) Completed** — filtered to status=complete
   - **D) Incomplete** — filtered to status=incomplete/unknown
   - **Inconsistencies** — severity-ranked, docs involved, with a
     "Generate Fix Prompt" button (matching the existing reconciliation
     flow, and wired into the M3 "Apply with AI" path when that lands)

   Bonus git cross-check (cheap, reuses `GitInfo`): for each item Claude
   marks "complete," run `git log --grep` against key terms from the
   title; zero matches in the last year of commit history adds an
   inconsistency *"Marked complete but no matching commit messages —
   verify."* Catches the "I forgot to commit / I lied to my docs" case.

## Milestone 3 — Smarter Notebook + telemetry

The agent gains memory; the user gains visibility.

10. **Persistent agent action log per project** *(M)* — New `agent_actions`
    table (`project_id, kind, path, content_hash, status, executed_at`).
    Replaces the in-memory `UndoHistory`; viewable per project; cross-session
    undo of the latest action.
11. **Execute Documentation fix prompts in the Notebook** *(S)* — "Apply with
    AI" button on the fix-prompt panel feeds it into the Notebook against
    the project root, through the existing preview/execute/undo gate.
    Reused by M2.5's audit-inconsistencies "Generate Fix Prompt".
12. **AI call log + cost tracking** *(M)* — New `ai_calls` table
    (`timestamp, model, module, project_id, input_tokens, output_tokens,
    cost_estimate`). "Activity" view in Settings shows recent calls + a
    running total per period.
13. **Streaming token meter** *(S)* — During `AgentChatAsync`, count
    text-delta + input-json-delta length; surface a live token count + a
    running cost estimate in the chat busy chip.

## Milestone 4 — Real project hub

ClaudePM becomes a hub for real Claude Code repos.

14. **Import existing project from `.claude/` + git** *(M)* — Point at a
    folder → ingest its `CLAUDE.md` as Description; pull `.claude/commands/*.md`
    into the Prompt library tagged with the project; pull `.claude/skills/`
    entries into the Skill Library; seed `Project.LastActivity` from
    `git log -1`. Bidirectional with #2 (Tier 1 of the brainstorm).
15. **Project templates in Session Builder** *(M)* — Step 0 of the wizard.
    Templates: Avalonia + .NET, FastAPI + Python, Next.js + TypeScript,
    Python CLI, plain monorepo. Each ships its own CLAUDE.md skeleton,
    README, `.gitignore`, and a stack-tuned kickoff prompt.
16. **Per-project model / output overrides** *(M)* — Optional `Model` and
    `DefaultOutputPath` on `Project`. `AnthropicChatService` resolves
    project override → global setting. Editor lives on the Projects tab.

## Milestone 5 — Landing dashboard + v1.0 polish

17. **Project health cards on Home** *(M)* — Per-project card with:
    stale-doc count from the last reconciliation, commits in the last 7 days
    from `git log`, pending agent action count, last activity timestamp.
    Click a card → navigate to that project's most relevant tab.
18. **v1.0 polish + bug-fix sweep** *(M)* — Pre-release tidy. UI consistency
    pass, error-message audit, end-to-end "first 5 minutes" walkthrough,
    hardening on any rough edges discovered during M1–M4.

## Milestone 6 — Skill Library Builder

Expand Module 5 from "edit existing skills" into a real authoring surface.
Today the user can scan, browse, validate, rename, and export skills they
already have; M6 lets them create new ones from scratch with structure,
guidance, and AI assistance.

19. **New Skill wizard** *(M)* — Empty-state "New Skill" button on the
    Skill Library tab opens a wizard:
    - **Step 0 — Template**: pick a starting template (Tool-using skill,
      Domain knowledge, Subagent, Workflow recipe, Blank). Each ships a
      skeleton body with the trigger-phrase rubric already filled in.
    - **Step 1 — Identity**: name (validated against the lowercase-hyphen
      rule + the "no 'claude' in name" guard) + one-line description
      (with the 1024-char budget meter and "use when…" trigger hint).
    - **Step 2 — Body**: monospace editor pre-populated from the template.
      Live-validates frontmatter + name + description as the user types.
    - **Step 3 — Save**: writes the skill in BOTH formats (flat `.skill`
      and `<name>/SKILL.md`) into the currently-selected scan folder.
20. **AI assist on description + body** *(M)* — Buttons inside the wizard
    that call Claude to:
    - Rewrite a draft description so it contains the explicit trigger
      phrases the validator looks for ("Use when…", "Trigger on…").
    - Generate a draft body from a one-paragraph human description of
      what the skill should do (template-aware — uses the picked
      template's structural conventions).
    Diff view for accept / reject, same UX as the Prompt Manager redesign
    flow. Both calls go through `IAiService.CompleteAsync` so prompt
    caching benefits them.
21. **In-app skill preview / dry-run** *(S)* — A "Preview as Claude would
    see it" panel renders the final frontmatter + body exactly as the
    skill loader would emit it. No execution sandbox in v1 (deferred to
    v1.1+ "Skill testing sandbox") — this is a visual sanity check only.
22. **Bulk import from a flat folder** *(S)* — "Import existing skills"
    button that scans an arbitrary folder for `.skill` files OR
    `*/SKILL.md` folders and copies them into the user's skills root
    in both formats. Lets users gather skills authored elsewhere into
    one canonical location.

## After v1.0

Deferred to v1.1+:
- Doc-vs-code semantic reconciliation (SPEC.md item)
- macOS / Linux secure key stores (Keychain, libsecret)
- Tray + system integration (one-click Notebook from notification area)
- `edit_file` tool with inline diff preview
- Transcript code-block extractor in Session Builder
- Command palette (Ctrl+K) + global keyboard shortcuts
- Skill testing sandbox
- Recent activity feed across all projects

---

## Appendix — Curated prompts library content sketch

Five categories, six prompts each. Every prompt uses `{{variable}}` placeholders
where useful so the existing Fill Template flow works. Prompts are written from
the perspective of *talking to Claude inside Claude Code* — terse system
guidance for the model, not user-facing prose.

### Category 1 — Doc & VCS hygiene
1. **Initialize doc system** — set up `CLAUDE.md` / `SPEC.md` / `README.md` /
   `CHANGELOG.md` following the ClaudePM convention; populate with whatever
   you can infer from the current codebase, mark unknown sections explicitly.
2. **Audit doc-vs-code drift** — for each `.md` doc in the project, list
   anything that no longer matches the code. Prioritize CRITICAL > WARNING >
   INFO. Don't fix yet, just report.
3. **Update `CHANGELOG.md` for `{{branch}}`** — diff against `main`, group
   commits into Added / Changed / Fixed / Removed, write a single
   release-style entry.
4. **Promote "Last Completed Task" to CHANGELOG.md** — read CLAUDE.md, write
   a versioned entry for the work it describes, clear the marker.
5. **Write an ADR for `{{decision}}`** — new file under `docs/adr/NNNN-…md`,
   structure: Context / Decision / Status / Consequences.
6. **Roadmap entry for `{{feature}}`** — append to ROADMAP.md with scope tag,
   rough work plan, and explicit non-goals.

### Category 2 — Testing & regression
1. **Build a test plan** — survey existing tests, identify gaps, produce a
   prioritized backlog (critical paths first). Don't implement.
2. **Write a regression test for `{{bug}}`** — must fail on the old code,
   pass on the fix. Place in the existing test directory.
3. **Set up test infrastructure from scratch** — pick a framework based on
   stack, add scripts, write 2–3 smoke tests as starting points.
4. **Identify untestable code & propose refactors** — find areas where tests
   are hard to write, suggest the smallest refactor that unlocks coverage.
5. **Generate property-based tests for `{{module}}`** — identify invariants,
   write FsCheck/Hypothesis/Hedgehog tests as appropriate.
6. **Add a golden-path smoke test** — single e2e test of the most common
   user journey. Fast, hermetic, no flake.

### Category 3 — Efficient task execution
1. **Pre-flight before a large change** — list every file you intend to
   touch and the rough nature of the change. Wait for approval before
   editing.
2. **Smallest viable version first** *(system rider)* — implement the
   minimum change that proves the approach works, then iterate. Don't add
   features the task didn't ask for.
3. **Pause-and-plan checkpoint** — every N edits, stop and summarize what's
   done, what's left, and confirm direction.
4. **Stop on uncertainty** *(system rider)* — refuse to guess on unknown
   APIs or unclear requirements. Ask, don't invent.
5. **Constrain blast radius** *(system rider)* — only touch files listed in
   the task description. If you need to touch more, ask first.
6. **Working commit policy** *(system rider)* — every commit must build,
   tests must pass. No "WIP" commits. No "fixed previous commit" follow-ups.

### Category 4 — New session starters
1. **Pick up where I left off** — read `CLAUDE.md` + the latest `CHANGELOG`
   entry; summarize state in 3 bullets and propose the most likely next
   priority.
2. **Continue from PR #`{{n}}`** — read the PR, the diff, and any review
   comments; respond to feedback before adding new work.
3. **Sub-agent kickoff for `{{task}}`** — initialize a focused subagent
   with only the files relevant to `{{task}}` in scope.
4. **Onboarding self-brief** — produce a 5-minute orientation for a new
   contributor, derived from current docs.
5. **Day-N status check** — what changed in the last 24h, what's next,
   what's blocked. Pull from git + CLAUDE.md.
6. **Resume after long break** — re-read everything; flag anything that
   looks stale based on git activity vs doc activity.

### Category 5 — Common essential dev tasks
1. **Code review my last commit** — strict pass: bugs, unclear naming,
   missing tests, security gotchas, style drift.
2. **Refactor `{{module}}` safely** — extract / rename / restructure; tests
   must continue passing at every checkpoint. Stop and report if a refactor
   step breaks anything.
3. **Performance investigation** — profile, identify hot path, propose
   changes. Don't implement yet — propose first.
4. **Write the README for the library you just authored** — quick-start,
   API surface, examples, contributing.
5. **Database migration writer** — write forward + rollback SQL for
   `{{change}}`, include sanity checks (row counts, type compatibility).
6. **API design review** — for `{{endpoint}}`, audit: status codes, error
   shape, versioning, idempotency, auth.
