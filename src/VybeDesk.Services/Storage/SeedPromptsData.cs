namespace VybeDesk.Services.Storage;

/// <summary>
/// Curated prompts seeded into the Prompt Manager on first run (and added on
/// later runs if missing-by-title, so users picking up an existing DB get the
/// new content too without losing what they already have).
///
/// Five categories × six prompts. Variables in <c>{{double_braces}}</c> work
/// with the Prompt Manager's existing Fill Template flow.
/// </summary>
internal static class SeedPromptsData
{
    public sealed record SeedPrompt(
        string Title, string Body, string Category, IReadOnlyList<string> Tags);

    public static readonly IReadOnlyList<SeedPrompt> All = new SeedPrompt[]
    {
        // ─── Category 1 — Doc & VCS hygiene ────────────────────────────

        new("Initialize project doc system",
            """
            Set up the standard documentation files for this project:
            CLAUDE.md (running session context with a "Last Completed Task"
            section), SPEC.md (product/architecture spec), README.md
            (quick-start landing), and CHANGELOG.md (versioned history).

            Populate each with whatever you can infer from the codebase.
            Mark explicitly with [TBD: ...] anything you can't infer rather
            than guessing.

            Cross-link them from README. Conventions:
            - CLAUDE.md goes at the repo root
            - SPEC.md goes at the repo root
            - Docs aimed at contributors go in /docs/
            """,
            "Doc & VCS hygiene",
            new[] { "docs", "claude-md", "scaffold" }),

        new("Audit doc-vs-code drift",
            """
            Read every .md file in the project. For each one, list any
            claims that no longer match the current code: version numbers,
            file paths, API signatures, feature lists, configuration keys,
            module counts.

            Output as a severity-ranked table: CRITICAL (actively
            misleading), WARNING (likely wrong), INFO (cosmetic drift).
            Don't fix anything yet — just report.
            """,
            "Doc & VCS hygiene",
            new[] { "docs", "audit" }),

        new("Update CHANGELOG for current branch",
            """
            Diff this branch against {{base_branch}} (default: main). Group
            the changes into Added / Changed / Fixed / Removed sections
            following the Keep-A-Changelog convention. Write a single new
            entry at the top of CHANGELOG.md with version {{version}} and
            today's date.

            Don't include implementation noise — focus on what an external
            reader would care about. Reference commit hashes only when
            essential.
            """,
            "Doc & VCS hygiene",
            new[] { "docs", "changelog", "release" }),

        new("Promote CLAUDE.md \"Last Completed Task\" to CHANGELOG",
            """
            Read the "Last Completed Task" section of CLAUDE.md. Write a
            corresponding versioned entry to CHANGELOG.md following the
            existing format (Added / Changed / Fixed / Removed). Then reset
            the "Last Completed Task" section to a brief one-liner pointing
            at the new CHANGELOG entry.
            """,
            "Doc & VCS hygiene",
            new[] { "docs", "claude-md", "changelog" }),

        new("Write an ADR for {{decision}}",
            """
            Create a new ADR (Architecture Decision Record) for the
            decision: "{{decision}}". Place it under docs/adr/ as
            NNNN-<slugified-title>.md using the next available number.

            Structure:

            # NNNN. <decision title>

            ## Status
            Proposed | Accepted | Superseded by ADR-XXXX

            ## Context
            What problem are we solving? What's the constraint landscape?

            ## Decision
            What we're going with, in plain language.

            ## Consequences
            What gets easier; what gets harder; what we explicitly traded
            off.
            """,
            "Doc & VCS hygiene",
            new[] { "docs", "adr", "architecture" }),

        new("Add roadmap entry for {{feature}}",
            """
            Append a new entry to ROADMAP.md for: "{{feature}}".

            Include:
            - Scope tag (S = single session, M = a few sessions, L = bigger)
            - One-line goal
            - Rough work plan as bullets
            - Explicit non-goals (what we won't ship in this round)

            If a "Milestone" structure already exists, slot it into the most
            appropriate milestone; otherwise list it under "Planned".
            """,
            "Doc & VCS hygiene",
            new[] { "docs", "roadmap" }),

        // ─── Category 2 — Testing & regression ─────────────────────────

        new("Build a test plan for this project",
            """
            Survey what's currently tested in this project. For each module
            / package, identify:
            - What types of tests exist (unit / integration / e2e)
            - Roughly what coverage looks like
            - Critical paths that are untested

            Produce a prioritized backlog: Critical (add now) → Important
            (next sprint) → Nice-to-have. Don't implement anything — just
            the plan.
            """,
            "Testing & regression",
            new[] { "testing", "audit", "plan" }),

        new("Write a regression test for {{bug}}",
            """
            Write a regression test that would have caught the bug:
            "{{bug}}".

            Requirements:
            - Must FAIL on the pre-fix code
            - Must PASS on the fixed code
            - Place in the existing test directory following the project's
              conventions
            - Use the project's existing test framework — don't introduce a
              new one

            Show the test code, where to place it, and how to run just this
            test.
            """,
            "Testing & regression",
            new[] { "testing", "regression", "bug-fix" }),

        new("Set up test infrastructure from scratch",
            """
            This project has no tests yet. Set up the basic test
            infrastructure:

            1. Choose a test framework appropriate for the stack (justify
               the choice in one sentence).
            2. Add the necessary dev dependencies.
            3. Create a test directory following the project's conventions.
            4. Write 2–3 smoke tests as starting points (one per major
               component if possible).
            5. Add a script / task to run the tests.
            6. Document how to run tests in README.md.

            Stop after the smoke tests pass. Don't try to test everything.
            """,
            "Testing & regression",
            new[] { "testing", "scaffold", "setup" }),

        new("Identify hard-to-test code and propose refactors",
            """
            Identify code in this project that's hard to test (deep
            dependencies, hidden side effects, global state, time /
            randomness coupling, untestable I/O).

            For each, propose the smallest refactor that would make it
            testable — dependency injection, factory extraction, time
            abstraction, etc. Don't implement; just report:

            | Location | Why it's hard to test | Proposed refactor | Effort (S/M/L) |
            """,
            "Testing & regression",
            new[] { "testing", "refactor" }),

        new("Generate property-based tests for {{module}}",
            """
            Identify invariants that should hold for {{module}}. Generate
            property-based tests covering each invariant.

            Use the appropriate framework for the stack (FsCheck for .NET,
            Hypothesis for Python, fast-check for JS/TS). For each
            property, write:
            - The invariant in plain English (one line, as a doc comment)
            - The generator / strategy that produces inputs
            - The assertion

            Aim for 5–8 properties, not 50. Focus on invariants the
            existing example-based tests can't easily prove.
            """,
            "Testing & regression",
            new[] { "testing", "property-based" }),

        new("Add a golden-path smoke test",
            """
            Write a single end-to-end smoke test that exercises the most
            common user journey in this application / service.

            Requirements:
            - Hermetic — runs in isolation, no shared state, no network
              dependencies (or only mocked endpoints)
            - Fast — should complete in seconds
            - No flake — deterministic inputs, deterministic assertions
            - Golden path only — happy case, no edge cases

            Document at the top of the file what user journey it covers.
            """,
            "Testing & regression",
            new[] { "testing", "smoke", "e2e" }),

        // ─── Category 3 — Efficient task execution ─────────────────────

        new("Pre-flight before a large change",
            """
            Before you make any edits, list:
            1. Every file you intend to touch.
            2. The rough nature of the change in each
               (new / edit / refactor / delete).
            3. The order you'll make changes in.
            4. Any external dependencies or services this affects.
            5. The smallest verifiable checkpoint (a single test or command
               that should pass after you're done).

            Wait for my approval before you start editing.
            """,
            "Efficient task execution",
            new[] { "workflow", "planning" }),

        new("Smallest viable version first (system rider)",
            """
            Implementation rules for this task:

            1. Build the smallest version that proves the approach works.
               NO features beyond what the task literally requested.
            2. Don't add fallbacks, retry logic, or "future-proofing"
               speculation.
            3. Don't introduce abstractions until there are at least 3
               concrete cases that need them.
            4. If you find yourself writing "this might be useful later"
               code, stop and ask first.
            5. Every commit must build and pass tests. No WIP commits.
            """,
            "Efficient task execution",
            new[] { "workflow", "system-rider" }),

        new("Pause-and-plan checkpoint",
            """
            You've been working on this task. Pause and summarize:

            1. What's done (specific files / functions modified).
            2. What's NOT yet done (still to do).
            3. Anything that surprised you / changed your understanding.
            4. The next concrete step you intend to take.
            5. Any decisions you'd want my approval on before continuing.

            Don't make any more code changes until I respond.
            """,
            "Efficient task execution",
            new[] { "workflow", "planning" }),

        new("Stop on uncertainty (system rider)",
            """
            Decision rules for this task:

            - If you don't know what an API does, READ THE DOCS or ask.
              Don't guess.
            - If you can't figure out a requirement from the task
              description, ASK before guessing the user's intent.
            - If a test fails for a reason you don't understand,
              INVESTIGATE — never disable, comment out, or weaken the
              assertion.
            - If a refactor touches more files than expected, STOP and
              confirm scope.
            - Better to ask one extra question than ship one wrong change.
            """,
            "Efficient task execution",
            new[] { "workflow", "system-rider" }),

        new("Constrain blast radius (system rider)",
            """
            For this task:

            - Only edit files I've named in the task description.
            - If you discover you need to touch additional files, STOP and
              tell me which and why before editing them.
            - Don't refactor unrelated code, even if you spot opportunities.
            - Don't update dependencies, change config, or modify CI
              unless explicitly requested.
            - Don't reformat files you're not otherwise editing.
            """,
            "Efficient task execution",
            new[] { "workflow", "system-rider", "safety" }),

        new("Working commit policy (system rider)",
            """
            Commit hygiene rules:

            - Every commit must compile and pass tests. Use git stash if
              you need to test mid-change.
            - One logical change per commit. Don't combine refactor +
              feature, or fix + cleanup.
            - Commit message: "<imperative> <what>" headline ≤ 72 chars;
              optional body with WHY (not just WHAT).
            - No "WIP", "fix previous", "save state" commits. Squash before
              pushing.
            - Don't push directly to main / master. Open a PR even for
              solo work.
            """,
            "Efficient task execution",
            new[] { "workflow", "git", "system-rider" }),

        // ─── Category 4 — New session starters ─────────────────────────

        new("Pick up where I left off",
            """
            1. Read CLAUDE.md, particularly the "Last Completed Task"
               section.
            2. Read the most recent CHANGELOG.md entry.
            3. Look at `git log -5 --oneline` for recent commits.
            4. Summarize the current state in 3 bullets:
               - What was just shipped
               - What's in flight (incomplete)
               - What looks like the next priority
            5. Propose the most likely next task and ask if that's what I
               want to work on.
            """,
            "New session starters",
            new[] { "session-start", "context" }),

        new("Continue from PR #{{pr_number}}",
            """
            For PR #{{pr_number}}:

            1. Fetch the PR via `gh pr view {{pr_number}}`.
            2. Read the PR description and the full diff.
            3. Read all review comments and any "Requested changes"
               feedback.
            4. Summarize what's been done, what reviewers want changed,
               and what's still open.
            5. Respond to reviewer feedback BEFORE adding any new
               functionality.
            6. Don't push or comment until I confirm your plan.
            """,
            "New session starters",
            new[] { "session-start", "pr", "review" }),

        new("Sub-agent kickoff for {{task}}",
            """
            You're a focused sub-agent for a specific task: "{{task}}".

            Scope rules:
            - Only the files directly relevant to this task are in scope.
            - Don't read or modify anything outside the task scope.
            - Stay narrow: solve the task, then stop.
            - Don't propose follow-ups unless they're blocking.

            Start by listing the files you need to read to understand the
            task fully, then read them.
            """,
            "New session starters",
            new[] { "session-start", "sub-agent", "scope" }),

        new("Onboarding self-brief for new contributor",
            """
            Produce a 5-minute orientation document for a new contributor
            joining this project.

            Derive everything from current docs (README.md, CLAUDE.md,
            SPEC.md, CHANGELOG.md, docs/) — don't invent. Cover:

            1. What this project does (1–2 sentences).
            2. The stack and architecture in one paragraph.
            3. How to build, test, and run locally (commands).
            4. The directory structure (annotated tree).
            5. The most important convention to know before opening a PR.
            6. The 3 most likely first tasks (where to find them).

            If a doc is missing something material, list it as a gap rather
            than inventing.
            """,
            "New session starters",
            new[] { "session-start", "onboarding", "docs" }),

        new("Day-N status check",
            """
            Generate a daily status report for this project:

            1. What changed in the last 24 hours
               (`git log --since="24 hours ago"`).
            2. What's currently in flight (any branches with recent commits
               not on main).
            3. What's blocked or pending (from CLAUDE.md "Last Completed
               Task" or any TODO files).
            4. What I should consider next.

            Keep it under 200 words. Don't editorialize — just facts.
            """,
            "New session starters",
            new[] { "session-start", "status" }),

        new("Resume after a long break",
            """
            I haven't worked on this project in a while. Help me re-orient:

            1. Read CLAUDE.md, README.md, SPEC.md, and the last few
               CHANGELOG entries.
            2. Run `git log --since="3 months ago" --oneline` and summarize
               the major themes.
            3. Flag any docs that look stale relative to recent git activity
               (last commit > 3 months older than newest code commit).
            4. List the top 3–5 priorities based on the docs + recent
               activity.
            5. Suggest what I should pick up first to get re-engaged.
            """,
            "New session starters",
            new[] { "session-start", "context" }),

        // ─── Category 5 — Common essential dev tasks ───────────────────

        new("Code review my last commit",
            """
            Strict code review of the most recent commit.

            For each change, look for:
            - Logic bugs (off-by-one, null / undefined dereferences, race
              conditions)
            - Unclear naming (cryptic variables, misleading function names)
            - Missing test coverage (any new behavior without a
              corresponding test)
            - Security issues (injection, XSS, secrets in logs, unbounded
              inputs)
            - Style drift from project conventions

            Output: severity-ranked list. Don't be polite — be useful.
            Cite file:line for each finding.
            """,
            "Common dev tasks",
            new[] { "review", "quality" }),

        new("Refactor {{module}} safely",
            """
            Refactor {{module}} with these guarantees:

            1. The existing tests MUST keep passing at every checkpoint
               (run them between each step).
            2. Don't change behavior — only structure.
            3. Take it in small, atomic steps (extract, rename,
               restructure, one at a time).
            4. After each step, summarize what changed and confirm the
               tests still pass.
            5. If at any step you can't proceed without changing behavior,
               STOP and ask.

            Common safe refactors: extract method, rename for clarity,
            introduce parameter object, replace conditional with
            polymorphism, extract module / file.
            """,
            "Common dev tasks",
            new[] { "refactor", "quality" }),

        new("Performance investigation at {{location}}",
            """
            The code at {{location}} is slower than expected. Investigate:

            1. Profile or measure to identify the actual hot path — don't
               guess.
            2. Describe what's expensive (CPU? I/O? allocation? lock
               contention?).
            3. Propose changes ranked by impact / effort.
            4. DON'T IMPLEMENT — present findings first, get approval,
               then implement only the approved changes.

            Show numbers wherever possible (before / after measurements,
            big-O estimates).
            """,
            "Common dev tasks",
            new[] { "performance", "investigation" }),

        new("Write the README for a library at {{path}}",
            """
            Write a README.md for the library at {{path}}. Include:

            1. **One-line description** — what the library does, who it's
               for.
            2. **Installation** — the actual command.
            3. **Quick-start example** — the smallest useful snippet,
               working code.
            4. **API surface** — the main types / functions, not
               exhaustive; link to deeper docs.
            5. **Configuration** — env vars, options, defaults.
            6. **Contributing** — where to start, how to run tests.
            7. **License**.

            Keep it under 200 lines. Lead with example code, not prose.
            """,
            "Common dev tasks",
            new[] { "docs", "readme", "library" }),

        new("Database migration writer for {{change}}",
            """
            Write a database migration for the change: "{{change}}".

            Provide both UP and DOWN migrations. Include:

            1. **Up migration** — the DDL to apply the change.
            2. **Down migration** — the DDL to revert it (or document why
               it can't be reverted).
            3. **Sanity check queries** — row counts, type compatibility,
               constraint checks to run after applying.
            4. **Compatibility note** — whether this is breaking for
               existing application code, and how to safely deploy
               (rolling vs maintenance window).

            Use the project's existing migration tool conventions. Don't
            run anything — just produce the migration files.
            """,
            "Common dev tasks",
            new[] { "database", "migration", "sql" }),

        new("API design review for {{endpoint}}",
            """
            Audit the API endpoint(s) for {{endpoint}}. For each, evaluate:

            - **HTTP method correctness** (POST vs PUT vs PATCH
              appropriateness).
            - **Status codes** (correct for success, partial success,
              client error, server error).
            - **Error response shape** (consistent across endpoints;
              informative without leaking internals).
            - **Idempotency** (are unsafe methods truly idempotent where
              needed?).
            - **Versioning** (URL, header, none — and whether it'll
              survive future changes).
            - **Auth** (consistent with rest of API; least privilege).
            - **Pagination / filtering** (consistent conventions; bounds).

            Output a severity-ranked finding list with proposed fixes.
            """,
            "Common dev tasks",
            new[] { "api", "design", "review" }),
    };
}
