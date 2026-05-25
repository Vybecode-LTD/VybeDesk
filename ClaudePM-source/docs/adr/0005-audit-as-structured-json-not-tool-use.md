# ADR-0005: Project Audit as structured-JSON, not tool_use

**Status:** Accepted
**Date:** 2026-05-24

## Context

The M2.5 Project Audit synthesizes a project's docs into three structured
outputs: a design summary, a flat roadmap-item list (each tagged complete /
incomplete / unknown), and a severity-ranked inconsistencies list.

Two viable patterns for getting structured data out of Claude:

1. **`tool_use`** — declare a `submit_audit` tool with the schema, let
   Claude call it, parse the tool_use block's `input` as our payload.
   This is Anthropic's recommended pattern for structured output.
2. **Structured JSON in text** — ask Claude to return JSON in the message
   body, parse the response text.

Pattern (1) is more robust in theory: tool input schemas are validated by
Anthropic before delivery, and the response shape is guaranteed. But for
this specific use case:

- The audit is **one-shot, non-conversational**. There's no follow-up turn
  where the agent needs to see its own tool call. tool_use's whole point
  is that the model picks *when* to call the tool — here we always want
  one call.
- The audit endpoint is `IAiService.CompleteAsync` (non-streaming, no
  tool support). Moving it to `AgentChatAsync` (streaming + tool_use)
  would mean three changes — interface, service, ViewModel — for no
  user-facing benefit.
- Claude's text-mode JSON is reliable when the system prompt is explicit
  about the shape ("return ONLY a JSON object, no prose, no markdown
  fences"). In practice the responses arrive in three flavors: clean
  JSON, JSON wrapped in a ```json fence, or JSON with leading
  pleasantries — all parseable.

## Decision

`IDocReconciliationService.AuditAsync` calls `IAiService.CompleteAsync`
with a system prompt that pins down the JSON shape and rules. The response
goes through `ExtractJsonObject` (balanced-brace scan from the first `{`)
then `JsonSerializer.Deserialize<AuditPayload>` with case-insensitive +
trailing-comma tolerance + comment skipping.

On any parse failure: return `ProjectAuditReport.Empty`. The UI handles
the empty state cleanly so the user sees "no audit data" rather than a
crash.

Golden-input tests exercise the parser against the response shapes Claude
actually produces (clean / fenced / leading-prose / trailing-prose /
malformed / no-JSON / blank-titled items / unsorted severities).

## Consequences

- Audit lives in the non-streaming code path — simple,
  cancellation-friendly, no SSE bookkeeping.
- A misbehaving model response degrades to "empty audit" rather than an
  exception, so the user sees a UI state, not a crash.
- If we ever need the audit to be incremental (stream items as they're
  decided), this would need to move to `tool_use` or a streaming
  variant. Until then, the synchronous one-shot path is the right
  tradeoff.
- **Do not refactor the audit onto `AgentChatAsync` / tool_use** without
  a concrete user need — the simplicity of the current path is worth
  preserving.
