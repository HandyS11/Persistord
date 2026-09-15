# Design — Documentation site revamp: design system, landing page, information architecture

**Date:** 2026-09-14
**Status:** Proposed
**Supersedes (in part):** [2026-09-10-docs-site-theme-design.md](2026-09-10-docs-site-theme-design.md)
— its section 2 colour rule and section 3 landing layout are replaced here; its constraints on
DocFX extension points, self-hosted fonts, and adapter-guide accuracy are kept.
**Source:** Review of the site built from `develop` at `f9aab4c`, measured against the four sibling
sites ([DotnetTokenKiller], [ProjGraph], [RustPlusApi], [RustMapsApi]) and against top-tier
product documentation.

[DotnetTokenKiller]: https://handys11.github.io/DotnetTokenKiller/
[ProjGraph]: https://handys11.github.io/ProjGraph/
[RustPlusApi]: https://handys11.github.io/RustPlusApi/
[RustMapsApi]: https://handys11.github.io/RustMapsApi/

## Context

PR #55 gave the site a custom theme (`docs/templates/persistord/public/{main.css,main.js}`, Inter +
JetBrains Mono, a dark violet palette, a `layout: landing` page) and a guide for every package.
That work is not yet live: `.github/workflows/Documentation.yml` deploys only on pushes to `main`,
so `handys11.github.io/Persistord` still serves stock DocFX.

Building `develop` and screenshotting it at 1440 px and 375 px, in both themes, shows these gaps:

| Area | Finding |
| --- | --- |
| Landing layout | No centred container — at 1440 px all content hugs the left edge (x≈12 px); every sibling site centres a ~1100 px column |
| Landing density | ~26 cards (4 package groups, 9 capabilities, 7 next-steps) and a 7-line bolded paragraph under the hero panes; nothing has emphasis |
| Guide pages | Code blocks are double-framed (`pre` inside a bordered surface); h2 is nearly h1-sized so hierarchy is flat; TOC group labels carry emoji |
| API pages | Stock DocFX: every link underlined, h1 and h2 sections collide, no structure around members |
| Package README pages | Badges centred awkwardly; the GitHub-oriented "← Persistord docs · Documentation site" line links to the site the reader is already on; long signatures wrap inside inline-code pills |
| Authoring features | Across 22 articles: 1 callout, 0 tab groups, 1 Mermaid diagram |
| Information architecture | 5 top tabs, two of which (Samples, Development) hold a single page; the Guides group order is arbitrary and mixes concepts with tasks |
| Site infrastructure | No sitemap, no 404 page, no meta descriptions, no `lang` |

The colour rule from the previous spec (violet = persisted, lavender = Discord, applied site-wide)
needs a 40-line stylesheet comment to police, and two tints of one hue are not reliably
distinguishable in running text.

## Decisions taken during brainstorming

- **Direction:** go beyond the sibling sites to top-tier product-documentation polish, keeping the
  DotnetTokenKiller/ProjGraph layout grammar as the starting point.
- **Generator:** stay on DocFX 2.78.5.
- **Template depth:** CSS and JS only. **No `.tmpl` or partial overrides**, and no fork of `modern`.
  `main.js` uses only DocFX's documented extension points (`defaultTheme`, `iconLinks`, `mermaid`,
  `configureHljs`, `start`). Anything this constraint cannot reach is accepted as a limit, or
  handled by a small DOM enhancement inside `start()` — never by a template override.
- **Palette:** derived from `icon.png`.
- **Content:** restructure the navigation and enrich every article (callouts, tabs, diagrams, a
  consistent page skeleton); no wholesale rewrites.
