# ADR-0002: Direct HTTPS to the Anthropic Messages API instead of the SDK

**Status:** Accepted
**Date:** 2026-05-24

## Context

VybeDesk talks to Anthropic's Messages API for everything: doc reconciliation,
the Notebook agent, prompt redesign, audit synthesis. The official
`Anthropic.SDK` (`Anthropic.SDK` NuGet) is the path-of-least-resistance
choice.

But our requirements are slightly off the SDK's main path:

- **Streaming + tool_use protocol** for the Notebook with custom delta
  handling (text deltas update an observable `bubble.Text` live;
  `input_json_delta` fragments are reassembled per tool_use block).
- **Schema-less tool definitions** — we want to pass JSON schemas through
  to Anthropic verbatim, including ones with nested objects, oneOf, etc.
- **Test injection** — we want a fake `HttpMessageHandler` that returns
  canned SSE bytes, so the streaming + tool_use parser can be exercised
  in unit tests without a network or an SDK.
- **Retry / backoff control** — we own the retry policy for 429 / 503 / 529
  (see ADR-0004 era code for backoff specifics).

Wrapping the SDK to do all of this would mean fighting it as often as using
it, and we'd still be on the hook to understand the SSE event sequence.

## Decision

`AnthropicChatService` POSTs directly to
`https://api.anthropic.com/v1/messages` with `x-api-key` and
`anthropic-version: 2023-06-01` headers. The non-streaming path uses typed
`MessagesRequest` / `MessagesResponse` DTOs; the streaming path builds a
`JsonObject` payload so tool schemas pass through as-is, then parses the
SSE stream by hand (`content_block_start`, `content_block_delta`,
`content_block_stop`, `message_delta`, `message_stop`, `error`).

Test-only constructor takes a pre-configured `HttpClient` so tests can plug
in a `FakeHandler` or a `ScriptedHandler`.

## Consequences

- We own the wire-protocol code, which is small (~150 lines) and
  thoroughly unit-tested.
- Adding new Anthropic features (citations, batch, files, memory) means
  reading the API docs and extending our own DTOs, not waiting for SDK
  releases.
- Retry / backoff lives in one helper (`SendWithRetryAsync`) — no SDK
  policy to override.
- If Anthropic deprecates an event type or changes a header, we have to
  update the code ourselves. The `anthropic-version` header gives us
  some runway.
- **Do not introduce the SDK alongside this** — pick one and live with it.
  Two paths to the same API surface is a debugging nightmare.
