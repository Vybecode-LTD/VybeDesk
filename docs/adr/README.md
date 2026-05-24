# Architecture Decision Records

Standalone records of significant technical decisions, written *after* the
decision shipped so future contributors can understand the **why**, not
just the **what**. Each ADR is numbered, dated, and immutable once
accepted — supersede with a new ADR rather than rewriting history.

Format: Context → Decision → Consequences. Brief by design.

## Index

| # | Title | Status |
|---|-------|--------|
| [0001](0001-markdig-over-markdown-avalonia.md) | Custom Markdig-backed renderer instead of Markdown.Avalonia | Accepted |
| [0002](0002-direct-https-over-anthropic-sdk.md) | Direct HTTPS to the Anthropic Messages API instead of the SDK | Accepted |
| [0003](0003-dpapi-for-api-key-storage.md) | DPAPI for API key storage (Windows-first) | Accepted |
| [0004](0004-no-iteration-cap-on-notebook-loop.md) | No hard cap on the Notebook agent's auto-loop | Accepted |
| [0005](0005-audit-as-structured-json-not-tool-use.md) | Project Audit as structured-JSON, not tool_use | Accepted |

## When to write a new ADR

- A non-obvious choice between two viable approaches (and the reasoning
  doesn't fit in a one-line comment or commit message).
- A constraint or invariant that future-you might forget and accidentally
  violate.
- A decision that took real investigation — capture the dead ends so the
  next person doesn't re-walk them.

If a decision is "obvious from the code," don't write an ADR for it. ADRs
are for the stuff that *looks* arbitrary but isn't.
