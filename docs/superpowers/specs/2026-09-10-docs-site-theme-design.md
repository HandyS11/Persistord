# Design — Documentation site: custom theme and content expansion

**Date:** 2026-09-10
**Status:** Proposed
**Source:** Review of `docs/` on `develop` at `f446685`, measured against the four sibling
documentation sites the maintainer has already shipped: [RustPlusApi], [RustMapsApi],
[DotnetTokenKiller], and [ProjGraph].

[RustPlusApi]: https://handys11.github.io/RustPlusApi/
[RustMapsApi]: https://handys11.github.io/RustMapsApi/
[DotnetTokenKiller]: https://handys11.github.io/DotnetTokenKiller/
[ProjGraph]: https://handys11.github.io/ProjGraph/

## Context

The Persistord site is built by DocFX 2.78.5 from `docs/docfx.json` and deployed to GitHub
Pages by `.github/workflows/Documentation.yml`. It uses the stock `default` + `modern`
templates with no customisation, and its landing page is ordinary Markdown.

Measured against the four sibling sites, the gaps are:

| Piece | Sibling sites | Persistord today |
| --- | --- | --- |
| Custom template | `docfx/templates/<name>/public/{main.css,main.js}` | none |
| Self-hosted fonts | Inter + JetBrains Mono, woff2 | none (system stack) |
| Landing page | `layout: landing`, hand-written HTML hero | plain Markdown, 5 links |
| Logo / favicon | `_appLogoPath` + `_appFaviconPath` | neither; `icon.png` unused |
| Footer | `_appFooter` with project nav | docfx default |
| Edit links | `_gitContribute` | not set |
| Icon links (GitHub, NuGet) | `iconLinks` in `main.js` | none |

Content has a matching hole. Persistord ships **ten** NuGet packages, three of them Discord
library adapters. `Persistord.Adapters.DiscordNet` has a guide; `Persistord.Adapters.DSharpPlus`
and `Persistord.Adapters.NetCord` have **none** — they are mentioned only in passing in
`introduction.md` and `getting-started.md`. The packaging story (what each package ships, what
depends on what, what is deliberately excluded from the meta package) exists only in the root
`README.md` and nowhere on the site. The ten per-package `README.md` files and
`samples/README.md` are not part of the site at all, though ProjGraph demonstrates the DocFX
pattern for including them.

`docs/docfx.json` also references `api/index.md`, which does not exist. The glob silently
matches nothing, so the API tab has no landing page.

## Goals

- A custom DocFX theme for Persistord that reads as a sibling of the four existing sites,
  derived from the brand colours already present in `icon.png`.
- A landing page that states the value proposition visually, in the before/after idiom the
  sibling sites use.
- Documentation for every shipped package, with no package lacking a guide.
- The per-package READMEs and samples surfaced on the site rather than only on GitHub.

## Non-Goals

- No rewrite of the 19 existing article bodies. They are accurate; they gain cross-links and
  TOC regrouping only.
- No change to `.github/workflows/Documentation.yml`. It already builds `docs/docfx.json`.
- No fork of DocFX's `modern` template, and no `.tmpl` partial overrides unless section 6
  proves CSS cannot reach something.
- No new library code. This spec touches `docs/` and `.gitignore`. The one exception is two
  XML doc-comment `cref` values in `src/Persistord.Core/Interception/TimestampInterceptor.cs`,
  required by section 6 and scoped to the comment text — no behaviour changes.

## 1. Theme package

### 1.1 Problem

The site is visually indistinguishable from any other stock DocFX build, and none of the
brand assets already in the repository (`icon.png`) reach it.

### 1.2 Change

Add a template directory beside the existing DocFX inputs:

```
docs/templates/persistord/public/
  main.css
  main.js
  fonts/inter-latin-400-normal.woff2
  fonts/inter-latin-600-normal.woff2
  fonts/inter-latin-700-normal.woff2
  fonts/jetbrains-mono-latin-400-normal.woff2
  fonts/jetbrains-mono-latin-700-normal.woff2
```

Fonts are vendored from the Fontsource CDN (~115 KB total, five faces) and committed. The
site must not depend on a third-party font host at page load.

