# ADR-0001: Custom Markdig-backed renderer instead of Markdown.Avalonia

**Status:** Accepted
**Date:** 2026-05-24

## Context

The Notebook (and later the Project Audit's Design section) needs to render
Claude's markdown responses inside Avalonia. The obvious off-the-shelf choice
was [Markdown.Avalonia](https://github.com/whistyun/Markdown.Avalonia), the
de-facto Avalonia markdown library.

We tried `Markdown.Avalonia` 11.0.2 in every binding mode we could think of:

- Always-visible `MarkdownScrollViewer` bound to the message text.
- `IsStreaming`-toggled swap between a plaintext `SelectableTextBlock`
  during streaming and `MarkdownScrollViewer` after settle.
- `HasText`-gated render so the control only mounts after content exists.

In every variant the chat bubble silently went blank — no exception, no
warning, just an empty surface. The package ships DLLs with no obvious
style-include path, and the rendering pipeline isn't documented well enough
to debug from outside. We sank a couple hours into it before deciding the
debug cost outweighed the rewrite cost.

## Decision

We built a custom `MarkdownPresenter` (`App/Controls/MarkdownPresenter.cs`)
that uses **Markdig** for parsing only — Markdig is a pure parsing library
with zero UI coupling — and a hand-written AST walker that emits native
Avalonia controls. Block support: H1–H4, paragraphs, fenced code blocks,
ordered + unordered lists, blockquotes, thematic breaks, tables. Inlines:
literal text, inline code, bold/italic, styled links, autolinks, line
breaks.

## Consequences

- We own the rendering pipeline end to end. No silent blanking, no opaque
  template behavior, no version-bump roulette.
- Adding a new markdown construct (e.g. callouts, task lists, footnotes) is
  a small extension to the walker rather than a third-party PR.
- Tables get header-aware column widths via a `MinWidth` derived from the
  header's character count + a star-weight by body content length. (Stock
  Avalonia star columns don't grow to fit content; we had to engineer
  around it.)
- Maintenance is on us — if a Markdig version bump changes the AST,
  the walker may need adjustment.
- **Do not reintroduce `Markdown.Avalonia`** unless someone has solved
  the blanking issue in a later version and documented the fix. Note it
  in this ADR or supersede it.
