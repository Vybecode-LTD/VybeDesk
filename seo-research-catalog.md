# SEO Optimizer Research Catalog — 2026

## Overview

This research covers 8 SEO disciplines, 200+ techniques, and the emerging
GEO (Generative Engine Optimization) field. Sources include Backlinko,
Search Engine Land, WordStream, Semrush, The HOTH, and peer-reviewed
research from Georgia Tech/Princeton/Allen Institute.

---

## 1. TECHNICAL SEO

The foundation — if search engines can't crawl, render, and index your
site, nothing else matters.

### 1.1 Crawlability
- **robots.txt**: Allow Googlebot, Bingbot, AND AI crawlers (ChatGPT-User, GPTBot, PerplexityBot, Google-Extended, ClaudeBot, anthropic-ai)
- **XML sitemap**: Submit via Google Search Console, max 50k URLs per sitemap, use sitemap index for larger sites
- **Crawl budget**: Remove low-value pages from crawl (faceted nav, session IDs, infinite scroll traps)
- **HTTP status codes**: 200 (good), 301 (permanent redirect), 404 (not found — have custom page), 410 (gone), 503 (maintenance — with Retry-After header)
- **Canonical tags**: Self-referencing canonicals on every page, point duplicates to canonical
- **Pagination**: Use `rel="next"` and `rel="prev"`, ensure paginated URLs are crawlable HTML
- **URL structure**: Short, descriptive, lowercase, hyphens not underscores, no parameters when possible
- **Redirect chains**: Max 1 hop (no chains of 301→301→301)
- **Orphan pages**: Every page must be reachable via internal links
- **Log file analysis**: Check actual Googlebot crawl behavior vs what sitemaps claim

