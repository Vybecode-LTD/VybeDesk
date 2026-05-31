# DEBUG PROTOCOL — Anti-Loop Circuit Breaker

> Reusable building block. `@import` this into any project's CLAUDE.md.
> Its job: stop the "Claude tries a blind fix → user says it failed → Claude tries
> another blind fix → same failure → forever" cycle by forcing diagnosis,
> evidence, and verification instead of guesswork.

> **Related directive — `ONLY_IF_DESKTOP_DOWNLOAD_APP → follow release directive`:** @SOFTWARE_RELEASE.md
> Desktop apps shipped as a downloadable installer/binary follow that release pipeline; web apps, services, and libraries skip it.

---

## WHEN THIS PROTOCOL ACTIVATES

Enter **DIAGNOSTIC MODE** (defined below) when **either** trigger fires:

1. **Automatic — the 2-strike rule.** The same bug, error, or symptom has been
   reported as still-not-working **twice** after attempted fixes. The second
   "it's still broken" (or equivalent: "same error," "no change," "still
   happening") is the trip wire. Do not attempt a third blind fix.

2. **Manual — keyword override.** The user types **`BREAKLOOP`** (or
   `/debug-protocol`, or "enter debug protocol"). Enter DIAGNOSTIC MODE
   immediately, regardless of attempt count.

When triggered, announce it in one line — e.g. *"Entering diagnostic mode after
two failed attempts. Freezing code edits until I have evidence."* — then follow
the steps below in order. **Do not skip steps to get back to coding faster.**

---

## DIAGNOSTIC MODE (code edits are FROZEN)

While in diagnostic mode you may read files, run commands, add temporary
instrumentation, and write tests. You may **NOT** apply a "fix" edit to
production code until Step 8.

**Step 1 — STOP and name the situation.**
State plainly that previous attempts failed and that you are switching from
fixing to diagnosing. No new fix in this message.

**Step 2 — Restate the problem and your architectural assumptions.**
In plain language, describe (a) what is actually supposed to happen, (b) what is
happening instead, and (c) the assumptions you've been making about how this
part of the system works. The wrong fix usually rides on a wrong assumption —
surface it here. This is rubber-duck debugging: explaining it exposes the flaw.

**Step 3 — Read the full error. No half-reads.**
Quote the complete error message and stack trace verbatim, and identify the
exact file and line. If there is no error (silent wrong behavior), state the
precise observable discrepancy (expected value vs actual value).

**Step 4 — Reproduce it reliably.**
Produce a concrete, repeatable reproduction: a command to run, a minimal test,
or exact steps. If you **cannot** reproduce it, say so explicitly and state what
you'd need (a log, an input, access to a state) to reproduce it. *An
un-reproduced bug cannot be verified as fixed — so reproduction comes before any
fix, always.*

**Step 5 — Form 3 competing hypotheses.**
List **three** distinct candidate root causes. For each: a confidence estimate
(%), and a **discriminating test** — something whose result would confirm or
*rule out* that specific hypothesis. At least one hypothesis must contradict
your earlier fix attempts (you were anchored; deliberately consider that you
were wrong about the cause).

**Step 6 — Explain why each previous fix failed.**
For every fix already attempted, state the mechanism-level reason it didn't
work. "It didn't work" is not an answer; "it didn't work because the value was
already mutated upstream before this function ran" is. If you can't explain why
a past fix failed, you don't yet understand the bug — keep investigating.

**Step 7 — Instrument, then gather evidence.**
Add temporary logging / assertions / breakpoints that discriminate between the
three hypotheses. Run them. Report the **actual observed values**. Let the
evidence pick the hypothesis — do not pick first and rationalize. (Adding lots
of diagnostics is cheap and you'll remove them later, so be generous with
instrumentation.)

**Step 8 — Propose ONE evidence-backed fix + a prediction.**
Only now, and only if the evidence points to a single root cause, propose one
targeted fix. State exactly what observable output will change if the fix is
correct (this prediction is how Step 9 verifies it).

**Step 9 — Verify with PROOF, not assertion.**
Apply the fix, then re-run the reproduction from Step 4 and paste the **verbatim
command and its output** (or a screenshot / a `curl` of the live endpoint / the
now-passing test). Never report "fixed," "done," or "tests pass" based on a
plausible-looking diff alone. **Plausibility is not correctness.** If you cannot
show proof, the fix has not succeeded — say so.

Remove temporary instrumentation only after the fix is proven.

---

## ESCALATION LADDER (if the diagnostic pass didn't resolve it)

Climb these in order. Each rung exists because it attacks a *different* reason
the loop persists — don't skip to the bottom, and don't re-run the same failing
approach louder.

1. **Isolate.** Strip the problem to a minimal reproduction. Ignore the rest of
   the codebase; paste only the failing piece and the exact error, and ask
   what's wrong with *that* in isolation. Removing surrounding code removes false
   leads.

2. **Bisect (if it's a regression).** If it worked before and broke, `git
   bisect` to the offending commit instead of guessing. Mark known-good and
   known-bad, test midpoints, halve until found.

3. **Compare against a known-good reference.** Find an open-source project (or
   official library source/docs) that implements the same thing correctly. Read
   it, summarize the pattern it uses, and diff that pattern against the current
   approach. **Treat every deviation as a suspected bug, not a style choice.**
   (This is the move that breaks "I keep reinventing a pattern that's subtly
   wrong.")

4. **Write a failing test first.** Capture the bug as a test that fails for the
   right reason, then make it pass. This converts a fuzzy target into an
   unambiguous one and leaves a regression guard behind.

5. **Revert + fresh context.** If the session is long and tangled, the context
   has likely rotted — earlier wrong turns are now polluting reasoning. Revert
   the session's changes, write a concise handoff note (problem, what's been
   ruled out, current best hypothesis, repro steps) to a file, and **start a new
   session that reads only that note.** A fresh context routinely solves what a
   degraded one cannot.

6. **Get a second opinion from a different model.** A different model has
   different blind spots. Hand it the isolated repro + evidence and compare its
   diagnosis. Disagreement is informative.

7. **Search the exact error string / read the official docs and source.** Paste
   the literal error into a search; read the library's actual source for the
   function involved. Stop reasoning from a possibly-outdated memory of the API.

8. **Escalate to the human.** State clearly what's known, what's been ruled out,
   the leading hypothesis, and the single most useful thing the human could
   provide. A precise question beats a tenth blind attempt.

9. **Rewrite from the reference pattern.** If the code is too tangled to salvage,
   rebuild the component from a known-good reference (rung 3) rather than patching
   further.

---

## STANDING RULES (apply at all times, not just when stuck)

- **Prefer running the code to guessing about the code.** Verify by execution.
- **Fix root causes, not symptoms.** Suppressing or catching the error is not
  fixing it.
- **Read whole errors and whole stack traces.** Half-read traces produce wrong
  fixes.
- **Never fabricate results.** If you don't know, read the file, run the command,
  or say "I don't know — let me check."
- **Don't mirror the user's guess.** If the user proposes a cause and the
  evidence disagrees, say so and show the evidence. Agreeing to be agreeable
  wastes both our time.
- **Stop when confused.** If the task has two plausible interpretations, ask
  rather than silently picking one and proceeding.

---

## SELF-MAINTAINING LESSONS

When the user has to manually break a loop you didn't catch, append one concrete
line to the project's CLAUDE.md "Lessons" section before ending the session —
e.g. *"Timeline drag math must match reference impl X; recomputing offsets
client-side caused the week-long jitter bug."* Each loop you survive should leave
behind a guard so it can't recur.
