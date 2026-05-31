# SEO / GEO OPTIMIZATION — Web-Facing Directive

> Reusable building block. `@include` this into a project's CLAUDE.md (local
> `@SEO_OPTIMIZATION.md` when copied into the project). Deep reference (200+
> techniques) lives in `@seo-research-catalog.md` — this file is the binding
> *directive*; the catalog is the *encyclopedia*.

**APPLIES TO — `ONLY_IF_WEB_FACING`:** any project that serves HTML to humans or
AI crawlers — marketing sites, landing pages, blogs, docs sites, web apps with
public pages, app download pages. **DOES NOT APPLY TO:** purely internal tools,
CLIs, libraries, or backend-only services with no public HTML. If the project
has no web-facing surface, **skip this directive.**

This document is binding when included. Mandate: every public page is optimized
for **both** classic search (Googlebot) **and** AI answer engines (GEO —
ChatGPT, Gemini, Perplexity, Claude, AI Overviews). In 2026 these are one job,
not two.

---

## NON-NEGOTIABLE RULES (every public page)

1. **Title tag** — unique, 50-60 chars, primary keyword near the front.
2. **Meta description** — unique, 150-160 chars, includes the primary keyword + a call to action.
3. **One `<h1>`** matching the primary keyword and the page's search intent; logical `h2`/`h3` nesting (never skip levels). Prefer **question-format** headers ("What is X?") — AI Overviews cite these.
4. **`<link rel="canonical">`** — self-referencing on every page; duplicates point to the canonical.
5. **JSON-LD schema** appropriate to the page type (Organization, Article, FAQPage, Product, SoftwareApplication, BreadcrumbList, …). Validate before deploy. Schema must reflect visible content only.
6. **AI crawler access** — `robots.txt` MUST allow `GPTBot`, `ChatGPT-User`, `PerplexityBot`, `Google-Extended`, `ClaudeBot`, `anthropic-ai`. (Cloudflare blocks AI bots by default since 2024 — check it.)
7. **Images** — descriptive alt text, WebP/AVIF, explicit `width`/`height` (prevents CLS), `loading="lazy"` below the fold.
8. **Internal linking** — 3-5 descriptive-anchor links per 1,000 words; no orphan pages (reachable within 3 clicks of home).
9. **Core Web Vitals** — LCP < 2.5s, **INP < 200ms** (the usual failure — audit third-party scripts), CLS < 0.1.
10. **HTTPS everywhere**, mobile-first (content parity with desktop), XML sitemap submitted.

## GEO — get cited in AI answers (do this, not just classic SEO)

- **Modular, self-contained paragraphs** — AI extracts chunks, not whole pages.
- **Direct-answer format** — question header, then a 2-4 sentence answer immediately below.
- **Statistics (+33% citation rate) and expert quotations (+41%)** — the two biggest citation boosters; include real numbers and named-expert quotes.
- **Freshness** — content updated within 30 days gets ~3.2× more AI citations; stamp "Last updated: <date>".
- **Brand entity clarity** — consistent name/description across platforms; Organization schema; a detailed About page with credentials. (Top predictor of AI citation across 129k domains.)

## Commands (if the SEO optimizer skill suite is installed)

| Command | Action |
|---|---|
| `SEO audit` | Full site audit (technical, on-page, schema, GEO, content, links, local, analytics) |
| `optimize this page` | Single-page deep dive |
| `generate schema for <type>` | Ready-to-paste JSON-LD |
| `GEO audit` | AI search-visibility assessment |

## Pre-publish gate (every new/changed public page)

Title ✓ · meta ✓ · single H1 + intent ✓ · canonical ✓ · valid JSON-LD ✓ ·
alt text + image dims ✓ · internal links ✓ · AI crawlers allowed ✓ · CWV not
regressed ✓ · (if substantive) a direct-answer paragraph + a statistic/quote ✓.

> Full technique library, schema table, CWV thresholds, link-building tactics,
> local SEO, programmatic SEO, and analytics metrics: **`@seo-research-catalog.md`**.