### 1.2 Indexation
- **Meta robots**: `index,follow` (default), `noindex,follow` (don't index but follow links), `noindex,nofollow` (block entirely)
- **Google Search Console**: Monitor Index Coverage report, submit URLs for indexing
- **Duplicate content**: Canonicalize or noindex, never leave competing duplicates
- **Thin content**: Pages with <300 words that don't serve unique purpose → consolidate or remove
- **Parameter handling**: Use Google Search Console URL Parameters tool
- **JavaScript rendering**: Ensure critical content is in initial HTML or pre-rendered (SSR/SSG), not behind JS that Googlebot might miss

### 1.3 Core Web Vitals (2026 thresholds)
- **LCP (Largest Contentful Paint)**: < 2.5s — optimize hero images, preload critical resources, server response time
- **INP (Interaction to Next Paint)**: < 200ms — replaced FID in March 2024, measures ALL interactions not just first. Fix: optimize JS execution, break long tasks, defer non-critical scripts
- **CLS (Cumulative Layout Shift)**: < 0.1 — set explicit width/height on images/videos, reserve space for ads/embeds, use `font-display: swap`
- **INP is the most commonly failed metric in 2026** — heavy third-party scripts (analytics, chat widgets, ad platforms) are the primary cause

### 1.4 Site Speed
- **Image optimization**: WebP/AVIF format, lazy loading (`loading="lazy"`), responsive images (`srcset`), compress to <100KB for above-fold
- **CSS/JS**: Minify, tree-shake unused code, defer non-critical JS, inline critical CSS
- **Server**: HTTP/2 or HTTP/3, CDN (Cloudflare/Fastly), Brotli compression, TTFB < 200ms
- **Caching**: Set `Cache-Control` headers, immutable for hashed assets, short TTL for HTML
- **Third-party scripts**: Audit with Lighthouse, defer/async all non-critical, self-host where possible
- **Fonts**: `font-display: swap`, preload critical fonts, limit font weights/styles

### 1.5 Mobile-First
- **Google indexes mobile version first** — mobile experience IS the experience
- **Responsive design**: No separate mobile URLs (m.example.com is legacy)
- **Touch targets**: Min 48x48px, 8px spacing between tappable elements
- **Viewport**: `<meta name="viewport" content="width=device-width, initial-scale=1">`
- **Content parity**: Mobile must have all content desktop has (no hiding behind tabs/accordions that Google can't see)

### 1.6 Security
- **HTTPS everywhere**: SSL certificate, redirect all HTTP to HTTPS
- **HSTS header**: `Strict-Transport-Security: max-age=31536000; includeSubDomains`
- **CSP header**: Content Security Policy to prevent XSS
- **Mixed content**: No HTTP resources on HTTPS pages

### 1.7 International SEO
- **hreflang tags**: Specify language/region for each page version
- **URL structure**: subdirectories (/en/, /fr/) preferred over subdomains (en.example.com)
- **Content localization**: Translate AND localize (not just translate)

---

## 2. ON-PAGE SEO

Optimizing individual pages for both search engines and users.

### 2.1 Title Tags
- **Length**: 50-60 characters (Google truncates at ~600px width)
- **Primary keyword**: Place near the beginning
- **Unique per page**: No duplicate titles across site
- **Format**: `Primary Keyword - Secondary Keyword | Brand Name`
- **Power words**: "Ultimate", "Complete", "Guide", "2026" boost CTR
- **Numbers**: "7 Ways to..." outperforms generic titles in CTR

### 2.2 Meta Descriptions
- **Length**: 150-160 characters
- **Include primary keyword**: Google bolds matching terms
- **Call to action**: "Learn how...", "Discover...", "Get started..."
- **Unique per page**: Google may rewrite if not relevant to query
- **Not a ranking factor directly** — but impacts CTR which impacts rankings

### 2.3 Header Structure (H1-H6)
- **One H1 per page**: Contains primary keyword, matches search intent
- **H2s for major sections**: 3-8 per long-form page
- **H3s for subsections**: Nest logically under H2s
- **Question-format headers**: "What is X?" / "How does Y work?" — AI Overviews heavily cite these
- **Keyword in at least one H2**: Reinforces topic relevance
- **Don't skip levels**: H1→H2→H3, never H1→H3

### 2.4 Content Optimization
- **Search intent alignment**: Informational, navigational, transactional, commercial
- **Keyword density**: 0.5-2% (1-2 per 100 words) — natural usage, never stuffing
- **Primary keyword placement**: First 100 words, at least one H2, URL slug, alt text
- **Semantic keywords (LSI)**: Related terms that signal topical depth
- **Entity coverage**: People, places, concepts related to the topic
- **Content length**: Match top-ranking competitors — comprehensive but not padded
- **Freshness**: Update every 6-12 months, add "Last updated: [date]"
- **E-E-A-T signals**: Author bios, credentials, citations, real experience
- **Readability**: 6th-8th grade reading level, short paragraphs, clear language
- **Multimedia**: Images, videos, infographics, tables, charts improve engagement

### 2.5 Internal Linking
- **3-5 internal links per 1,000 words**
- **Descriptive anchor text**: Use keywords, not "click here"
- **Link to high-value pages**: Distribute authority to important content
- **Top-of-page links**: Reduce bounce rate, improve dwell time
- **Hub-and-spoke model**: Pillar page links to cluster pages and vice versa
- **Fix orphan pages**: Every page reachable within 3 clicks from homepage
- **Audit regularly**: Remove links to 404'd pages, update redirected links

### 2.6 URL Optimization
- **Short and descriptive**: `/seo-guide/` not `/p=123` or `/blog/2026/05/28/the-complete-guide-to-seo/`
- **Lowercase**: URLs are case-sensitive on some servers
- **Hyphens**: Not underscores or spaces
- **Include primary keyword**: Mild ranking signal
- **No parameters when possible**: Clean URLs > parameterized URLs

### 2.7 Image Optimization
- **Alt text**: Descriptive, keyword-natural, accessibility-first
- **File names**: `seo-checklist-2026.webp` not `IMG_4532.jpg`
- **Format**: WebP for photos, SVG for icons/logos, AVIF for next-gen
- **Compression**: Tools — Squoosh, TinyPNG, ImageOptim
- **Lazy loading**: `loading="lazy"` on below-fold images
- **Dimensions**: Always set `width` and `height` attributes (prevents CLS)
- **Responsive**: Use `srcset` for multiple sizes

### 2.8 External Linking
- **Link to authoritative sources**: Studies, data, official docs
- **Adds credibility**: Google sees well-researched content as more trustworthy
- **Use `rel="nofollow"` for**: Paid links, user-generated content, untrusted sources
- **2-5 external links per long-form article**: Quality over quantity

---

## 3. OFF-PAGE SEO & LINK BUILDING

Building authority through external signals.

### 3.1 Backlink Quality Signals
- **Domain authority**: Links from high-DA sites worth more
- **Relevance**: Topically related sites > random high-DA sites
- **Dofollow vs nofollow**: Dofollow passes PageRank; nofollow still has value for traffic/brand
- **Anchor text diversity**: Mix of branded, naked URL, keyword, generic
- **Link velocity**: Gradual growth, not sudden spikes
- **Unique referring domains**: 10 links from 10 domains > 10 links from 1 domain

### 3.2 Link Building Strategies (2026)
- **Digital PR**: Create newsworthy content, pitch to journalists
- **HARO/Connectively/Help a B2B Writer**: Respond to journalist queries
- **Guest posting**: Quality over quantity, relevant publications only
- **Broken link building**: Find broken links on relevant sites, offer your content as replacement
- **Skyscraper technique**: Find top content, create something better, pitch to linkers
- **Resource page link building**: Get listed on curated resource pages
- **Unlinked brand mentions**: Find mentions without links, request link addition
- **Original research/data**: Studies, surveys, benchmarks earn natural links
- **Expert roundups**: Compile expert quotes, participants share
- **Infographics**: Visual content earns links naturally
- **Podcast appearances**: Show notes link to your site

### 3.3 Brand Signals
- **Branded search volume**: People searching your brand name = authority signal
- **Social media presence**: Not a direct ranking factor but drives branded searches
- **Wikipedia/Wikidata**: Brand entity recognition
- **Knowledge Graph inclusion**: Organization schema + consistent NAP + Wikipedia
- **Forum/community presence**: Reddit, Quora, Stack Overflow mentions

---

## 4. LOCAL SEO

For businesses serving geographic areas.

### 4.1 Google Business Profile (GBP)
- **Claim and verify**: Complete every field
- **NAP consistency**: Name, Address, Phone identical everywhere
- **Categories**: Primary + secondary categories, be specific
- **Photos**: 10+ high-quality photos, updated regularly
- **Posts**: Weekly Google Posts with offers, updates, events
- **Q&A**: Seed with common questions and answers
- **Products/Services**: List all with descriptions and prices
- **Hours**: Keep accurate, including holiday hours

### 4.2 Reviews
- **Volume**: More reviews = higher local ranking
- **Recency**: Recent reviews matter more
- **Respond to all reviews**: Positive and negative
- **Rating**: 4.0+ average is the threshold
- **Keywords in reviews**: Natural mentions of services/location help
- **Review velocity**: Steady stream > burst of reviews

### 4.3 Local Citations
- **NAP citations**: Consistent across all directories
- **Top directories**: Yelp, BBB, Yellow Pages, industry-specific
- **Data aggregators**: Foursquare, Data.com
- **Local business schema**: `LocalBusiness` JSON-LD with geo coordinates

### 4.4 Local Content
- **Location pages**: Unique content per location served
- **Local keywords**: "best [service] in [city]"
- **Local link building**: Chamber of commerce, local news, community sponsors
- **Local events/news**: Content about local happenings

---

## 5. CONTENT STRATEGY

The content engine that drives organic growth.

### 5.1 Keyword Research
- **Search intent classification**: Informational, navigational, transactional, commercial
- **Long-tail keywords**: Lower competition, higher conversion, better for AI answers
- **Keyword clustering**: Group related keywords, target with one page
- **Tools**: Ahrefs, Semrush, Google Keyword Planner, Keywords Everywhere, AlsoAsked
- **Competitor gap analysis**: Keywords competitors rank for that you don't
- **People Also Ask**: Mine for question-based content ideas
- **Search volume vs. difficulty**: Balance opportunity vs. competition
- **Zero-volume keywords**: Can still drive traffic — conversational queries

### 5.2 Content Types
- **Pillar pages**: Comprehensive 3,000-5,000 word guides on core topics
- **Cluster content**: Supporting articles linking back to pillar
- **Blog posts**: Regular publishing, 1,500-2,500 words for competitive keywords
- **Product/service pages**: Transactional intent, conversion-focused
- **Comparison pages**: "X vs Y" — high commercial intent
- **How-to guides**: Step-by-step, earns featured snippets
- **Listicles**: "Top 10 X" — scannable, shareable
- **Case studies**: Demonstrates E-E-A-T, earns trust
- **Glossary/definitions**: Captures informational queries, AI citations
- **FAQ pages**: Structured Q&A, schema-eligible

### 5.3 Content Clusters (Topic Authority)
- **Hub-and-spoke model**: Pillar page (hub) + supporting articles (spokes)
- **Internal linking between cluster**: Every spoke links to hub and 2-3 other spokes
- **Topical depth**: Cover every subtopic exhaustively
- **Build authority over breadth**: Deep expertise > wide shallow coverage
- **Update cadence**: Refresh hub quarterly, spokes annually

### 5.4 Content Optimization for AI
- **Modular content**: Self-contained paragraphs that AI can extract independently
- **Direct answer paragraphs**: Question-then-answer format in first 2-3 sentences
- **Statistics and data**: AI models prefer content with specific numbers (+33% citation rate per GEO research)
- **Quotations from experts**: +41% citation rate in AI answers
- **Authoritative tone**: First-person expert voice, not generic corporate
- **Cite sources**: Attribution increases trust for both humans and AI
- **Structured data**: FAQ, HowTo, Article schema helps AI classify content

---

## 6. STRUCTURED DATA & SCHEMA MARKUP

Machine-readable content classification.

### 6.1 Priority Schema Types (2026)

| Schema Type | Impact | Use case |
|---|---|---|
| **Organization** | Entity foundation | Homepage — brand identity in Knowledge Graph |
| **Article/BlogPosting** | Editorial credibility | Blog posts, news articles |
| **FAQPage** | AI extraction | Q&A pages (restricted to govt/health on Google since 2023, still works for AI crawlers) |
| **HowTo** | Step-by-step processes | Tutorials, guides |
| **Product** | E-commerce rich results | Product pages — price, rating, availability |
| **LocalBusiness** | Local search/Maps | Business location pages |
| **BreadcrumbList** | Navigation rich results | All pages — shows site structure |
| **Person** | E-E-A-T/author credibility | Author pages, about pages |
| **Review/AggregateRating** | Star ratings in SERPs | Product/service reviews |
| **Event** | Event rich results | Events, webinars |
| **SoftwareApplication** | App rich results | Software product pages |
| **VideoObject** | Video rich results | Pages with embedded video |
| **WebSite** | Sitelinks searchbox | Homepage |
| **Speakable** | Voice search | News articles (Google News publishers) |

### 6.2 Implementation Rules
- **JSON-LD format**: Google's recommended format, separate from HTML
- **One script tag per schema type**: Don't combine unrelated types
- **Reflect visible content only**: Schema must match what users see
- **Validate before deploy**: Rich Results Test, Schema Markup Validator
- **Monitor in Search Console**: Check rich results errors weekly
- **Multiple schemas per page**: A blog post can have Article + BreadcrumbList + Organization
- **Nest when appropriate**: Author Person inside Article

### 6.3 AI-Specific Schema Benefits
- GPT-4 accuracy improved from 16% to 54% when content had structured data (Data World study)
- Nature Communications: LLMs extract more accurately from structured fields than prose
- Pages with schema are 3x more likely to appear in AI Overviews
- Schema helps AI systems verify entities, dates, authorship, and facts

---

## 7. GENERATIVE ENGINE OPTIMIZATION (GEO)

The new discipline — optimizing for AI-generated answers.

### 7.1 What GEO Is
- Getting your brand **cited** in AI answers (ChatGPT, Gemini, Perplexity, Claude, AI Overviews)
- NOT replacing SEO — extending it
- Academic origin: Georgia Tech + Princeton + Allen Institute (KDD 2024)
- Research showed specific techniques can increase AI visibility by **up to 115%**

### 7.2 AI Crawler Access
- **robots.txt must allow**: GPTBot, ChatGPT-User, PerplexityBot, Google-Extended, ClaudeBot, anthropic-ai, Bytespider
- **Cloudflare default blocks AI bots since 2024** — check your settings!
- **Verify in server logs**: Look for AI user-agents
- **Sitemap submission**: Some AI crawlers use sitemaps
- **Clean HTML/DOM**: AI extractors need parseable content

### 7.3 Content Optimization for AI Citations
- **Statistics boost citations +33%** (GEO KDD 2024 study)
- **Expert quotations boost +41%**
- **Authoritative styling**: First-person expertise, not generic fluff
- **Explicit source citations**: AI trusts content that cites sources
- **Modular, self-contained paragraphs**: AI extracts individual chunks
- **Question-answer format**: H2/H3 as questions, 2-4 sentence direct answers immediately below
- **Concise definitions**: "X is [definition]" in first sentence of sections
- **Tables and lists**: Structured format is easier for AI to parse
- **Recency**: Content updated within 30 days gets 3.2x more AI citations (Digital Bloom 2025)
- **2023-2025+ content**: 71% of ChatGPT citations come from content published in this range (Seer Interactive)

### 7.4 Brand Entity Optimization
- **SE Ranking 129,000-domain study**: Brand-entity clarity is a top predictor of AI citation
- **Consistent brand mentions**: Same name, same description across all platforms
- **Wikipedia/Wikidata presence**: AI systems use these for entity verification
- **Schema Organization markup**: Establishes brand entity in knowledge graphs
- **About page**: Detailed company/person information with credentials
- **Author entities**: Named authors with Person schema, linked to social profiles
- **Cross-platform presence**: LinkedIn, Crunchbase, industry directories

### 7.5 Share of Model Voice (SOMV)
- New metric: How often your brand is mentioned in AI responses for relevant queries
- Track with: Semrush Enterprise AIO, Brandlight, manual sampling
- Monitor: Mentions, sentiment, competitive share, platform distribution

---

## 8. CONTENT FRESHNESS & MAINTENANCE

Keeping content competitive over time.

### 8.1 Content Refresh Strategy
- **Audit frequency**: Quarterly for high-value pages, annually for all
- **Content decay signals**: Declining traffic, dropping rankings, rising bounce rate
- **Update checklist**: Statistics, examples, screenshots, links, dates, tools mentioned
- **Republish with new date**: Only if substantial update (>30% changed)
- **Historical optimization**: Update old posts instead of creating competing new ones
- **Remove/redirect dead content**: 404 or 301 redirect low-value pages

### 8.2 Content Audit Categories
- **Keep**: High-performing, still relevant
- **Update**: Good topic, needs refresh
- **Consolidate**: Multiple thin pages → one comprehensive page
- **Remove**: No traffic, no relevance, no potential

---

## 9. ANALYTICS & MEASUREMENT

### 9.1 Key Metrics
- **Organic traffic**: Google Analytics / GA4
- **Keyword rankings**: Semrush, Ahrefs, Search Console
- **Click-through rate (CTR)**: Search Console → Performance
- **Bounce rate / Engagement rate**: GA4
- **Core Web Vitals**: Search Console → Page Experience
- **Index coverage**: Search Console → Pages
- **Backlink profile**: Ahrefs, Semrush, Moz
- **Conversion rate**: From organic traffic specifically

### 9.2 AI Visibility Metrics (GEO)
- **AI mention frequency**: How often cited in AI answers
- **AI mention sentiment**: Positive/negative/neutral
- **Share of Model Voice**: Your share vs competitors in AI responses
- **Platform distribution**: Which AI platforms cite you most
- **Citation context**: What queries trigger your citations

---

## 10. PROGRAMMATIC SEO

Scaling content creation for large sites.

### 10.1 When to Use
- Large catalogs (e-commerce, directories, marketplaces)
- Location-based pages (city/state service pages)
- Template-driven content with variable data
- Comparison/aggregation pages

### 10.2 Techniques
- **Template + data**: Create page template, populate with unique data per variation
- **Unique value**: Each page must add something a simple database query wouldn't
- **Internal linking at scale**: Programmatic cross-links between related generated pages
- **Avoid thin content traps**: Generated pages must have substantial unique content
- **Canonicalization**: Careful with near-duplicate generated pages

---

## KEY FINDINGS SUMMARY

1. **Technical SEO is now dual-purpose**: Optimize for both Googlebot AND AI crawlers
2. **INP is the new CWV bottleneck**: Most sites fail on interaction responsiveness
3. **GEO is not optional**: 15%+ of Google searches show AI Overviews, ChatGPT has 37.5M daily queries
4. **Schema markup drives AI citations**: 3x more likely to appear in AI Overviews with JSON-LD
5. **Content modularization is critical**: AI extracts individual paragraphs, not whole pages
6. **E-E-A-T is measurable via entities**: Named authors, credentials, cross-platform presence
7. **Statistics (+33%) and quotations (+41%)**: Single biggest citation boosters in AI answers
8. **Freshness matters more than ever**: 30-day updated content gets 3.2x more AI citations
9. **Brand entity clarity**: Top predictor of AI citation across 129,000 domains studied
10. **SEO + GEO must be unified**: The brands winning in 2026 optimize for both simultaneously