`docs/docfx.json` changes:

- `build.template` becomes `[ "default", "modern", "templates/persistord" ]`.
- `build.resource` gains an entry with `src: ".."` and `files: [ "icon.png" ]`, replacing the
  dead `images/**` glob that currently matches nothing (section 6).
- `build.globalMetadata` gains `_appLogoPath: "icon.png"`, `_appFaviconPath: "icon.png"`,
  `_appFooter` (raw HTML: tagline plus GitHub / NuGet / Releases / MIT links), and
  `_gitContribute` pointing at `https://github.com/HandyS11/Persistord` on branch `develop`.

`main.js` uses only DocFX's documented extension points — `defaultTheme`, `iconLinks`,
`mermaid`, `start` — so a DocFX upgrade cannot break it through an undocumented seam. It
provides `defaultTheme: 'dark'`, icon links to GitHub and NuGet, Mermaid font configuration
restricted to theme-agnostic values, and the copy-button behaviour used by the landing page's
install strips.

`main.css` overrides Bootstrap custom properties (`--bs-*`) and documented DocFX selectors
only, for the same reason. Its header comment states this constraint and the colour rule from
section 2, so a later editor knows what is load-bearing.

### 1.3 Acceptance

- `dotnet docfx docs/docfx.json` emits the theme's `main.css`, `main.js`, and all five font
  files under `_site/public/`.
- Rendered pages show the Persistord logo in the navbar, the favicon in the browser tab, the
  custom footer, an "Edit this page" link resolving to `blob/develop`, and GitHub + NuGet icon
  links in the navbar.
- No `@import` or `<link>` in the built site resolves to a third-party origin.

## 2. Colour, typography, motion

### 2.1 Problem

A theme needs a rule for when colour is applied, or accent colour spreads until it means
nothing. The sibling sites each state such a rule in their stylesheet header; Persistord needs
one of its own.

### 2.2 Change

Tokens are prefixed `--pd-` and sampled from `icon.png` (ink `#181a27`, violet `#3d26b8` and
`#4635c2`, mid-violet `#8685f4`, pale lavender `#e0dfff`):

| Token role | Dark | Light |
| --- | --- | --- |
| page background | `#0d0f1a` | `#f6f6fb` |
| surface | `#161a2b` | `#ffffff` |
| hairline | `#242a44` | `#dcdcec` |
| violet (accent) | `#8685f4` | `#4635c2` |
| lavender (accent) | `#e0dfff` | `#3d26b8` |

The light theme is designed rather than derived: it is cool paper with the violet darkened far
enough to hold contrast on white, not an inversion of the dark ramp.

**The colour rule, stated in the stylesheet header:** violet marks *persisted* things — rows,
columns, keys, migrations, anything that reaches the database. Lavender marks *Discord* things
— gateway payloads, snowflake ids, events. Anything that is neither is chrome, and chrome is
never accented.

Typography is Inter for text and JetBrains Mono for code, both self-hosted with
`font-display: swap`.

Motion is limited to a single landing-page animation: the snowflake id moving between the
transform panes in section 3. It is wrapped in `prefers-reduced-motion: reduce` and is the only
non-user-triggered movement on the site.

### 2.3 Acceptance

- Body text and both accents meet WCAG AA contrast against their own background in both themes.
- Toggling light/dark leaves no element unreadable and no hard-coded colour unswitched.
- With `prefers-reduced-motion: reduce`, the landing page performs no animation and the panes
  render in their final state.

## 3. Landing page

### 3.1 Problem

`docs/index.md` is 891 bytes of prose and five links. It never states what problem Persistord
solves, never shows the library working, and never mentions nine of the ten packages.

### 3.2 Change

`docs/index.md` becomes a `layout: landing` page of hand-written HTML sections, all styled by
`main.css`:

1. **Hero** — headline *"Every Discord bot rewrites the same tables. Persistord ships them."*,
   a lede naming EF Core 10, the six core entities, and the two things the library refuses to do
   (choose a provider, reference a Discord library), and three calls to action: Get started /
   Browse packages / View source.
