# ADR-0006: Prompt caching breakpoints on system + last tool

**Status:** Accepted
**Date:** 2026-05-24

## Context

Every Claude API call VybeDesk makes (Notebook, audit, reconciliation,
prompt redesign, session-builder review) sends the same large prefix
repeatedly: the system prompt and (for the Notebook) the tool
definitions. With base input pricing at $5/MTok for Opus 4.7, sending
the ~1000-token system prompt + ~200-token tools block on every Notebook
turn adds up fast — the user spent $10 in a single testing day before
caching was enabled.

Anthropic's prompt caching lets us mark stable prefix blocks with a
`cache_control: { type: "ephemeral" }` breakpoint. Subsequent requests
that present the same prefix read those tokens from cache at ~10% of
the base cost (`$0.50/MTok` instead of `$5/MTok` for Opus 4.7), with a
5-minute TTL that refreshes free on each hit.

Constraints we had to work around:

- **Minimum cacheable size.** Opus 4.7 requires the *total request* to
  reach 4,096 tokens before caching activates. A single short Notebook
  turn (system + tools + one user message) is well below this. Caching
  silently no-ops there. But as conversation history grows past the
  threshold, the cached prefix kicks in and stays cached for the rest
  of the session.
- **Hierarchical cache order.** Cache reads work in priority order:
  `tools → system → messages`. Marking only the *last* tool caches the
  entire tools block as a unit, using one breakpoint instead of N.
- **Breakpoint budget.** Max 4 explicit breakpoints per request — we
  use 2 (system + last tool), leaving room for future per-conversation
  breakpoints on frozen history if we want them.
- **Cache invalidation.** Any change to system text or any tool's
  schema invalidates the cache entry. Our system prompt is loaded
  once at app start and our tool schemas are compile-time constants,
  so the cache holds.

## Decision

`AnthropicChatService` attaches `cache_control: { type: "ephemeral" }`
on:

1. The system block, in both `AgentChatAsync` (streaming, Notebook) and
   `CompleteAsync` / `ChatAsync` (non-streaming, every other caller).
   System is sent as an array of one text block so the breakpoint has
   somewhere to attach — the server treats array-form system identically
   to string-form.
2. The last tool in the `tools` array (streaming path only — other
   callers don't pass tools).

No breakpoints on message blocks yet. A future enhancement could mark
the last block of the previous assistant turn so conversation history
gets cached too, but that requires tracking turn boundaries in the
serializer and we don't have a measured need for it yet.

## Consequences

- **Cost.** Notebook sessions that cross the cacheable threshold (which
  happens within 2–3 turns on average) pay the cache write cost once
  (1.25× base) then read at 0.1× for every subsequent turn within the
  5-minute TTL. Estimated savings on a 10-turn session: ~70% of system+
  tools tokens, which is most of the prefix cost.
- **No effect on short calls.** Single-shot audit / reconciliation calls
  with a small system prompt and a large user payload don't benefit —
  the user message bulk dominates and isn't cached. That's fine; those
  are infrequent.
- **Test coverage.** Three tests in `AnthropicChatServiceTests` assert
  the wire format: system is array-form with `cache_control` on the
  one block, the last tool gets `cache_control`, earlier tools don't.
- **Future work.** When M3 ships AI call telemetry (`ai_calls` SQLite
  table), surface `cache_creation_input_tokens` and
  `cache_read_input_tokens` from the response so the user can see the
  savings in the Activity view. Until then, the Anthropic billing
  dashboard is the only way to verify caching is hitting.
- **Don't move the breakpoints lightly.** Putting `cache_control` on a
  block that changes between requests (timestamps, per-turn user input)
  is the documented footgun — the hash differs, the cache misses,
  every request pays the write cost. The current placement is on
  content that's verifiably static across a session.