- **Delivery:** one spec, three sequential PRs (see [Delivery](#delivery)).

## Goals

- A design system — tokens, type scale, layout, components — that makes every page type (landing,
  article, package README, API reference, 404) look deliberate in both light and dark.
- A landing page with six sections, each making one point, and a diagram of the model Persistord
  ships.
- Navigation organised by what the reader is trying to do, with no single-page top tab.
- Every article using the authoring features its content calls for.

## Non-Goals

- No change to `.github/workflows/Documentation.yml`, and no deployment-trigger change.
- No template overrides (see Decisions). Open Graph image cards and header markup changes are
  therefore out of scope.
- No change to the per-package `README.md` sources — they ship inside each nupkg and render on
  GitHub and nuget.org. Site-specific treatment is CSS only.
- No moved or renamed article files; every existing URL keeps resolving.
- No new library code and no API changes.

## 1. Design system

### 1.1 Problem

The current stylesheet was built around the landing page's semantic marks. It has no type scale,
no layout container for the landing page, and an accent rule that is expensive to maintain and hard
to perceive.

### 1.2 Change

`main.css` is **rewritten**, not patched, around `--pd-*` tokens. Its header comment records the
token roles, the gradient's three permitted uses, and the list of DocFX selectors the theme depends
on (section 6).

**Palette.** Sampled from `icon.png`: ink tile `#181a27`, rim `#303050`, bubble gradient
`#3d26b8 → #6258d8 → #8685f4`, highlight `#e0dfff`.

| Token | Dark | Light | Icon source |
| --- | --- | --- | --- |
| `--pd-page` | `#0f1019` | `#f7f7fb` | ink, deepened / cool paper |
| `--pd-surface` | `#181a27` | `#ffffff` | the tile |
| `--pd-raised` | `#20233a` | `#f0f0f8` | — |
| `--pd-hairline` | `#2d3050` | `#e2e2ee` | the tile rim |
| `--pd-text` | `#e6e6f2` | `#181a27` | highlight / ink |
| `--pd-muted` | `#9a9cb8` | `#5b5d78` | — |
| `--pd-accent` | `#8685f4` | `#4635c2` | light / deep end of the gradient |
| `--pd-gradient-fill` | `linear-gradient(135deg, #3d26b8, #6258d8)` | same | the bubble fill, deep half |
| `--pd-gradient-ink` | `linear-gradient(135deg, #8685f4, #b8b6f5)` | `linear-gradient(135deg, #3d26b8, #6258d8)` | the bubble fill, the half that holds contrast on that theme's page |

The values above are starting points. Section 6's contrast check is the contract: any token that
fails its pairing is adjusted, and the adjusted value is recorded in the stylesheet.

The icon's full `#3d26b8 → #6258d8 → #8685f4` gradient cannot be used as one token. Measured during
planning, white text on its light end is 3.15:1 and its dark end is 1.96:1 against the dark page, so
it fails both as a button fill and as a mark on the page. It is therefore split: `--pd-gradient-fill`
carries white labels (every stop ≥ 5.38:1), and `--pd-gradient-ink` is the per-theme half that stays
≥ 4.5:1 against its own page, for gradient text and indicator bars.

**The gradient has exactly three uses:** the landing hero eyebrow wordmark (`--pd-gradient-ink`),
the primary button (`--pd-gradient-fill`), and the active-page indicator — sidebar bar and navbar
tab underline (`--pd-gradient-ink`). It appears nowhere else.

**Status colours** for callouts, each with a dark and a light value tuned to sit beside violet:
note = accent violet, tip = teal, warning = amber, caution = rose. IMPORTANT reuses the note colour
with a stronger surface tint.

**The global semantic colour rule is retired.** `.pd-snowflake` (lavender, "came off the gateway")
and `.pd-row-id` (violet, "is now in a column") survive **only** in the landing page's proof panes
(section 3), where the rule tells a story, and the stylesheet header says so in a few lines.

**Theme default:** follow the operating system. `main.js` exports `defaultTheme: 'auto'` explicitly
(DocFX's fallback is also `auto`; stating it records the decision). Both themes are designed; neither
is derived from the other.

**Typography.** Inter (400/600/700) and JetBrains Mono (400/700), already vendored; still no
third-party font host.

| Element | Size | Weight | Tracking | Line height |
| --- | --- | --- | --- | --- |
| Body | 1rem (16 px) | 400 | 0 | 1.7 |
| h1 | 2.25rem | 700 | −0.02em | 1.15 |
| h2 | 1.5rem | 600 | −0.01em | 1.3 |
| h3 | 1.2rem | 600 | 0 | 1.4 |
| h4 | 1rem | 600 | 0 | 1.5 |
| Code | 0.875em | 400 | 0 | 1.6 |

Prose is capped at a ~72ch measure. h2 gets generous top spacing and no rule line.

**Layout and shape.** 4 px spacing base. Radii: 6 px for small elements (inline code, buttons,
inputs), 12 px for containers (cards, code blocks, callouts, images). Landing content sits in a
centred `max-width: 1120px` container with 24 px gutters. Article pages keep DocFX's three columns
inside the centred `container-xxl`: sidebar ~260 px, content at the reading measure, affix ~220 px.

**Motion.** 150 ms ease on hover and focus transitions only. The landing snowflake wash is the only
non-user-triggered animation and is gated behind `prefers-reduced-motion: no-preference`.

### 1.3 Acceptance

- Every token pairing listed in section 6 meets WCAG AA in both themes.
- The gradient tokens are consumed in exactly the three places named above (checked by grepping
  the theme stylesheets for `var(--pd-gradient-`).
- No hard-coded colour outside the token block; toggling the theme leaves nothing unswitched.

## 2. Page frame and components

### 2.1 Problem

Chrome and content components use DocFX defaults or partial overrides that produce the double code
frame, flat heading hierarchy, stock API pages, and awkward README pages listed in Context.

### 2.2 Change

Everything here styles markup DocFX `modern` 2.78.5 already emits. Verified present in
`docfx.min.js`: code copy button (`code-action`), AnchorJS heading anchors, prev/next
(`#nextArticle`), tab groups (`tabGroup`), alerts, image lightbox, `configureHljs`.

#### Chrome

- *Navbar* — icon and wordmark with correct spacing; tabs in muted text, active tab in full text
  with a 2 px gradient underline; search as a compact field with a `/` hint; pressing `/` outside an
  input focuses search (wired in `start()`); GitHub and NuGet `iconLinks` kept; translucent
  background with backdrop blur.
- *Sidebar* — group headings as small uppercase muted labels, **no emoji** (the emoji are removed
  from every `toc.yml`); items 14 px muted; hover raises the background; current page gets the
  gradient bar, a tinted background, and full-strength text; the filter input matches search.
- *Affix ("In this article")* — a thin left rail with the active heading highlighted, replacing the
  oversized letter-spaced label.
- *Breadcrumb* — small muted text above the h1.
- *Prev/next* — two full-width cards at page bottom, each showing direction and target title.
- *Edit link* — muted, aligned with prev/next.
- *Footer* — `_appFooter` HTML (and its per-depth `fileMetadata` variants) becomes two rows: link
  groups (Docs · Packages · Community) and a tagline + MIT line.

#### Content components

- *Code blocks* — one frame only (surface background, hairline, 12 px radius); a language label
  top-left derived from the `lang-*` class in `start()`; the copy button top-right, visible on hover
  and on keyboard focus; highlight.js colours retuned to the palette for both themes.
- *Inline code* — a faint tint, no border, `box-decoration-break: clone` so wrapped signatures stay
  legible.
- *Callouts* — `[!NOTE]`, `[!TIP]`, `[!IMPORTANT]`, `[!WARNING]`, `[!CAUTION]`: left accent bar,
  tinted surface, icon and label in the status colour.
- *Tabs* — underline style, active tab in the accent colour; DocFX's cross-page tab sync is kept.
- *Tables* — raised header row, hairline row dividers, horizontal scroll contained within the table
  on narrow viewports.
- *Heading anchors* — `#` revealed on hover and focus, in muted text.
- *Mermaid* — themed from the tokens through the `mermaid` export so diagrams read correctly in both
  themes.
- *Images* — 12 px radius and hairline border; lightbox kept.

#### API reference pages (CSS only)

- Links: accent colour, no underline, underline on hover.
- Vertical rhythm between the page h1 and its "Namespaces" / "Classes" / member sections.
- Each member reads as a distinct block: DocFX emits members as flat siblings (`h3[data-uid]`,
  summary, `.codewrapper`, `h4.section`, `dl.parameters`) with no wrapping element, so the block is
  drawn in CSS — a hairline rule and spacing above each member `h3` — rather than by re-parenting
  nodes in JavaScript, which would disturb DocFX's affix and anchors. The signature sits in a code
  panel, parameters and returns as a two-column definition grid.
- Inheritance, "Implements", and assembly lines in small muted text.
- Namespace pages lay out their type lists as a two-column grid of name + summary.

#### Package README pages (CSS only, sources untouched)

Each README opens with a `<div align="center">` holding a badge paragraph and a navigation paragraph
(`← Persistord docs · Documentation site`); DocFX renders it verbatim.

- `div[align="center"]` is left-aligned, and its badge paragraph lays out as a single row.
- The navigation paragraph is hidden on the site only, by
  `div[align="center"] > p:has(> a[href="https://handys11.github.io/Persistord/"])`. No article
  uses `div[align="center"]` or links to the site's own absolute URL, so the selector cannot match
  outside the READMEs; the implementation re-checks this with a grep of `docs/` and `samples/`.

**Responsive.** At 375 px: sidebar in DocFX's offcanvas, tables and code blocks scroll internally,
no horizontal page overflow.

### 2.3 Acceptance

- No page in the section 6 screenshot matrix shows a double-framed code block, an underlined API
  link at rest, emoji in the sidebar, or horizontal page overflow at 375 px.
- Every callout type and a tab group render correctly in both themes. PR 1 adds
  `docs/development/components.md`, a contributor-facing component reference that exercises every
  component in this section and doubles as the visual test corpus before PR 3 enriches the articles.
- `/` focuses search; the copy button is reachable and announced by keyboard.
- The README back-link line is hidden on the site and still present in the README source.

## 3. Landing page

### 3.1 Problem

The landing page makes no single point with emphasis: 26 cards, a stat row of filler figures
("0 provider dependencies"), and the snowflake explanation duplicated at full length from
`articles/snowflake-conversion.md`. It never shows the model Persistord ships, which is the product.

### 3.2 Change

`docs/index.md` stays `layout: landing` with hand-written HTML. Six sections, all inside the
centred 1120 px container:

1. **Hero** (left-aligned).
   - Eyebrow: the icon mark and "Persistord" in the brand gradient.
   - Headline kept: *"Every Discord bot rewrites the same tables. Persistord ships them."*
   - Lede, two lines at desktop width: an EF Core 10 model for guilds, channels, users, members,
     roles and messages that never picks your database provider or your Discord library.
   - Actions: **Get started** (primary, gradient), **Browse packages** (outline), "GitHub"
     (text link; its arrow is DocFX's external-link icon).
   - Install strip `dotnet add package Persistord` with the existing copy button behaviour.
   - One muted meta line replacing the facts row:
     `EF Core 10 · PostgreSQL · SQL Server · SQLite · Discord.Net · DSharpPlus · NetCord`.
2. **Proof — gateway → mapper → row.** The existing three panes, full width, with arrow connectors;
   the `.pd-snowflake` / `.pd-row-id` marks and their wash kept. The seven-line explanation becomes a
   two-line caption and a "How the conversion works →" link. Every claim constraint recorded in the
   previous spec's section 3.2 (no value change shown, the provider hedge, no Steam64 example, the
   model builds without the converter) still binds the caption.
3. **The model you'd have written.** A hand-authored inline SVG, coloured from the tokens so it
   follows the theme, with `role="img"`, a `<title>`, and a `<desc>` summarising the graph. Nodes are
   grouped and labelled by package: the `Persistord.Core` skeleton (`GuildEntity`, `ChannelEntity`,
   `RoleEntity`, `MemberEntity`, `UserEntity`), `Persistord.Messages` (`MessageEntity`, `Embed` —
   a keyed entity that owns `EmbedFooter` and `EmbedAuthor` — `EmbedField`, `AttachmentEntity`,
   `ReactionEntity`), and `Persistord.History` (`MessageHistoryEntity`). **Every node and every edge
   is verified against the entity classes and their `IEntityTypeConfiguration`s** before merge, by
   `docs/tools/site-check/tests/model.test.mjs`. Two edge styles: a solid edge is a foreign key the
   model configures; a dashed edge is a snowflake id column that refers to another entity with no
   foreign key (the Core skeleton's only foreign key is `ChannelEntity.ParentId`). Arrows point at
   the referenced entity; nothing else is drawn.
4. **Build your stack.** Three steps replacing the ten package cards:
   ① `dotnet add package Persistord`;
   ② pick one adapter — an accessible tablist (Discord.Net / DSharpPlus / NetCord), each panel
   showing its install command and a ≤3-line mapper snippet checked against that adapter's source
   (DSharpPlus's `guildId` parameter on member and role mappers included);
   ③ optional add-ons — Managed · Protection · Testing — each a linked package name and one sentence.
   A "Compare all ten packages →" link goes to `articles/packages.md`.
   The tablist follows the ARIA Authoring Practices tabs pattern (roving `tabindex`, arrow keys,
   Home/End), wired in `start()`. Without JavaScript the three panels render stacked.
5. **What's in the box.** The capability cards become a 3×3 list in the DotnetTokenKiller idiom:
   left rule, title, one sentence, the whole item linking to its guide. No card chrome.
6. **Start here.** Three cards: Getting Started, Guides, API Reference.

**Mobile.** All sections stack; the SVG scales through its `viewBox` down to an 880 px minimum and
scrolls inside its figure below that, as the Mermaid diagrams do, so its labels stay legible; the
proof panes stack vertically with connectors rotated to point down.

### 3.3 Acceptance

- The landing page has at most three card-chromed elements (the "Start here" cards); the proof
  panes and the adapter tab panels are figures and panels, not cards.
- Every link resolves; every package named links to its NuGet page or its README page.
- The model SVG has been checked edge-by-edge against source (`tests/model.test.mjs` re-checks it on
  every run), and the PR description lists the configuration or column each edge came from.
- The adapter tablist is fully operable by keyboard and renders stacked with JavaScript disabled.
- Correctly stacked and legible at 375 px; no animation under `prefers-reduced-motion: reduce`.

## 4. Information architecture and content enrichment

### 4.1 Problem

Two top tabs hold one page each; the Guides group interleaves concepts and tasks in no particular
order; the articles barely use callouts, tabs, or diagrams, even where their content is a warning,
a choice between alternatives, or a relationship graph.

### 4.2 Change

**Top navigation** (`docs/toc.yml`) becomes **Docs · Packages · API**. Samples and Development
leave the navbar and are reached from the Docs sidebar. No article file moves; every existing URL
keeps resolving.

**Docs sidebar** (`docs/articles/toc.yml`):

| Group | Pages, in order |
| --- | --- |
| Start | Introduction · Getting Started · Packages |
| Concepts | Snowflake Conversion · Core Graph · Messages · History · Soft-delete & Query Filters · DbContext Lifetime |
| Guides | Migrations · Providers · Upsert · Guild Lifecycle · Recipes |
| Add-ons | Managed Resources · Protection · Testing |
| Adapters | Choosing an Adapter · Discord.Net · DSharpPlus · NetCord |
| Resources | Samples · Troubleshooting · Upgrading · Release notes · Contributing |

"Release notes" is an external link to `https://github.com/HandyS11/Persistord/releases`.
"Contributing" links to `development/index.md` and nests "Component reference"
(`development/components.md`); "Samples" nests "All samples" (`samples/README.md`). DocFX gives a
page the `toc.yml` of its own folder whenever one exists, even if another TOC references the page,
so `docs/development/toc.yml` and `docs/samples/toc.yml` are deleted; no page file moves.

**New page:** `articles/upgrading.md`, holding the "Upgrading from 1.0.0-beta2" section extracted
from `articles/core-graph.md` (which keeps a one-line pointer to it). Future upgrade notes go here.

**Enrichment rules, applied to every article:**

- **Callouts** replace prose that is *already* a note, warning, or caution — for example the
  plaintext warning in `protection.md`, the long-lived-context failure modes in
  `dbcontext-lifetime.md`, and the query-filter and tracking caveats in `upsert.md`. At most about
  two per page. No callout introduces a claim the article did not already make.
- **Tabs** only where the reader picks exactly one option:
  provider setup (PostgreSQL / SQL Server / SQLite) in `getting-started.md` and `providers.md`;
  adapter usage (Discord.Net / DSharpPlus / NetCord) in `getting-started.md`, `recipes.md`
  ("Map from …"), and `adapters.md`. Tab ids are shared (`#tab/postgresql`, `#tab/discordnet`, …)
  so a reader's choice carries across pages: DocFX syncs same-id groups within a page and honours
  `?tabs=<id>`, and the theme (`rememberTabChoices()` in `start()`) remembers the reader's choices
  for the browsing session and applies them on each page; an explicit `?tabs=` wins for the groups
  it names.
- **Mermaid diagrams**, themed per section 2:
  `core-graph.md` (entity relationships), `messages.md` (message → embeds, attachments, reactions),
  `history.md` (message → history), `guild-lifecycle.md` (JoinedGuild/LeftGuild sequence),
  `managed-resources.md` (reconcile loop), `protection.md` (encrypt/decrypt path).
  `packages.md` keeps its existing dependency graph. ER diagrams follow the landing diagram's edge
  rule (solid = configured foreign key, dotted = id column with no foreign key) and are checked
  against the same source evidence.
- **Page skeleton:** every article gets a `description:` front-matter entry (rendered as its
  `<meta name="description">`), a one-sentence lede, and a uniform "See also" list.
  `troubleshooting.md` entries adopt a consistent **Symptom / Cause / Fix** structure.
- **Getting Started as a tutorial:** "What you'll build", prerequisites, numbered steps, and a
  closing "Run it" step showing the expected result.
- **Tightening** only where content is duplicated across `introduction.md`, the landing page, and
  `packages.md`; each fact gets one home and the others link to it.
- **Accuracy:** every signature, snippet, tab panel, diagram node and edge, and version range is
  checked against the source that ships it, as the previous spec required for the adapter guides.

### 4.3 Acceptance

- The navbar has three tabs; every pre-existing `articles/*.html`, `src/*/README.html`,
  `samples/README.html`, and `development/index.html` URL still resolves.
- `development/index.html` and `samples/README.html` show a sidebar with more than one entry.
- Every article has a `description:`; `upgrading.md` exists and `core-graph.md` links to it.
- Tabs and diagrams appear on exactly the pages listed above (plus `packages.md`'s dependency
  graph), and each was reviewed against source; `tests/content.test.mjs` re-checks placement, the
  ER edges, and the adapter and provider snippets on every run.

## 5. Site infrastructure

### 5.1 Problem

The site has no sitemap, no `lang`, no meta descriptions, and no 404 page. GitHub Pages serves
`/404.html` **at the missing URL**, so a normal DocFX-built 404 page, whose stylesheet and script
links are relative, renders unstyled at any missing path deeper than the site root.

### 5.2 Change

In `docs/docfx.json`:

- `build.sitemap` with `baseUrl: "https://handys11.github.io/Persistord/"`.
- `globalMetadata`: `_lang: "en"`.
- `build.resource` gains `404.html`.

**Descriptions, without duplicates.** `modern`'s `_master.tmpl` emits one
`<meta name="description">` for `_description` **and another** for page-level `description`, so a
global `_description` would give every described article two tags. Therefore there is **no global
`_description`**. Instead:

- `docs/index.md` and every article carry page-level `description:` front matter (the articles in
  PR 3; the landing page in PR 2).
- `fileMetadata.description` covers the pages that have no front matter of their own: `api/**.yml`
  (a generic API-reference description), `src/*/README.md`, `samples/README.md`, and `license.md`.
  These globs do not overlap the described pages.

`docs/404.html` is a **self-contained static page**, copied verbatim as a resource rather than built
as content. It links `/Persistord/public/docfx.min.css` and `/Persistord/public/main.css` by absolute
path, so it consumes the same tokens and fonts rather than duplicating them; applies the stored or OS
theme with an inline script; and links to Docs, Packages, API, and the home page.

### 5.3 Acceptance

- `_site/sitemap.xml` exists and lists absolute URLs under the base URL.
- Built pages carry `lang="en"`. No built page has more than one `<meta name="description">`; after
  PR 3, every page in `_site` has exactly one. (`toc.html` sidebar fragments are not pages;
  `404.html` carries its own.)
- The 404 renders fully styled in both themes when served at a deep missing path. GitHub Pages'
  behaviour is reproduced by a local static server that serves `_site` under a `/Persistord/`
  prefix and answers any missing path with `404.html` and status 404, as Pages does; the page is
  loaded at `/Persistord/articles/missing/deeper.html`.

## 6. Verification

### 6.1 Problem

CSS-only theming against another project's markup fails quietly: a selector that stops matching
produces an unstyled element, not a build error. The light theme regresses unnoticed when work is
done in dark.

### 6.2 Change

Each PR runs the checks covering its scope:

1. **Build gate.** `dotnet docfx docs/docfx.json --warningsAsErrors` exits zero. The build is
   warning-clean on `develop` today, so the gate is meaningful from the first PR.
2. **Screenshot matrix.** The built site served locally and driven with Playwright at 1440 px and
   375 px, in light and dark, across: landing; `core-graph` (Mermaid); `getting-started` (tabs);
   `protection` (callouts); one package README; one API namespace page; one API class page; the
   search results view; the 404 at a deep missing path. Pass means nothing unstyled, unreadable,
   clipped, or overflowing.
3. **Contrast.** A script computes WCAG contrast for: text, muted, and accent on page, surface, and
   raised; each status colour's label on its tinted callout surface; code tokens on the code surface.
   Both themes. Pass is AA (4.5:1 body text, 3:1 large text and UI boundaries).
4. **Link crawl.** No internal link in `_site` 404s; no theme asset (CSS, JS, font) resolves to a
   third-party origin. The README shield badges remain the accepted deviation recorded in the
   previous spec.
5. **Keyboard and motion.** Tab through the landing page and one article: visible focus on every
   interactive element; adapter tabs operable with arrow keys, Home and End; `/` focuses search.
   With `prefers-reduced-motion: reduce`, nothing animates.
6. **Drift guard.** The `main.css` header lists every DocFX selector the theme depends on. DocFX is
   pinned (`2.78.5`, `rollForward: false` in `.config/dotnet-tools.json`); any upgrade re-runs the
   screenshot matrix and re-checks that list.

### 6.3 Acceptance

- All six checks pass for the PR's scope, and the PR description links or attaches the screenshot
  set.

## Delivery

Three sequential PRs from this spec, each with its own implementation plan:

| PR | Scope | Sections |
| --- | --- | --- |
| 1 — Design system and page frame | Tokens, typography, layout, chrome, components, API and README page styling, footer, sitemap, `lang`, `fileMetadata` descriptions for API/README pages, 404, emoji removed from TOC labels | 1, 2, 5, 6 |
| 2 — Landing page | The six-section landing page on top of PR 1 | 3, 6 |
| 3 — Information architecture and content | Navigation restructure, `upgrading.md`, per-article enrichment | 4, 6 |

PR 1 changes no article content, so the existing articles are its test corpus. PR 3 depends on PR 1's
callout, tab, and Mermaid styling.

## Risks

| Risk | Mitigation |
| --- | --- |
| CSS cannot reach some DocFX markup | Accept the limit, or a small DOM enhancement in `start()`; never a template override |
| A DocFX upgrade changes a selector the theme depends on | Version pinned; selector list in the `main.css` header; screenshot matrix on upgrade |
| Tabs, snippets, or diagrams drift from the shipped API | Section 4 accuracy rule; edge-by-edge SVG review in PR 2 |
| Light theme regresses unseen | Both themes in every screenshot run and contrast check |
| Landing page caption re-introduces a false snowflake claim | Previous spec's section 3.2 constraints still bind and are cited in section 3 |
| 404 page drifts from the theme it copies | It consumes `main.css` tokens by absolute path rather than duplicating them |