2. **The transform** — three columns reading *gateway event → `.ToMessageEntity()` → the
   `messages` row*. The snowflake `1234567890123456789` appears in lavender on the left and
   lands as violet `-8211653183586094899` in a `BIGINT` column on the right, annotated *"same
   64 bits, read back unsigned"*. This is the sharpest idea in the library and it carries the
   page.
3. **Install strip** — `dotnet add package Persistord` with a copy button.
4. **Facts row** — 3 Discord libraries · 6 core entities · 0 provider dependencies · 10 packages.
5. **Packages** — cards in three groups: *the stack* (`Persistord`, `.Core`, `.Messages`,
   `.History`), *adapters, pick at most one* (`.Adapters.DiscordNet`, `.Adapters.DSharpPlus`,
   `.Adapters.NetCord`), and *opt-in* (`.Managed`, `.Protection`, `.Testing`). The grouping
   carries the packaging decision recorded in the root README: the meta package is the
   library-neutral mirror stack and nothing more.
6. **Capabilities** — snowflake conversion, core graph, upsert, soft-delete and query filters,
   history, guild purge, managed resources, protection, testing fixtures; each linking to its
   guide.
7. **Where to go next** — link cards into the guides and the API reference.

The copy-button behaviour degrades the way ProjGraph's does: on clipboard refusal it selects the
command text and tells the user which shortcut to press, so the button always reports an outcome.

### 3.3 Acceptance

- The landing page renders with no sidebar and no article chrome (`layout: landing` honoured).
- All ten packages are named and linked to their NuGet page.
- Every capability card links to an existing guide; no card is a dead end.
- The page is legible and correctly stacked at 375 px width.

## 4. New documentation pages

### 4.1 Problem

Two shipped packages have no guide. The packaging story is absent from the site. The API tab
has no landing page, and `docfx.json` points at a file that does not exist.

### 4.2 Change

All three adapters expose an identical seven-method surface — `ToGuildEntity`,
`ToChannelEntity`, `ToUserEntity`, `ToMemberEntity`, `ToRoleEntity`, `ToMessageEntity`, and
`ToHistoryEntity(changeType)`. The existing `articles/discord-net-adapter.md` already documents
that surface well; it is the **template** the two new guides mirror, not a page needing repair.
Its structure — install, usage, mapper table, what mappers copy and leave alone, versioning,
see-also — is reused verbatim as a skeleton.

New files:

| File | Contents |
| --- | --- |
| `articles/adapters.md` | What an adapter is and why it is a separate package; a comparison table across the three adapters covering source types and per-library behaviour; how to map by hand when no adapter fits |
| `articles/dsharpplus-adapter.md` | The Discord.Net guide's structure, against DSharpPlus source types and its supported version range |
| `articles/netcord-adapter.md` | The same, against NetCord source types, including NetCord-specific behaviour such as `Username` resolution through the client |
| `articles/packages.md` | Package matrix; a Mermaid dependency graph; an install decision guide; why `.Managed`, `.Protection`, and `.Testing` are excluded from the meta package |
| `api/index.md` | API reference landing page: the namespace map and where to start |
| `license.md` | MIT licence text, linked from the footer |

Because the three adapter surfaces are identical, `adapters.md` compares **source types and
gotchas**, not capabilities. It must not imply one adapter can do more than another.

Content for the two new adapter guides is derived from the existing package READMEs
(`src/Persistord.Adapters.DSharpPlus/README.md`, `src/Persistord.Adapters.NetCord/README.md`)
and from the mapper source, never invented. Every method signature and version range stated in
a guide must be checked against the code that ships it.

`docs/api/` is currently ignored by `.gitignore` line 416 because DocFX generates into it. A
negation alone is not enough: git does not descend into an excluded **directory**, so
`!docs/api/index.md` under a `docs/api/` rule has no effect. The rule must be rewritten to
exclude the directory's *contents* instead:

```gitignore
docs/api/*
!docs/api/index.md
```

This keeps the hand-written landing page tracked while the generated YAML, `toc.yml`, and
`.manifest` stay ignored.

### 4.3 Acceptance

- Every one of the ten packages is reachable from the site within two clicks of the landing page.
- Each adapter guide's mapper table matches the extension methods actually declared in that
  adapter's source.
