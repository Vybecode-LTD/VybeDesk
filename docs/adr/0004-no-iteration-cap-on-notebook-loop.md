# ADR-0004: No hard cap on the Notebook agent's auto-loop

**Status:** Accepted
**Date:** 2026-05-24

## Context

`NotebookViewModel.RunAssistantTurnAsync` runs Claude turns in a loop:

1. Call `AgentChatAsync`.
2. If Claude returned `tool_use` blocks for read-only tools
   (`read_file`, `list_directory`), auto-execute them, post results back,
   and loop again.
3. If Claude returned write tool_use blocks (`create_file`, `create_folder`,
   `move`), surface them in the PendingActions UI and stop.
4. If Claude returned plain text (no tool calls), surface it and stop.

A long inspection ("audit this repo's structure") can fire 10–30 iterations
of step 2 before Claude is satisfied. An earlier version of this loop had
a `maxIterations = 5` cap that aborted the turn with a `[capped]` note.

That cap caused real pain: legitimate audit / inspection workflows hit the
limit, the user had to send "continue" to advance, and the conversation
felt artificially gated. Removing it surfaced the real bottleneck: the
user wants the Cancel button to be the brake, not a hardcoded loop count.

## Decision

The auto-loop has **no iteration cap**. `for (int iter = 0; ; iter++)` runs
until one of:

- Claude returns `end_turn` (no more tool calls) → surface text, exit.
- Claude returns a write tool_use → queue PendingActions, exit.
- The `CancellationToken` fires (user clicked Cancel) → throw, exit.
- An exception escapes (API error, parse failure, etc.) → caught one
  level up, surfaced in StatusMessage.

The Cancel button (`SendCancelCommand`, wired by
`[RelayCommand(IncludeCancelCommand = true)]`) is the only brake.

## Consequences

- Legitimate long inspections complete in one user turn.
- A pathological loop (Claude reading the same file 1000 times) will eat
  tokens until the user clicks Cancel. **Do not assume the loop is
  self-limiting.**
- A future "Apply with AI" flow on documentation fix prompts (M3 #11)
  could create an audit → apply → re-audit cycle that's hard to spot.
  **When that ships, add a separate guard** (cycle detection or a
  per-tool-call rate counter) — but do not bring back the global
  iteration cap, it caused the original problem.
- The user is the safety system. The Cancel button is always visible
  during a turn (`IsVisible="{Binding IsBusy}"` on the button).