- Each adapter guide's stated version range matches the range in that package's `.csproj`.
- `docs/api/index.md` is tracked by git and the generated `docs/api/*.yml` is not.

## 5. Site structure

### 5.1 Problem

The top-level TOC is *Articles / Development / API*. The per-package READMEs and
`samples/README.md` live outside the site. The Guides TOC has thirteen flat entries under one
heading, with the sole adapter page among them.

### 5.2 Change

`docs/toc.yml` becomes:

```
Guides · Packages · Samples · Development · API Reference
```

**Packages** and **Samples** pull content from outside `docs/` using the pattern established in
ProjGraph: a `build.content` entry with `src: ".."` listing `src/*/README.md` and
`samples/README.md`, with TOC entries linking via `href: ../../src/<Package>/README.md`. The
`../../` depth is a function of where the entry sits, and getting it wrong produces a broken
link rather than a build failure — section 6 covers how that is caught.

Within `articles/toc.yml`, the existing groups gain an **Adapters** group holding
`adapters.md`, `discord-net-adapter.md`, `dsharpplus-adapter.md`, and `netcord-adapter.md`, and
`packages.md` joins the *Get Started* group. Group headings gain emoji prefixes, matching
ProjGraph and DotnetTokenKiller.

Existing article URLs are unchanged: files stay at `articles/<name>.html`, and only the
navigation labels and grouping move. No redirects are needed.

### 5.3 Acceptance

- All five top-level tabs resolve and none is empty.
- All ten package READMEs and `samples/README.md` render as site pages.
- Every pre-existing `articles/*.html` URL still resolves.

## 6. Build and verification

### 6.1 Problem

Cross-root TOC links and relative asset paths fail quietly. DocFX reports an invalid file link
as a warning and exits zero, so a broken navigation tree ships green.

DocFX's `--warningsAsErrors` flag exists but is global — there is no per-category form. The
build on `develop` at `f446685` is **not** warning-clean: it emits two `InvalidCref` warnings
for `"O:DbContext.SaveChanges"` and `"O:DbContext.SaveChangesAsync"` in
`src/Persistord.Core/Interception/TimestampInterceptor.cs`. Until those are resolved the flag
cannot serve as a gate, because it would fail on pre-existing noise unrelated to this work.

The same build logs `No files are found with glob pattern images/**` — a third dangling
reference in `docfx.json`, alongside the missing `api/index.md` from section 4.

### 6.2 Change

Verification for this work is:

0. The two `InvalidCref` values are corrected so the baseline build is warning-clean, and the
   dead `images/**` resource glob is removed from `docfx.json` — `icon.png` arrives through the
   `src: ".."` resource entry added in section 1 instead. Both are prerequisites for step 1.
1. `dotnet docfx docs/docfx.json --warningsAsErrors` exits zero, so a wrong `../../` depth in
   section 5 fails the check instead of shipping.
2. The built site served locally and driven with Playwright to screenshot, in **both** light and
   dark: the landing page, one guide page, one package README page, and one API page. The light
   theme is checked explicitly because it is designed independently in section 2 and is the one
   that regresses unnoticed.
3. A crawl of the built `_site` confirming no internal link 404s and no asset resolves to a
   third-party origin.

`.github/workflows/Documentation.yml` is unchanged. It runs `dotnet docfx docs/docfx.json`,
which picks up the new template through `docfx.json` with no workflow edit.

### 6.3 Acceptance

- `dotnet docfx docs/docfx.json --warningsAsErrors` exits zero, with no `InvalidCref`,
  `InvalidFileLink`, `InvalidBookmark`, or dangling-glob warnings.
- Screenshots exist for both themes and show no unstyled, unreadable, or overflowing content.
- No internal link in `_site` 404s.

## Risks

| Risk | Mitigation |
| --- | --- |
| A DocFX upgrade moves a selector `main.css` depends on | Overrides restricted to `--bs-*` properties and documented selectors; constraint recorded in the stylesheet header |
| Cross-root `../../` TOC links break silently | Section 6's warnings-as-failures check |
| Light theme regresses unseen | Explicit light-mode screenshots in section 6 |
| Adapter guides drift from the shipping mapper surface | Section 4 requires every signature and version range to be checked against source |
