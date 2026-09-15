# Docs Site Revamp — PR 2: Landing Page — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild `docs/index.md` as the six-section landing page of spec section 3 (hero, proof, model diagram, build your stack, what's in the box, start here) on top of the PR 1 design system. Every section is verified by the `docs/tools/site-check` harness, and the model diagram is checked edge by edge against the EF Core configurations.

**Architecture:** The page stays `layout: landing` with hand-written HTML in `docs/index.md`, styled only by `docs/templates/persistord/public/css/landing.css`, which this plan rewrites section by section. The model diagram is an inline SVG coloured by CSS classes that read the design tokens. The adapter picker is authored as stacked panels and upgraded to an ARIA tabs widget by `wireTabs()` inside `main.js`'s `start()`. Three new harness suites cover the work: `landing.test.mjs` for page structure, `model.test.mjs` for diagram accuracy, geometry and colour, and `adapters.test.mjs` for the tabs widget and snippet accuracy. The model and adapter suites read the C# sources, so the page fails its tests if the library drifts away from it.

**Tech Stack:** DocFX 2.78.5 (pinned), vanilla CSS on the PR 1 tokens, inline SVG, vanilla ES modules, Node ≥ 22 `node:test`, Playwright 1.63.0 (Chromium).

**Spec:** `docs/superpowers/specs/2026-09-14-docs-site-revamp-design.md` — section 3 and section 6 (PR 2 row of its Delivery table). Task 6 amends the spec with the deviations recorded below.

## Global Constraints

- **No template overrides.** Never add a `.tmpl`, `.tmpl.partial`, or `layout/` file under `docs/templates/persistord`.
- **`main.js` extension points only:** `defaultTheme`, `iconLinks`, `mermaid`, `configureHljs`, `start`. DOM enhancements live inside `start()`.
- **Every colour is a token.** No stylesheet other than `css/tokens.css` writes a hex, `rgb()`, or `hsl()` (enforced by `tests/tokens.test.mjs`). The inline SVG carries **no** `fill`, `stroke`, `color`, or `style` attribute — it is coloured only by classes in `landing.css`. No new tokens are needed; reuse `--pd-surface`, `--pd-raised`, `--pd-hairline`, `--pd-control-border`, `--pd-text`, `--pd-heading`, `--pd-muted`, `--pd-accent`.
- **Gradient budget (spec 1.2):** after this PR, `var(--pd-gradient-*)` is consumed by exactly four selectors: `#navbar .navbar-nav .nav-link.active::after`, `.toc li.active:not(:has(li.active)) > a::before`, `.pd-btn-primary`, `.pd-wordmark`.
- **Contrast is the contract:** WCAG AA — 4.5:1 text, 3:1 large text, UI boundaries and meaningful graphics — in both themes.
- **Snowflake claims (spec 3.2, carrying the previous spec's section 3.2):** never show an id changing value; never claim that *no* relational provider has an unsigned 64-bit type (name PostgreSQL and SQL Server); never cite a Steam64 id; never claim the model fails to build, or a `ulong` cannot be mapped, without the converter.
- **Model diagram rule (user decision, 2026-09-15):** a **solid** edge is a foreign key the model configures; a **dashed** edge is a snowflake id column that refers to another entity with **no** foreign key. Arrows point at the referenced entity. Nothing else is drawn. The diagram covers `ApplyCoreGraph()`, `ApplyMessagesModule()` and `ApplyHistoryModule()`; `ApplyGuildRoot()` is opt-in and adds no key to any drawn entity (none implements `IGuildScoped`).
- **`Embed` is not an owned type** (spec 3.2 wording is wrong): it is a keyed entity (`EmbedEntityConfiguration.HasKey`) that owns `EmbedFooter` and `EmbedAuthor` and has many `EmbedField`s. Draw it that way.
- **Raw HTML in `docs/index.md`:** no blank line anywhere inside a top-level element (Markdig ends an HTML block at a blank line and resumes Markdown parsing). Lines inside `<pre>` start at column 0, because indentation becomes part of the code. DocFX's HTML pass lowercases attribute names (`viewBox` becomes `viewbox` in `_site/index.html`), and the browser's HTML parser restores SVG camelCase. Assert SVG attributes through the DOM, never by grepping `_site`.
- **DocFX behaviour on the landing page (probed 2026-09-15):** every `pre > code` is highlighted and gets a `.code-action` copy button, and `labelCodeBlocks()` captions it. `pre` without `code` (the proof panes) is left alone. Links in `article` to another host get `class="external"`, `target="_blank"` and a trailing icon through `a.external[href]::after`. AnchorJS adds `.anchorjs-link` to headings.
- **Links:** link a guide by its source path (`articles/upsert.md`), and a package README from `docs/index.md` as `../src/<Package>/README.md` (it renders at `src/<Package>/README.html`). Every package named in `<code>` on the page links to its README page.
- **Accessibility:** the adapter picker follows the WAI-ARIA Authoring Practices tabs pattern with automatic activation. It uses a roving `tabindex`, Left/Right arrows that wrap, and Home/End. With JavaScript disabled, the three panels render stacked under their own headings. The one animation (the snowflake wash) is gated behind `prefers-reduced-motion: no-preference`.
- **Do not modify:** `.github/workflows/Documentation.yml`, anything under `src/` or `samples/`, `docs/articles/**`, `docs/docfx.json`, `css/tokens.css`, or any PR 1 stylesheet other than `landing.css`.
- **DocFX build gate:** `dotnet docfx docs/docfx.json --warningsAsErrors` exits 0. `npm run build:fast` (in `docs/tools/site-check`) rebuilds in seconds once a full `npm run build` has run.
- **Markdown lint:** `npx --no-install markdownlint-cli2 docs/index.md` (MD013, MD033, MD041 are disabled in `.markdownlint.json`).
- **Repository facts:** repo `https://github.com/HandyS11/Persistord`; work on branch `docs/landing-page` off `develop`; Pages base URL `https://handys11.github.io/Persistord/`.
- **Commits** end with the trailer `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`.

### Recorded deviations from the spec (Task 6 amends the spec text)

1. Model diagram: two edge styles, per the rule above (spec said "draws only relationships the model configures").
2. `Embed` drawn as a keyed entity owning Footer/Author, not as an owned type of `MessageEntity`.
3. The SVG keeps a `min-width` of 880px and scrolls inside its figure below that width, as PR 1's Mermaid diagrams do, instead of shrinking through its `viewBox` until its labels are illegible at 375px.
4. The hero text link reads "GitHub"; its arrow is DocFX's external-link icon rather than a literal "→".
5. PR 1 leftovers: spec 5.2 names `fileMetadata._description`, but PR 1 ships `fileMetadata.description`. Spec 6.2's screenshot matrix omits the search-results view, which PR 1 styles and screenshots.

### Out of scope

The PR 1 review follow-ups (API class meta descriptions, table squeeze at 375px, `--bs-*-rgb` duplication, `:root` token fallback, oklch in the tokens test, `@import` concatenation, the `search.test.mjs` query, IMPORTANT vs NOTE callouts, `READING_LINE`) stay deferred to PR 3 or later. Only the landing wordmark's gradient use belongs here.

---

## File Structure

### Created

| Path | Responsibility |
| --- | --- |
| `docs/tools/site-check/tests/landing.test.mjs` | Hero, proof section, what's-in-the-box, start-here, package links, retired blocks, dead landing selectors, 375px overflow |
| `docs/tools/site-check/tests/model.test.mjs` | Model diagram: nodes/edges vs source evidence, completeness, geometry, token colours, accessible name |
| `docs/tools/site-check/tests/adapters.test.mjs` | Adapter tabs: ARIA semantics, keyboard, no-JS stacking, snippet signatures vs adapter source, README links |

### Modified

| Path | Change |
| --- | --- |
| `docs/index.md` | `description` front matter; rewritten section by section (Tasks 1–5) |
| `docs/templates/persistord/public/css/landing.css` | Rewritten section by section; retired blocks removed |
| `docs/templates/persistord/public/main.js` | `wireTabs()` added to `start()`; header comment |
| `docs/templates/persistord/public/main.css` | Header: gradient budget now names the wordmark |
| `docs/tools/site-check/lib/site.mjs` | `open()` accepts `javaScriptEnabled` |
| `docs/tools/site-check/tests/tokens.test.mjs` | `GRADIENT_USES` gains `.pd-wordmark` |
| `docs/tools/site-check/tests/infra.test.mjs` | `index.html` joins `DESCRIBED_BY_FRONT_MATTER` |
| `docs/tools/site-check/tests/keyboard.test.mjs` | Tab walk covers the landing page |
| `docs/superpowers/specs/2026-09-14-docs-site-revamp-design.md` | Deviations amended |

### Final section order of `docs/index.md`

`section.pd-hero` → `section.pd-section.pd-transform` → `section.pd-section.pd-model` → `section.pd-section.pd-stack` → `section.pd-section.pd-box` → `section.pd-section.pd-start`.

---

### Task 1: Hero, page description, and the wordmark's gradient use

**Files:**

- Create: `docs/tools/site-check/tests/landing.test.mjs`
- Modify: `docs/index.md`, `docs/templates/persistord/public/css/landing.css`, `docs/templates/persistord/public/main.css`, `docs/tools/site-check/tests/tokens.test.mjs`, `docs/tools/site-check/tests/infra.test.mjs`

**Interfaces:**

- Consumes (PR 1 harness): `launchSite()`, `computed(page, selector, properties, pseudo?)`, `tokens(page, names)`, `THEMES` from `lib/site.mjs`; `parseColor`, `sameColor` from `lib/color.mjs`.
- Produces: `tests/landing.test.mjs` with the module-level `site`, `LANDING = 'index.html'`, and `lineCount(page, selector) => Promise<number>` that later tasks append tests to. Classes `.pd-eyebrow`, `.pd-wordmark`, `.pd-text-link`, `.pd-hero-meta`. `.pd-install` now has `margin: 0`, and each context sets its own spacing.

- [ ] **Step 1: Branch**

```bash
git switch develop && git pull --ff-only && git switch -c docs/landing-page
```

- [ ] **Step 2: Write the failing hero tests**

`docs/tools/site-check/tests/landing.test.mjs`:

```js
import assert from 'node:assert/strict'
import { after, before, test } from 'node:test'
import { parseColor, sameColor } from '../lib/color.mjs'
import { THEMES, computed, launchSite, tokens } from '../lib/site.mjs'

const LANDING = 'index.html'

/* Rendered text lines of a block, from its content height and line height. */
const lineCount = (page, selector) =>
  page.$eval(selector, element => {
    const style = getComputedStyle(element)
    const height = element.getBoundingClientRect().height - parseFloat(style.paddingTop) - parseFloat(style.paddingBottom)
    return Math.round(height / parseFloat(style.lineHeight))
  })

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

test('the hero leads with the wordmark, headline, lede, actions, install strip and meta line', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const hero = await page.$eval('.content article .pd-hero', section => {
      const kind = link =>
        link.classList.contains('pd-btn-primary')
          ? 'primary'
          : link.classList.contains('pd-btn')
            ? 'outline'
            : link.classList.contains('pd-text-link')
              ? 'text'
              : link.className
      const text = element => element?.textContent.replace(/\s+/g, ' ').trim()
      return {
        mark: section.querySelector('.pd-eyebrow > img')?.getAttribute('alt'),
        wordmark: text(section.querySelector('.pd-eyebrow > .pd-wordmark')),
        headline: text(section.querySelector('h1')),
        lede: text(section.querySelector('.pd-hero-lede')),
        actions: [...section.querySelectorAll('.pd-hero-actions > a')].map(link => [
          text(link),
          kind(link),
          link.getAttribute('href'),
        ]),
        install: text(section.querySelector('.pd-install > code')),
        meta: text(section.querySelector('.pd-hero-meta')),
      }
    })

    assert.equal(hero.mark, '', 'the icon mark is decorative, so its alt is empty')
    assert.equal(hero.wordmark, 'Persistord')
    assert.equal(hero.headline, 'Every Discord bot rewrites the same tables. Persistord ships them.')
    assert.equal(
      hero.lede,
      'An EF Core 10 model for guilds, channels, users, members, roles and messages that never picks your database provider or your Discord library.'
    )
    assert.deepEqual(hero.actions, [
      ['Get started', 'primary', 'articles/getting-started.html'],
      ['Browse packages', 'outline', 'articles/packages.html'],
      ['GitHub', 'text', 'https://github.com/HandyS11/Persistord'],
    ])
    assert.equal(hero.install, 'dotnet add package Persistord')
    assert.equal(hero.meta, 'EF Core 10 · PostgreSQL · SQL Server · SQLite · Discord.Net · DSharpPlus · NetCord')
  } finally {
    await close()
  }
})

test('the hero lede fits two lines at desktop width, and the stat grid is gone', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const lines = await lineCount(page, '.pd-hero-lede')
    assert.ok(lines <= 2, `the lede wraps to ${lines} lines`)
    assert.equal(await page.locator('.pd-stats').count(), 0)
  } finally {
    await close()
  }
})

for (const theme of THEMES) {
  test(`${theme}: the wordmark is text painted with the gradient ink`, async () => {
    const { page, close } = await site.open(LANDING, { theme })
    try {
      const style = await computed(page, '.pd-wordmark', ['background-image', 'background-clip', 'color'])
      const { 'gradient-ink': ink } = await tokens(page, ['gradient-ink'])
      const stops = value => (value.match(/rgba?\([^)]*\)|#[0-9a-f]{6}/gi) ?? []).map(parseColor)

      const painted = stops(style['background-image'])
      const expected = stops(ink)
      assert.equal(painted.length, expected.length, `wordmark background is ${style['background-image']}`)
      painted.forEach((stop, i) => assert.ok(sameColor(stop, expected[i]), `stop ${i} is ${style['background-image']}`))
      assert.equal(style['background-clip'], 'text')
      assert.equal(parseColor(style.color)[3], 0, `wordmark colour is ${style.color}`)
    } finally {
      await close()
    }
  })
}
```

In `docs/tools/site-check/tests/tokens.test.mjs`, replace the `GRADIENT_USES` set with:

```js
const GRADIENT_USES = new Set([
  '#navbar .navbar-nav .nav-link.active::after',
  '.toc li.active:not(:has(li.active)) > a::before',
  '.pd-btn-primary',
  '.pd-wordmark',
])
```

In `docs/tools/site-check/tests/infra.test.mjs`, replace the `DESCRIBED_BY_FRONT_MATTER` line with:

```js
const DESCRIBED_BY_FRONT_MATTER = ['index.html', 'development/index.html', 'development/components.html']
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:

```bash
cd docs/tools/site-check && npm run build:fast && node --test tests/landing.test.mjs tests/infra.test.mjs tests/tokens.test.mjs
```

(If `npm run build:fast` fails because `docs/api/*.yml` is missing, run `npm run build` once.)

Expected: the hero test fails (`hero.mark` is `undefined`); the lede test fails (`the lede wraps to 3 lines` or more); both wordmark tests throw (no `.pd-wordmark`); `described pages carry exactly one meta description` fails for `index.html` (0). Token tests pass.

- [ ] **Step 4: Rewrite the front matter and hero**

In `docs/index.md`, replace everything from the first line through the closing `</section>` of `.pd-hero` (currently lines 1–14) with:

```html
---
title: Persistord
layout: landing
description: Persistord ships the EF Core 10 model every Discord bot rewrites — guilds, channels, users, members, roles and messages — without choosing your database provider or your Discord library.
---

<section class="pd-hero">
  <p class="pd-eyebrow"><img src="icon.png" alt="" width="28" height="28"><span class="pd-wordmark">Persistord</span></p>
  <h1>Every Discord bot rewrites the same tables. Persistord ships them.</h1>
  <p class="pd-hero-lede">An EF Core 10 model for guilds, channels, users, members, roles and messages that never picks your database provider or your Discord library.</p>
  <div class="pd-hero-actions">
    <a class="pd-btn pd-btn-primary" href="articles/getting-started.md">Get started</a>
    <a class="pd-btn" href="articles/packages.md">Browse packages</a>
    <a class="pd-text-link" href="https://github.com/HandyS11/Persistord">GitHub</a>
  </div>
  <div class="pd-install"><span class="pd-prompt">$</span><code>dotnet add package Persistord</code><button class="pd-copy" type="button" aria-live="polite">Copy</button></div>
  <p class="pd-hero-meta">EF Core 10 · PostgreSQL · SQL Server · SQLite · Discord.Net · DSharpPlus · NetCord</p>
</section>
```

Then delete the old install strip (`<div class="pd-install">…</div>` after the transform section) and the whole `<dl class="pd-stats">…</dl>` block, including the blank lines around them, so exactly one blank line separates the transform section from the "Ten packages" section.

- [ ] **Step 5: Restyle the hero**

In `docs/templates/persistord/public/css/landing.css`:

1. Replace the header comment (lines 1–6) with:

```css
/*
 * Landing page (docs/index.md, `layout: landing`): hero, proof panes, model
 * diagram, adapter picker, capability list, and start-here cards. The
 * .pd-snowflake / .pd-row-id marks are the only place the gateway-vs-column
 * colour rule survives, and only inside the proof panes. Every class here is
 * used by docs/index.md or main.js (tests/landing.test.mjs).
 */
```

2. Replace the whole `hero` block — from its `/* --- hero -- */` banner up to (not including) the `buttons` banner — with:

```css
/* ------------------------------------------------------------------ hero -- */

.pd-hero {
  padding: 3.5rem 0 3.25rem;
}

.pd-eyebrow {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin: 0 0 1.25rem;
  font-size: 1.05rem;
  font-weight: 700;
  letter-spacing: -0.01em;
  line-height: 1;
}

/* The icon is a mark, not a figure: components.css frames lone images. */
.pd-eyebrow > img {
  width: 28px;
  height: 28px;
  border: 0;
  border-radius: var(--pd-radius-sm);
}

/* One of the gradient's sanctioned uses (main.css header). Every
   --pd-gradient-ink stop holds 4.5:1 on its theme's page. */
.pd-wordmark {
  background-image: var(--pd-gradient-ink);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}

.pd-hero h1 {
  margin: 0;
  max-width: 24ch;
  font-size: clamp(2rem, 1.1rem + 3.2vw, 3.25rem);
  line-height: 1.08;
}

.pd-hero-lede {
  margin: 1.25rem 0 0;
  max-width: 66ch;
  color: var(--pd-muted);
  font-size: 1.15rem;
  line-height: 1.6;
}

.pd-hero-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.7rem;
  margin-top: 1.9rem;
}

.pd-text-link {
  padding: 0.6rem 0.35rem;
  color: var(--pd-accent);
  font-size: 0.96rem;
  font-weight: 600;
  text-decoration: none;
}

.pd-text-link:hover {
  color: var(--pd-accent-hover);
  text-decoration: underline;
}

.pd-hero .pd-install {
  margin-top: 1.75rem;
}

.pd-hero-meta {
  margin: 1rem 0 0;
  color: var(--pd-muted);
  font-size: 0.875rem;
}
```

3. In the `.pd-install` rule, change `margin: 0 0 2.6rem;` to `margin: 0;`.

4. Delete the whole `stats` block (its banner, `.pd-stats`, `.pd-stat`, `.pd-stat dt`, `.pd-stat dd`).

5. In the shared rule that drops the inline-code tint, delete the `.pd-hero-lede code,` selector line.

- [ ] **Step 6: Record the wordmark in the gradient budget**

In `docs/templates/persistord/public/main.css`, replace the gradient-budget paragraph of the header comment with:

```css
 * Gradient budget: --pd-gradient-* has three uses - the active-page indicator
 * (navbar underline and sidebar bar), the primary button, and the landing
 * wordmark (.pd-wordmark). tests/tokens.test.mjs enforces the list of
 * consuming selectors.
```

- [ ] **Step 7: Rebuild and run the tests to verify they pass**

Run:

```bash
cd docs/tools/site-check && npm run build:fast && node --test tests/landing.test.mjs tests/infra.test.mjs tests/tokens.test.mjs tests/chrome.test.mjs tests/keyboard.test.mjs
```

Expected: build exits 0 with `0 warning(s)`; all tests pass. If the lede test reports 3 lines, reduce `.pd-hero-lede` `max-width` only if the text is being squeezed; the rule is ≤ 2 lines inside the 1120px column, so widen `max-width` (up to `72ch`) rather than shrinking the font.

- [ ] **Step 8: Lint and commit**

```bash
npx --no-install markdownlint-cli2 docs/index.md
git add docs/index.md docs/templates/persistord/public/css/landing.css docs/templates/persistord/public/main.css docs/tools/site-check/tests/landing.test.mjs docs/tools/site-check/tests/tokens.test.mjs docs/tools/site-check/tests/infra.test.mjs
git commit -m "docs: landing hero with gradient wordmark, install strip and meta line

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Proof section — panes, connectors, two-line caption

**Files:**

- Modify: `docs/index.md`, `docs/templates/persistord/public/css/landing.css`, `docs/tools/site-check/tests/landing.test.mjs`

**Interfaces:**

- Consumes: `site`, `LANDING`, `lineCount` (Task 1).
- Produces: `section.pd-section.pd-transform` containing `.pd-panes` → `.pd-pane`, `.pd-connector`, `.pd-pane`, `.pd-connector`, `.pd-pane`, followed by `p.pd-caption`. `.pd-transform .pd-snowflake` is kept, because `tests/keyboard.test.mjs` checks its reduced-motion gating.

- [ ] **Step 1: Write the failing proof tests**

Append to `docs/tools/site-check/tests/landing.test.mjs`:

```js
test('the proof shows one snowflake, unchanged, from gateway payload to column', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const proof = await page.$eval('.pd-transform', section => ({
      panes: section.querySelectorAll('.pd-panes > .pd-pane').length,
      connectors: section.querySelectorAll('.pd-panes > .pd-connector[aria-hidden="true"]').length,
      snowflake: section.querySelector('.pd-snowflake')?.textContent.trim(),
      rowId: section.querySelector('.pd-row-id')?.textContent.trim(),
      link: section.querySelector('.pd-caption a')?.getAttribute('href'),
      notes: section.querySelectorAll('.pd-note').length,
      text: section.textContent.replace(/\s+/g, ' '),
    }))

    assert.equal(proof.panes, 3)
    assert.equal(proof.connectors, 2)
    assert.ok(proof.snowflake, 'the gateway pane marks its snowflake')
    assert.equal(proof.rowId, proof.snowflake, 'the id must never be shown changing value')
    assert.equal(proof.link, 'articles/snowflake-conversion.html')
    assert.equal(proof.notes, 0, 'the long explanation is replaced by the caption')

    /* Claims the spec forbids (spec 3.2, previous spec 3.2): a sign flip, a
       Steam64 example, an over-broad provider claim, and "won't build". */
    for (const forbidden of [
      /-\d{15,}/,
      /steam/i,
      /no relational (provider|database)/i,
      /\b(cannot|can't|won't|will not|does not|doesn't) (map|build|compile)\b/i,
      /\bat all\b/i,
    ]) {
      assert.doesNotMatch(proof.text, forbidden)
    }

    const lines = await lineCount(page, '.pd-caption')
    assert.ok(lines <= 2, `the caption wraps to ${lines} lines`)
  } finally {
    await close()
  }
})

test('the proof panes read left to right on desktop and stack with downward connectors on a phone', async () => {
  const layout = async width => {
    const { page, close } = await site.open(LANDING, { width })
    try {
      return await page.$eval('.pd-panes', panes => ({
        tops: [...panes.querySelectorAll('.pd-pane')].map(pane => Math.round(pane.getBoundingClientRect().top)),
        lefts: [...panes.querySelectorAll('.pd-pane')].map(pane => Math.round(pane.getBoundingClientRect().left)),
        turns: [...panes.querySelectorAll('.pd-connector')].map(connector => {
          const match = /matrix\(([^)]+)\)/.exec(getComputedStyle(connector).transform)
          if (!match) {
            return 0
          }
          const [a, b] = match[1].split(',').map(Number)
          return Math.round((Math.atan2(b, a) * 180) / Math.PI)
        }),
      }))
    } finally {
      await close()
    }
  }

  const desktop = await layout(1440)
  assert.equal(new Set(desktop.tops).size, 1, `desktop pane tops: ${desktop.tops}`)
  assert.deepEqual(desktop.turns, [0, 0])

  const phone = await layout(375)
  assert.ok(phone.tops[0] < phone.tops[1] && phone.tops[1] < phone.tops[2], `phone pane tops: ${phone.tops}`)
  assert.equal(new Set(phone.lefts).size, 1, `phone pane lefts: ${phone.lefts}`)
  assert.deepEqual(phone.turns, [90, 90])
})
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
cd docs/tools/site-check && node --test tests/landing.test.mjs
```

Expected: Task 1's tests pass; `the proof shows one snowflake…` fails on `proof.connectors` (0); the layout test fails on `desktop.turns` (`[]` vs `[0, 0]`).

- [ ] **Step 3: Rewrite the proof section**

In `docs/index.md`, replace the whole `<section class="pd-section pd-transform">…</section>` with:

```html
<section class="pd-section pd-transform">
  <h2>A gateway payload, a mapper call, a row</h2>
  <div class="pd-panes">
    <figure class="pd-pane">
      <figcaption class="pd-pane-bar">1 · gateway event</figcaption>
      <pre>{
  "id": "<span class="pd-snowflake">1547396186112000000</span>",
  "channel_id": "1547300000000000000",
  "content": "gg"
}</pre>
    </figure>
    <span class="pd-connector" aria-hidden="true">→</span>
    <figure class="pd-pane">
      <figcaption class="pd-pane-bar">2 · .ToMessageEntity()</figcaption>
      <pre>// Persistord.Adapters.DiscordNet
var row = message.ToMessageEntity();
db.Messages.Add(row);
await db.SaveChangesAsync();</pre>
    </figure>
    <span class="pd-connector" aria-hidden="true">→</span>
    <figure class="pd-pane">
      <figcaption class="pd-pane-bar">3 · messages row</figcaption>
      <pre>column     type     value
---------  -------  -------------------
Id         BIGINT   <span class="pd-row-id">1547396186112000000</span>
ChannelId  BIGINT   1547300000000000000
Content    TEXT     'gg'</pre>
    </figure>
  </div>
  <p class="pd-caption">Discord ids are unsigned 64-bit integers, and PostgreSQL and SQL Server have no native unsigned 64-bit type. <code>DiscordDbContext</code> converts every <code>ulong</code> to a signed <code>BIGINT</code>: the same 64 bits go in and come back out, and no id is ever annotated. <a href="articles/snowflake-conversion.md">How the conversion works<span aria-hidden="true"> →</span></a></p>
</section>
```

(The pane contents are unchanged from PR 1. The `<pre>` continuation lines stay at column 0.)

- [ ] **Step 4: Restyle the proof section**

In `docs/templates/persistord/public/css/landing.css`, replace the `.pd-panes` rule and its `@media (max-width: 900px)` block (the first two rules of the `transform` block) with:

```css
.pd-panes {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr) auto minmax(0, 1fr);
  gap: 0.6rem;
}

/* Decorative: the pane order already reads as a sequence. */
.pd-connector {
  align-self: center;
  color: var(--pd-muted);
  font-size: 1.25rem;
  line-height: 1;
  text-align: center;
}

@media (max-width: 900px) {
  .pd-panes {
    grid-template-columns: minmax(0, 1fr);
  }

  .pd-connector {
    transform: rotate(90deg);
  }
}
```

Then add, directly after the `@keyframes pd-carry` block:

```css
.pd-section > .pd-caption {
  margin: 1rem 0 0;
  max-width: none;
  color: var(--pd-muted);
  font-size: 0.95rem;
  line-height: 1.65;
}

.pd-caption a {
  font-weight: 600;
  text-decoration: none;
  white-space: nowrap;
}

.pd-caption a:hover {
  text-decoration: underline;
}
```

Finally, in the shared rule that drops the inline-code tint, add `.pd-caption code,` as its first selector.

- [ ] **Step 5: Rebuild and run the tests to verify they pass**

Run:

```bash
cd docs/tools/site-check && npm run build:fast && node --test tests/landing.test.mjs tests/keyboard.test.mjs tests/tokens.test.mjs
```

Expected: `0 warning(s)`; all tests pass, including `the landing page animates only when motion is allowed`.

- [ ] **Step 6: Lint and commit**

```bash
npx --no-install markdownlint-cli2 docs/index.md
git add docs/index.md docs/templates/persistord/public/css/landing.css docs/tools/site-check/tests/landing.test.mjs
git commit -m "docs: landing proof panes with connectors and a two-line caption

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: The model diagram, verified against source

**Files:**

- Create: `docs/tools/site-check/tests/model.test.mjs`
- Modify: `docs/index.md`, `docs/templates/persistord/public/css/landing.css`

**Interfaces:**

- Consumes: `launchSite`, `computed`, `tokens`, `THEMES` (PR 1 `lib/site.mjs`); `parseColor`, `sameColor` (PR 1 `lib/color.mjs`).
- Produces: `section.pd-section.pd-model` holding `figure.pd-model-figure` → `div.pd-model-scroll` → `svg.pd-model-svg`, plus `figcaption.pd-model-legend`. Inside the SVG, each `g.pd-model-group[data-package]` holds a `rect.pd-model-frame`, a `text.pd-model-package`, and `g.pd-model-node[data-node]` elements, each a `rect` + `text.pd-model-name` + `text.pd-model-cols`. The edges are `path.pd-model-edge[data-kind="fk"|"id"][data-from][data-to][data-column]`, and the arrowhead is `marker#pd-model-arrow > path.pd-model-head`.

**Source facts this task draws** (verified 2026-09-15; the tests re-verify them on every run):

| Kind | Edge | Proven by |
| --- | --- | --- |
| fk | `ChannelEntity.ParentId → ChannelEntity` | `ChannelEntityConfiguration`: `HasOne<ChannelEntity>().WithMany().HasForeignKey(c => c.ParentId)` (Restrict) |
| fk | `Embed.MessageId → MessageEntity` | `MessageEntityConfiguration`: `HasMany(m => m.Embeds)…HasForeignKey(e => e.MessageId)` |
| fk | `AttachmentEntity.MessageId → MessageEntity` | `MessageEntityConfiguration`: `HasMany(m => m.Attachments)…HasForeignKey(a => a.MessageId)` |
| fk | `ReactionEntity.MessageId → MessageEntity` | `MessageEntityConfiguration`: `HasMany(m => m.Reactions)…HasForeignKey(r => r.MessageId)` |
| fk | `EmbedField.EmbedId → Embed` | `EmbedEntityConfiguration`: `HasMany(e => e.Fields)…HasForeignKey(f => f.EmbedId)` |
| fk | `MessageHistoryEntity.MessageId → MessageEntity` | `MessageHistoryEntityConfiguration`: `HasOne<MessageEntity>().WithMany().HasForeignKey(h => h.MessageId)` (Restrict) |
| id | `GuildEntity.OwnerId → UserEntity` | `ulong? OwnerId`; no foreign key configured |
| id | `MemberEntity.UserId → UserEntity` | part of the `(GuildId, UserId)` key; no foreign key |
| id | `MemberEntity.GuildId → GuildEntity` | part of the key; no foreign key |
| id | `RoleEntity.GuildId → GuildEntity` | `HasIndex(r => r.GuildId)`; no foreign key |
| id | `ChannelEntity.GuildId → GuildEntity` | `HasIndex(c => c.GuildId)`; no foreign key |
| id | `MessageEntity.ChannelId → ChannelEntity` | `HasIndex(m => new { m.ChannelId, m.Id })`; no foreign key |
| id | `MessageEntity.AuthorId → UserEntity` | `ulong AuthorId`; no foreign key |

`Embed` additionally owns `EmbedFooter` and `EmbedAuthor` (`OwnsOne`), shown as a line of text inside its node.

- [ ] **Step 1: Write the failing model tests**

`docs/tools/site-check/tests/model.test.mjs`:

```js
import assert from 'node:assert/strict'
import { readdir, readFile } from 'node:fs/promises'
import { after, before, test } from 'node:test'
import { parseColor, sameColor } from '../lib/color.mjs'
import { THEMES, computed, launchSite, tokens } from '../lib/site.mjs'

const LANDING = 'index.html'
const SVG = '.pd-model-svg'
const REPO = new URL('../../../../', import.meta.url)
const PACKAGES = ['Persistord.Core', 'Persistord.Messages', 'Persistord.History']

/* Whitespace-insensitive source, so a fluent chain split over lines matches. */
const flatten = text => text.replace(/\s+/g, ' ')

/*
 * Every edge the landing diagram draws. `fk`: a foreign key the model
 * configures, proven by the `evidence` statements ([repo path, statement]).
 * `id`: a snowflake column that refers to another entity with no foreign key.
 * Arrows run from the entity holding the column to the entity it refers to.
 */
const EDGES = [
  {
    kind: 'fk', from: 'ChannelEntity', to: 'ChannelEntity', column: 'ParentId',
    evidence: [
      ['src/Persistord.Core/Configurations/ChannelEntityConfiguration.cs', 'builder.HasOne<ChannelEntity>() .WithMany() .HasForeignKey(c => c.ParentId)'],
    ],
  },
  {
    kind: 'fk', from: 'Embed', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.Messages/Entities/MessageEntity.cs', 'public List<Embed> Embeds'],
      ['src/Persistord.Messages/Configurations/MessageEntityConfiguration.cs', 'builder.HasMany(m => m.Embeds).WithOne().HasForeignKey(e => e.MessageId);'],
    ],
  },
  {
    kind: 'fk', from: 'AttachmentEntity', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.Messages/Entities/MessageEntity.cs', 'public List<AttachmentEntity> Attachments'],
      ['src/Persistord.Messages/Configurations/MessageEntityConfiguration.cs', 'builder.HasMany(m => m.Attachments).WithOne().HasForeignKey(a => a.MessageId);'],
    ],
  },
  {
    kind: 'fk', from: 'ReactionEntity', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.Messages/Entities/MessageEntity.cs', 'public List<ReactionEntity> Reactions'],
      ['src/Persistord.Messages/Configurations/MessageEntityConfiguration.cs', 'builder.HasMany(m => m.Reactions).WithOne().HasForeignKey(r => r.MessageId);'],
    ],
  },
  {
    kind: 'fk', from: 'EmbedField', to: 'Embed', column: 'EmbedId',
    evidence: [
      ['src/Persistord.Messages/Owned/Embed.cs', 'public List<EmbedField> Fields'],
      ['src/Persistord.Messages/Configurations/EmbedEntityConfiguration.cs', 'builder.HasMany(e => e.Fields).WithOne().HasForeignKey(f => f.EmbedId);'],
    ],
  },
  {
    kind: 'fk', from: 'MessageHistoryEntity', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.History/Configurations/MessageHistoryEntityConfiguration.cs', 'builder.HasOne<MessageEntity>() .WithMany() .HasForeignKey(h => h.MessageId)'],
    ],
  },
  { kind: 'id', from: 'GuildEntity', to: 'UserEntity', column: 'OwnerId' },
  { kind: 'id', from: 'MemberEntity', to: 'UserEntity', column: 'UserId' },
  { kind: 'id', from: 'MemberEntity', to: 'GuildEntity', column: 'GuildId' },
  { kind: 'id', from: 'RoleEntity', to: 'GuildEntity', column: 'GuildId' },
  { kind: 'id', from: 'ChannelEntity', to: 'GuildEntity', column: 'GuildId' },
  { kind: 'id', from: 'MessageEntity', to: 'ChannelEntity', column: 'ChannelId' },
  { kind: 'id', from: 'MessageEntity', to: 'UserEntity', column: 'AuthorId' },
]

const NODE_NAMES = [...new Set(EDGES.flatMap(edge => [edge.from, edge.to]))]
const edgeKey = ({ kind, from, column, to }) => `${kind} ${from}.${column} -> ${to}`

/** Every class declared in the three model packages: package, flattened source, base list. */
async function modelClasses() {
  const found = new Map()

  async function walk(directory, packageName) {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      if (entry.name === 'bin' || entry.name === 'obj') {
        continue
      }
      if (entry.isDirectory()) {
        await walk(new URL(`${entry.name}/`, directory), packageName)
        continue
      }
      if (!entry.name.endsWith('.cs')) {
        continue
      }
      const source = flatten(await readFile(new URL(entry.name, directory), 'utf8'))
      const declarations = source.matchAll(/\bpublic (?:sealed |abstract |static )*class (\w+)(?:\([^)]*\))? ?(:[^{]*)?\{/g)
      for (const [, name, bases] of declarations) {
        found.set(name, { packageName, source, bases: bases?.trim() ?? '' })
      }
    }
  }

  for (const packageName of PACKAGES) {
    await walk(new URL(`src/${packageName}/`, REPO), packageName)
  }
  return found
}

let site
let classes

before(async () => {
  site = await launchSite()
  classes = await modelClasses()
})

after(async () => {
  await site.close()
})

test('the diagram draws exactly the recorded nodes, packages and edges', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const drawn = await page.$eval(SVG, svg => ({
      nodes: [...svg.querySelectorAll('.pd-model-node')].map(node => [
        node.dataset.node,
        node.closest('[data-package]')?.dataset.package,
      ]),
      edges: [...svg.querySelectorAll('.pd-model-edge')].map(({ dataset }) => ({ ...dataset })),
    }))

    assert.deepEqual(drawn.edges.map(edgeKey).toSorted(), EDGES.map(edgeKey).toSorted())
    assert.deepEqual(drawn.nodes.map(([name]) => name).toSorted(), NODE_NAMES.toSorted())
    for (const [name, packageName] of drawn.nodes) {
      assert.ok(classes.has(name), `${name} is not a class in ${PACKAGES.join(', ')}`)
      assert.equal(packageName, classes.get(name).packageName, `${name} is drawn inside the wrong package`)
    }
  } finally {
    await close()
  }
})

test('every solid edge is a foreign key the configurations declare', async () => {
  for (const edge of EDGES.filter(({ kind }) => kind === 'fk')) {
    for (const [path, statement] of edge.evidence) {
      const source = flatten(await readFile(new URL(path, REPO), 'utf8'))
      assert.ok(source.includes(flatten(statement)), `${edge.from}.${edge.column}: "${statement}" is not in ${path}`)
    }
  }
})

test('every dashed edge is a snowflake column with no foreign key behind it', () => {
  const everything = [...new Set([...classes.values()].map(({ source }) => source))].join('\n')
  for (const edge of EDGES.filter(({ kind }) => kind === 'id')) {
    const label = `${edge.from}.${edge.column}`
    const entity = classes.get(edge.from)
    assert.match(entity.source, new RegExp(`public ulong\\?? ${edge.column} \\{`), `${label} is not a ulong column`)
    assert.doesNotMatch(
      everything,
      new RegExp(`HasForeignKey\\( ?\\w+ => \\w+\\.${edge.column} ?\\)`),
      `${label} now has a configured foreign key - redraw it solid`
    )
    /* ApplyGuildRoot keys only IGuildScoped types; a base list could add one. */
    assert.equal(entity.bases, '', `${edge.from} now derives from "${entity.bases}" - re-check ApplyGuildRoot`)
  }
})

test('every reference column on a drawn entity is drawn', () => {
  const drawn = new Set(EDGES.map(({ from, column }) => `${from}.${column}`))
  for (const name of NODE_NAMES) {
    for (const [, column] of classes.get(name).source.matchAll(/public u?long\?? (\w+Id) \{/g)) {
      assert.ok(drawn.has(`${name}.${column}`), `${name}.${column} refers to another entity but is not drawn`)
    }
  }
})

test('Embed shows the two types it owns', async () => {
  const configuration = classes.get('EmbedEntityConfiguration').source
  assert.ok(configuration.includes('builder.OwnsOne(e => e.Footer);'), 'Embed no longer owns its footer')
  assert.ok(configuration.includes('builder.OwnsOne(e => e.Author);'), 'Embed no longer owns its author')

  const { page, close } = await site.open(LANDING)
  try {
    const text = await page.$eval(`${SVG} [data-node="Embed"]`, node => node.textContent)
    assert.match(text, /Footer/)
    assert.match(text, /Author/)
  } finally {
    await close()
  }
})

test('nodes do not overlap, labels fit their boxes, and edges join only their own nodes', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const problems = await page.$eval(SVG, svg => {
      const box = element => {
        const b = element.getBBox()
        return { left: b.x, top: b.y, right: b.x + b.width, bottom: b.y + b.height }
      }
      const inside = (point, rect, inset) =>
        point.x > rect.left + inset && point.x < rect.right - inset && point.y > rect.top + inset && point.y < rect.bottom - inset
      const near = (point, rect, slack) =>
        point.x >= rect.left - slack && point.x <= rect.right + slack && point.y >= rect.top - slack && point.y <= rect.bottom + slack

      const found = []
      const nodes = [...svg.querySelectorAll('.pd-model-node')].map(node => ({
        name: node.dataset.node,
        rect: box(node.querySelector('rect')),
        texts: [...node.querySelectorAll('text')].map(box),
      }))
      const labels = [...svg.querySelectorAll('.pd-model-package')].map(box)

      for (const node of nodes) {
        for (const text of node.texts) {
          if (text.left < node.rect.left + 4 || text.right > node.rect.right - 4 || text.top < node.rect.top || text.bottom > node.rect.bottom) {
            found.push(`${node.name}: a label overflows its box`)
          }
        }
      }

      nodes.forEach((a, i) => {
        for (const b of nodes.slice(i + 1)) {
          if (a.rect.left < b.rect.right && b.rect.left < a.rect.right && a.rect.top < b.rect.bottom && b.rect.top < a.rect.bottom) {
            found.push(`${a.name} overlaps ${b.name}`)
          }
        }
      })

      const byName = name => nodes.find(node => node.name === name)
      for (const path of svg.querySelectorAll('.pd-model-edge')) {
        const { from, to, column } = path.dataset
        const label = `${from}.${column} -> ${to}`
        const length = path.getTotalLength()
        if (!near(path.getPointAtLength(0), byName(from).rect, 1.5)) {
          found.push(`${label}: does not start on ${from}`)
        }
        if (!near(path.getPointAtLength(length), byName(to).rect, 1.5)) {
          found.push(`${label}: does not end on ${to}`)
        }
        for (let distance = 0; distance <= length; distance += 4) {
          const point = path.getPointAtLength(distance)
          for (const node of nodes) {
            if (node.name !== from && node.name !== to && inside(point, node.rect, 1)) {
              found.push(`${label}: crosses ${node.name}`)
            }
          }
          if (labels.some(rect => inside(point, rect, 0))) {
            found.push(`${label}: crosses a package label`)
          }
        }
      }
      return [...new Set(found)]
    })
    assert.deepEqual(problems, [])
  } finally {
    await close()
  }
})

test('the diagram has an accessible name and a description naming every entity', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const a11y = await page.$eval(SVG, svg => {
      const [title, desc] = (svg.getAttribute('aria-labelledby') ?? '').split(/\s+/).map(id => document.getElementById(id))
      return {
        role: svg.getAttribute('role'),
        title: title?.tagName.toLowerCase() === 'title' ? title.textContent.trim() : null,
        desc: desc?.tagName.toLowerCase() === 'desc' ? desc.textContent.replace(/\s+/g, ' ') : '',
        viewBoxWidth: svg.viewBox.baseVal.width,
      }
    })
    assert.equal(a11y.role, 'img')
    assert.ok(a11y.title, 'aria-labelledby does not start with the <title>')
    for (const name of NODE_NAMES) {
      assert.match(a11y.desc, new RegExp(`\\b${name}\\b`), `the <desc> never mentions ${name}`)
    }
    assert.equal(a11y.viewBoxWidth, 1120, 'viewBox did not survive the build (check its casing in the DOM)')
  } finally {
    await close()
  }
})

for (const theme of THEMES) {
  test(`${theme}: the diagram takes every colour from the tokens`, async () => {
    const { page, close } = await site.open(LANDING, { theme })
    try {
      const values = await tokens(page, ['surface', 'hairline', 'raised', 'control-border', 'text', 'muted', 'accent'])
      const expectations = [
        ['.pd-model-frame', 'fill', 'surface'],
        ['.pd-model-frame', 'stroke', 'hairline'],
        ['.pd-model-package', 'fill', 'accent'],
        ['.pd-model-node > rect', 'fill', 'raised'],
        ['.pd-model-node > rect', 'stroke', 'control-border'],
        ['.pd-model-name', 'fill', 'text'],
        ['.pd-model-cols', 'fill', 'muted'],
        ['.pd-model-edge', 'stroke', 'muted'],
        ['.pd-model-head', 'fill', 'muted'],
      ]
      for (const [selector, property, token] of expectations) {
        const value = (await computed(page, `${SVG} ${selector}`, [property]))[property]
        assert.ok(sameColor(parseColor(value), parseColor(values[token])), `${theme}: ${selector} ${property} is ${value}, not --pd-${token}`)
      }

      const dash = async kind => (await computed(page, `${SVG} .pd-model-edge[data-kind="${kind}"]`, ['stroke-dasharray']))['stroke-dasharray']
      assert.equal(await dash('fk'), 'none')
      assert.notEqual(await dash('id'), 'none')

      const painted = await page.locator(`${SVG} [fill], ${SVG} [stroke], ${SVG} [style], ${SVG} [color]`).count()
      assert.equal(painted, 0, 'the SVG hard-codes a colour attribute')
    } finally {
      await close()
    }
  })
}

test('the diagram fills the column on desktop and scrolls inside its figure on a phone', async () => {
  const desktop = await site.open(LANDING, { width: 1440 })
  try {
    const box = await desktop.page.$eval('.pd-model-scroll', scroll => ({ scroll: scroll.scrollWidth, client: scroll.clientWidth }))
    assert.ok(box.scroll <= box.client, `the desktop diagram scrolls: ${box.scroll} > ${box.client}`)
  } finally {
    await desktop.close()
  }

  const phone = await site.open(LANDING, { width: 375 })
  try {
    const box = await phone.page.evaluate(() => {
      const scroll = document.querySelector('.pd-model-scroll')
      return {
        page: document.documentElement.scrollWidth - document.documentElement.clientWidth,
        scrolls: scroll.scrollWidth > scroll.clientWidth,
        svg: document.querySelector('.pd-model-svg').getBoundingClientRect().width,
      }
    })
    assert.equal(box.page, 0, 'the diagram pushes the page wider than the viewport')
    assert.ok(box.scrolls, 'the diagram does not scroll inside its figure')
    assert.ok(box.svg >= 880, `the diagram shrank to ${box.svg}px`)
  } finally {
    await phone.close()
  }
})
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
cd docs/tools/site-check && node --test tests/model.test.mjs
```

Expected: the three source-only tests (`every solid edge…`, `every dashed edge…`, `every reference column…`) PASS, which proves the table matches the library before any drawing exists. Every browser test FAILS, because `.pd-model-svg` does not exist yet (`page.$eval: Failed to find element`). If a source-only test fails, the library has changed since this plan was written: stop, re-read the configuration it names, and update both the facts table above and `EDGES` before drawing.

- [ ] **Step 3: Draw the model section**

In `docs/index.md`, insert this directly after the closing `</section>` of `.pd-transform`, separated from it and from the next section by one blank line. The block must contain **no blank lines**:

```html
<section class="pd-section pd-model">
  <h2>The model you'd have written</h2>
  <p>The tables <a href="../src/Persistord.Core/README.md"><code>Persistord.Core</code></a>, <a href="../src/Persistord.Messages/README.md"><code>Persistord.Messages</code></a> and <a href="../src/Persistord.History/README.md"><code>Persistord.History</code></a> map, and every column that points from one to another.</p>
  <figure class="pd-model-figure">
    <div class="pd-model-scroll" role="region" aria-label="Entity model diagram" tabindex="0">
      <svg class="pd-model-svg" viewBox="0 0 1120 590" role="img" aria-labelledby="pd-model-title pd-model-desc">
        <title id="pd-model-title">The Persistord entity model</title>
        <desc id="pd-model-desc">Persistord.Core maps GuildEntity, UserEntity, MemberEntity, RoleEntity and ChannelEntity. Its only foreign key is ChannelEntity.ParentId, which points at a parent ChannelEntity. ChannelEntity.GuildId, RoleEntity.GuildId and MemberEntity.GuildId refer to GuildEntity, MemberEntity.UserId and GuildEntity.OwnerId refer to UserEntity, and none of them is a foreign key. Persistord.Messages maps MessageEntity. Embed, AttachmentEntity and ReactionEntity each have a MessageId foreign key to MessageEntity, and EmbedField has an EmbedId foreign key to Embed, which also owns a footer and an author. MessageEntity.ChannelId refers to ChannelEntity and MessageEntity.AuthorId to UserEntity, without foreign keys. Persistord.History maps MessageHistoryEntity, whose MessageId is a foreign key to MessageEntity.</desc>
        <defs>
          <marker id="pd-model-arrow" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="9" markerHeight="9" markerUnits="userSpaceOnUse" orient="auto"><path class="pd-model-head" d="M0 0 L10 5 L0 10 z"/></marker>
        </defs>
        <g class="pd-model-group" data-package="Persistord.Core">
          <rect class="pd-model-frame" x="1" y="40" width="518" height="400" rx="12"/>
          <text class="pd-model-package" x="21" y="68">Persistord.Core</text>
          <g class="pd-model-node" data-node="UserEntity"><rect x="40" y="96" width="180" height="60" rx="8"/><text class="pd-model-name" x="130" y="122">UserEntity</text><text class="pd-model-cols" x="130" y="142">Id</text></g>
          <g class="pd-model-node" data-node="GuildEntity"><rect x="300" y="96" width="180" height="60" rx="8"/><text class="pd-model-name" x="390" y="122">GuildEntity</text><text class="pd-model-cols" x="390" y="142">Id · OwnerId</text></g>
          <g class="pd-model-node" data-node="MemberEntity"><rect x="40" y="250" width="155" height="60" rx="8"/><text class="pd-model-name" x="117.5" y="276">MemberEntity</text><text class="pd-model-cols" x="117.5" y="296">key (GuildId, UserId)</text></g>
          <g class="pd-model-node" data-node="RoleEntity"><rect x="215" y="250" width="125" height="60" rx="8"/><text class="pd-model-name" x="277.5" y="276">RoleEntity</text><text class="pd-model-cols" x="277.5" y="296">GuildId</text></g>
          <g class="pd-model-node" data-node="ChannelEntity"><rect x="360" y="250" width="140" height="60" rx="8"/><text class="pd-model-name" x="430" y="276">ChannelEntity</text><text class="pd-model-cols" x="430" y="296">GuildId · ParentId</text></g>
        </g>
        <g class="pd-model-group" data-package="Persistord.Messages">
          <rect class="pd-model-frame" x="560" y="40" width="559" height="400" rx="12"/>
          <text class="pd-model-package" x="580" y="68">Persistord.Messages</text>
          <g class="pd-model-node" data-node="MessageEntity"><rect x="600" y="96" width="220" height="60" rx="8"/><text class="pd-model-name" x="710" y="122">MessageEntity</text><text class="pd-model-cols" x="710" y="142">ChannelId · AuthorId</text></g>
          <g class="pd-model-node" data-node="Embed"><rect x="580" y="250" width="150" height="78" rx="8"/><text class="pd-model-name" x="655" y="276">Embed</text><text class="pd-model-cols" x="655" y="296">MessageId</text><text class="pd-model-cols" x="655" y="314">owns Footer, Author</text></g>
          <g class="pd-model-node" data-node="EmbedField"><rect x="580" y="362" width="150" height="60" rx="8"/><text class="pd-model-name" x="655" y="388">EmbedField</text><text class="pd-model-cols" x="655" y="408">EmbedId</text></g>
          <g class="pd-model-node" data-node="AttachmentEntity"><rect x="770" y="250" width="166" height="60" rx="8"/><text class="pd-model-name" x="853" y="276">AttachmentEntity</text><text class="pd-model-cols" x="853" y="296">MessageId</text></g>
          <g class="pd-model-node" data-node="ReactionEntity"><rect x="955" y="250" width="150" height="60" rx="8"/><text class="pd-model-name" x="1030" y="276">ReactionEntity</text><text class="pd-model-cols" x="1030" y="296">MessageId</text></g>
        </g>
        <g class="pd-model-group" data-package="Persistord.History">
          <rect class="pd-model-frame" x="1" y="470" width="518" height="110" rx="12"/>
          <text class="pd-model-package" x="21" y="498">Persistord.History</text>
          <g class="pd-model-node" data-node="MessageHistoryEntity"><rect x="280" y="496" width="220" height="60" rx="8"/><text class="pd-model-name" x="390" y="522">MessageHistoryEntity</text><text class="pd-model-cols" x="390" y="542">MessageId</text></g>
        </g>
        <g class="pd-model-edges">
          <path class="pd-model-edge" data-kind="fk" data-from="ChannelEntity" data-to="ChannelEntity" data-column="ParentId" d="M400 310 C400 360 460 360 460 310" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="fk" data-from="Embed" data-to="MessageEntity" data-column="MessageId" d="M655 250 V156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="fk" data-from="AttachmentEntity" data-to="MessageEntity" data-column="MessageId" d="M853 250 L790 156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="fk" data-from="ReactionEntity" data-to="MessageEntity" data-column="MessageId" d="M1030 250 L812 156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="fk" data-from="EmbedField" data-to="Embed" data-column="EmbedId" d="M655 362 V328" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="fk" data-from="MessageHistoryEntity" data-to="MessageEntity" data-column="MessageId" d="M500 526 H750 V156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="GuildEntity" data-to="UserEntity" data-column="OwnerId" d="M300 126 H220" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="MemberEntity" data-to="UserEntity" data-column="UserId" d="M100 250 V156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="MemberEntity" data-to="GuildEntity" data-column="GuildId" d="M170 250 L330 156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="RoleEntity" data-to="GuildEntity" data-column="GuildId" d="M290 250 L400 156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="ChannelEntity" data-to="GuildEntity" data-column="GuildId" d="M440 250 V156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="MessageEntity" data-to="ChannelEntity" data-column="ChannelId" d="M600 140 H540 V280 H500" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="MessageEntity" data-to="UserEntity" data-column="AuthorId" d="M790 96 V20 H180 V96" marker-end="url(#pd-model-arrow)"/>
        </g>
      </svg>
    </div>
    <figcaption class="pd-model-legend">
      <span class="pd-legend-item"><svg class="pd-legend-swatch" viewBox="0 0 36 10" aria-hidden="true"><line class="pd-model-edge" data-kind="fk" x1="0" y1="5" x2="36" y2="5"/></svg>Configured foreign key</span>
      <span class="pd-legend-item"><svg class="pd-legend-swatch" viewBox="0 0 36 10" aria-hidden="true"><line class="pd-model-edge" data-kind="id" x1="0" y1="5" x2="36" y2="5"/></svg>Snowflake id column, no foreign key</span>
      <span>Arrows point at the entity a column refers to.</span>
    </figcaption>
  </figure>
</section>
```

- [ ] **Step 4: Style the diagram**

In `docs/templates/persistord/public/css/landing.css`, insert this block directly before the `sections` banner:

```css
/* ----------------------------------------------------------------- model -- */

/* Below 880px the diagram scrolls inside its figure instead of shrinking its
   labels past legibility - the trade PR 1 made for Mermaid diagrams. */
.pd-model-figure {
  margin: 0;
}

.pd-model-scroll {
  overflow-x: auto;
  border-radius: var(--pd-radius);
}

.pd-model-svg {
  display: block;
  width: 100%;
  min-width: 880px;
  height: auto;
  font-family: var(--pd-sans);
}

.pd-model-frame {
  fill: var(--pd-surface);
  stroke: var(--pd-hairline);
  stroke-width: 1;
}

.pd-model-package {
  fill: var(--pd-accent);
  font-family: var(--pd-mono);
  font-size: 13px;
}

/* A node's edge is a meaningful boundary, so it takes the 3:1 control border
   rather than the decorative hairline. */
.pd-model-node > rect {
  fill: var(--pd-raised);
  stroke: var(--pd-control-border);
  stroke-width: 1;
}

.pd-model-name,
.pd-model-cols {
  text-anchor: middle;
}

.pd-model-name {
  fill: var(--pd-text);
  font-family: var(--pd-mono);
  font-size: 15px;
}

.pd-model-cols {
  fill: var(--pd-muted);
  font-size: 12.5px;
}

.pd-model-edge {
  fill: none;
  stroke: var(--pd-muted);
  stroke-width: 1.5;
}

.pd-model-edge[data-kind='id'] {
  stroke-dasharray: 6 5;
}

.pd-model-head {
  fill: var(--pd-muted);
}

.pd-model-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 1.5rem;
  margin-top: 0.85rem;
  color: var(--pd-muted);
  font-size: 0.875rem;
}

.pd-legend-item {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.pd-legend-swatch {
  width: 36px;
  height: 10px;
}
```

In the shared rule that drops the inline-code tint, add `.pd-model > p code,` as its first selector.

- [ ] **Step 5: Rebuild and run the tests to verify they pass**

Run:

```bash
cd docs/tools/site-check && npm run build:fast && node --test tests/model.test.mjs tests/tokens.test.mjs tests/crawl.test.mjs
```

Expected: `0 warning(s)`; all tests pass. If the geometry test reports a label overflowing its box, widen that node's `rect` symmetrically and move its texts' `x` to the new centre. If an edge "does not start/end on" a node after that, move the edge's matching end coordinate onto the node's new edge. Keep every edge orthogonal or straight, and re-run until the list is empty. If the crawl reports `src/Persistord.Core/README.html` unresolved, the `../src/…` hrefs were mistyped; they must mirror `docs/packages/toc.yml`, one `../` shallower.

- [ ] **Step 6: Look at it**

Run `npm run shots`, then use the Read tool on `out/landing-light-1440.png`, `out/landing-dark-1440.png` and `out/landing-dark-375.png`. Check that the diagram reads cleanly in both themes: arrowheads visible at every edge end, dashed and solid edges distinguishable, no label touching an edge, and the phone view scrolling rather than shrinking. Fix any defect in `landing.css` or the coordinates, and re-run Step 5.

- [ ] **Step 7: Lint and commit**

```bash
npx --no-install markdownlint-cli2 docs/index.md
git add docs/index.md docs/templates/persistord/public/css/landing.css docs/tools/site-check/tests/model.test.mjs
git commit -m "docs: landing model diagram, checked edge by edge against the EF configurations

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Build your stack — three steps and the adapter tabs

**Files:**

- Create: `docs/tools/site-check/tests/adapters.test.mjs`
- Modify: `docs/index.md`, `docs/templates/persistord/public/css/landing.css`, `docs/templates/persistord/public/main.js`, `docs/tools/site-check/lib/site.mjs`

**Interfaces:**

- Consumes: `launchSite` (PR 1). The `.pd-install` strip and `wireCopyButtons()` (PR 1 `main.js`) work on any `.pd-copy` whose parent holds a `code`.
- Produces:
  - `site.open(path, { javaScriptEnabled })`, which defaults to `true`.
  - `wireTabs()` in `main.js`. For each `[data-pd-tabs="<label>"]` container with ≥ 2 `:scope > [data-pd-tab]` panels (each with an `id` and a `:scope > h4`), it prepends `div.pd-tablist[role="tablist"][aria-label]` holding `button.pd-tab[role="tab"]#<panel id>-tab`, and gives each panel `role="tabpanel"` and `aria-labelledby`. It adds `.pd-tabs-ready` to the container and keeps exactly one panel un-`hidden`.
  - `section.pd-section.pd-stack` containing `ol.pd-steps > li.pd-step` ×3, `div.pd-adapters[data-pd-tabs="Discord library"]` with `section.pd-adapter#adapter-discordnet|#adapter-dsharpplus|#adapter-netcord`, `ul.pd-addons`, and `p.pd-more`.

- [ ] **Step 1: Let the harness disable JavaScript**

In `docs/tools/site-check/lib/site.mjs`, add this option to the destructuring in `open()` (after `hasTouch = false,`):

```js
      /* false renders the page as a reader without JavaScript sees it. */
      javaScriptEnabled = true,
```

and pass it to the context:

```js
    const context = await browser.newContext({
      viewport: { width, height },
      colorScheme,
      reducedMotion,
      hasTouch,
      javaScriptEnabled,
    })
```

- [ ] **Step 2: Write the failing adapter tests**

`docs/tools/site-check/tests/adapters.test.mjs`:

```js
import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { after, before, test } from 'node:test'
import { launchSite } from '../lib/site.mjs'

const LANDING = 'index.html'
const REPO = new URL('../../../../', import.meta.url)
const TAB = '[data-pd-tabs] [role="tab"]'

const ADAPTERS = [
  {
    label: 'Discord.Net',
    panel: 'adapter-discordnet',
    package: 'Persistord.Adapters.DiscordNet',
    mappers: 'src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs',
    guide: 'articles/discord-net-adapter.html',
  },
  {
    label: 'DSharpPlus',
    panel: 'adapter-dsharpplus',
    package: 'Persistord.Adapters.DSharpPlus',
    mappers: 'src/Persistord.Adapters.DSharpPlus/DSharpPlusMappingExtensions.cs',
    guide: 'articles/dsharpplus-adapter.html',
  },
  {
    label: 'NetCord',
    panel: 'adapter-netcord',
    package: 'Persistord.Adapters.NetCord',
    mappers: 'src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs',
    guide: 'articles/netcord-adapter.html',
  },
]

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

test('the adapter picker is an ARIA tablist with the first adapter selected', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    await page.waitForSelector(TAB)
    const state = await page.$eval('[data-pd-tabs]', container => {
      const tablist = container.querySelector(':scope > [role="tablist"]')
      return {
        label: tablist?.getAttribute('aria-label'),
        tabs: [...tablist.querySelectorAll('[role="tab"]')].map(tab => {
          const panel = document.getElementById(tab.getAttribute('aria-controls'))
          return {
            name: tab.textContent.trim(),
            tag: tab.tagName.toLowerCase(),
            selected: tab.getAttribute('aria-selected'),
            tabIndex: tab.tabIndex,
            panel: panel?.id,
            panelRole: panel?.getAttribute('role'),
            labelledBy: panel?.getAttribute('aria-labelledby') === tab.id,
            hidden: panel?.hidden,
            headingShown: getComputedStyle(panel.querySelector(':scope > h4')).display !== 'none',
          }
        }),
      }
    })

    assert.equal(state.label, 'Discord library')
    assert.deepEqual(
      state.tabs,
      ADAPTERS.map((adapter, i) => ({
        name: adapter.label,
        tag: 'button',
        selected: String(i === 0),
        tabIndex: i === 0 ? 0 : -1,
        panel: adapter.panel,
        panelRole: 'tabpanel',
        labelledBy: true,
        hidden: i !== 0,
        headingShown: false,
      }))
    )
  } finally {
    await close()
  }
})

test('arrow keys wrap, Home and End jump, a click selects, and Tab leaves the tablist', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    await page.waitForSelector(TAB)
    const selection = () =>
      page.$eval('[data-pd-tabs]', container => {
        const tab = container.querySelector('[role="tab"][aria-selected="true"]')
        return {
          tab: tab.textContent.trim(),
          focused: document.activeElement === tab,
          visible: [...container.querySelectorAll('[role="tabpanel"]')].filter(panel => !panel.hidden).map(panel => panel.id),
        }
      })

    await page.focus(`${TAB}[aria-selected="true"]`)
    const steps = [
      ['ArrowRight', 'DSharpPlus', 'adapter-dsharpplus'],
      ['ArrowRight', 'NetCord', 'adapter-netcord'],
      ['ArrowRight', 'Discord.Net', 'adapter-discordnet'],
      ['ArrowLeft', 'NetCord', 'adapter-netcord'],
      ['Home', 'Discord.Net', 'adapter-discordnet'],
      ['End', 'NetCord', 'adapter-netcord'],
    ]
    for (const [key, tab, panel] of steps) {
      await page.keyboard.press(key)
      assert.deepEqual(await selection(), { tab, focused: true, visible: [panel] }, `after ${key}`)
    }

    await page.getByRole('tab', { name: 'DSharpPlus' }).click()
    assert.deepEqual((await selection()).visible, ['adapter-dsharpplus'], 'after a click')

    await page.keyboard.press('Tab')
    assert.notEqual(await page.evaluate(() => document.activeElement.getAttribute('role')), 'tab', 'Tab moved to another tab instead of leaving the tablist')
  } finally {
    await close()
  }
})

test('without JavaScript the three adapters render stacked under their own headings', async () => {
  const { page, close } = await site.open(LANDING, { javaScriptEnabled: false })
  try {
    assert.equal(await page.locator('[role="tablist"]').count(), 0)
    const panels = await page.$$eval('[data-pd-tabs] > [data-pd-tab]', sections =>
      sections.map(section => {
        const rect = section.getBoundingClientRect()
        return { heading: section.querySelector(':scope > h4')?.textContent.trim(), top: rect.top, bottom: rect.bottom, height: rect.height }
      })
    )
    assert.deepEqual(panels.map(({ heading }) => heading), ADAPTERS.map(({ label }) => label))
    panels.forEach((panel, i) => {
      assert.ok(panel.height > 0, `${panel.heading} is not rendered`)
      if (i > 0) {
        assert.ok(panel.top >= panels[i - 1].bottom, `${panel.heading} is not below ${panels[i - 1].heading}`)
      }
    })
  } finally {
    await close()
  }
})

test('each panel installs its adapter and calls only mappers it declares, with matching arguments', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    for (const adapter of ADAPTERS) {
      const panel = await page.$eval(`#${adapter.panel}`, section => ({
        install: section.querySelector('.pd-install > code')?.textContent.trim(),
        snippet: section.querySelector('pre > code.lang-csharp')?.textContent.trim() ?? '',
        links: [...section.querySelectorAll('a')].map(link => link.getAttribute('href')),
      }))
      const source = (await readFile(new URL(adapter.mappers, REPO), 'utf8')).replace(/\s+/g, ' ')

      assert.equal(panel.install, `dotnet add package ${adapter.package}`)
      const lines = panel.snippet.split('\n').length
      assert.ok(panel.snippet && lines <= 3, `${adapter.label} snippet has ${panel.snippet ? lines : 0} lines`)

      const calls = [...panel.snippet.matchAll(/\.To(\w+)Entity\(([^)]*)\)/g)]
      assert.ok(calls.length > 0, `${adapter.label} snippet calls no mapper`)
      for (const [call, name, args] of calls) {
        const signature = new RegExp(`public static \\w+ To${name}Entity\\(this [\\w.<>?]+ \\w+((?:, [\\w.<>?]+ \\w+)*)\\)`).exec(source)
        assert.ok(signature, `${adapter.label}: ${call} is not declared in ${adapter.mappers}`)
        const declared = signature[1] ? signature[1].split(',').length - 1 : 0
        const passed = args.trim() === '' ? 0 : args.split(',').length
        assert.equal(passed, declared, `${adapter.label}: ${call} passes ${passed} argument(s); the mapper takes ${declared}`)
      }

      assert.ok(panel.links.includes(`src/${adapter.package}/README.html`), `${adapter.label} does not link its README`)
      assert.ok(panel.links.includes(adapter.guide), `${adapter.label} does not link its guide`)
    }

    const dsharp = await page.$eval('#adapter-dsharpplus pre > code', code => code.textContent)
    assert.match(dsharp, /\.To(?:Member|Role)Entity\(\s*[\w.]+\s*\)/, 'the DSharpPlus snippet must show the guildId parameter')
  } finally {
    await close()
  }
})
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:

```bash
cd docs/tools/site-check && node --test tests/adapters.test.mjs
```

Expected: all four tests FAIL. The first two time out waiting for `[data-pd-tabs] [role="tab"]`, the no-JS test gets `[]` headings, and the snippet test fails on `page.$eval: Failed to find element: #adapter-discordnet`.

- [ ] **Step 4: Replace "Ten packages" with "Build your stack"**

In `docs/index.md`, replace the whole `<section class="pd-section">` whose heading is `<h2>Ten packages</h2>` with the block below. It must contain no blank lines, and the `<pre>` lines stay at column 0:

```html
<section class="pd-section pd-stack">
  <h2>Build your stack</h2>
  <ol class="pd-steps">
    <li class="pd-step">
      <h3>Add the model</h3>
      <p>The meta package brings <a href="../src/Persistord.Core/README.md"><code>Persistord.Core</code></a>, <a href="../src/Persistord.Messages/README.md"><code>Persistord.Messages</code></a> and <a href="../src/Persistord.History/README.md"><code>Persistord.History</code></a> in one reference.</p>
      <div class="pd-install"><span class="pd-prompt">$</span><code>dotnet add package Persistord</code><button class="pd-copy" type="button" aria-live="polite">Copy</button></div>
    </li>
    <li class="pd-step">
      <h3>Pick one adapter</h3>
      <p>An adapter maps one Discord library's objects to Persistord entities. Take at most one, or write the mapping yourself.</p>
      <div class="pd-adapters" data-pd-tabs="Discord library">
        <section class="pd-adapter" id="adapter-discordnet" data-pd-tab>
          <h4>Discord.Net</h4>
          <div class="pd-install"><span class="pd-prompt">$</span><code>dotnet add package Persistord.Adapters.DiscordNet</code><button class="pd-copy" type="button" aria-live="polite">Copy</button></div>
<pre><code class="lang-csharp">db.Guilds.Add(guild.ToGuildEntity());
db.Members.Add(guildUser.ToMemberEntity());
db.Messages.Add(message.ToMessageEntity());</code></pre>
          <p class="pd-adapter-links"><a href="../src/Persistord.Adapters.DiscordNet/README.md"><code>Persistord.Adapters.DiscordNet</code></a> · <a href="articles/discord-net-adapter.md">Discord.Net guide</a></p>
        </section>
        <section class="pd-adapter" id="adapter-dsharpplus" data-pd-tab>
          <h4>DSharpPlus</h4>
          <div class="pd-install"><span class="pd-prompt">$</span><code>dotnet add package Persistord.Adapters.DSharpPlus</code><button class="pd-copy" type="button" aria-live="polite">Copy</button></div>
<pre><code class="lang-csharp">db.Guilds.Add(guild.ToGuildEntity());
db.Members.Add(member.ToMemberEntity(guild.Id));
db.Messages.Add(message.ToMessageEntity());</code></pre>
          <p class="pd-adapter-links"><a href="../src/Persistord.Adapters.DSharpPlus/README.md"><code>Persistord.Adapters.DSharpPlus</code></a> · <a href="articles/dsharpplus-adapter.md">DSharpPlus guide</a></p>
        </section>
        <section class="pd-adapter" id="adapter-netcord" data-pd-tab>
          <h4>NetCord</h4>
          <div class="pd-install"><span class="pd-prompt">$</span><code>dotnet add package Persistord.Adapters.NetCord</code><button class="pd-copy" type="button" aria-live="polite">Copy</button></div>
<pre><code class="lang-csharp">db.Guilds.Add(guild.ToGuildEntity());
db.Members.Add(guildUser.ToMemberEntity());
db.Messages.Add(message.ToMessageEntity());</code></pre>
          <p class="pd-adapter-links"><a href="../src/Persistord.Adapters.NetCord/README.md"><code>Persistord.Adapters.NetCord</code></a> · <a href="articles/netcord-adapter.md">NetCord guide</a></p>
        </section>
      </div>
    </li>
    <li class="pd-step">
      <h3>Add what you need</h3>
      <ul class="pd-addons">
        <li><a href="../src/Persistord.Managed/README.md"><code>Persistord.Managed</code></a><span>The categories, channels, anchored messages and webhooks your bot owns.</span></li>
        <li><a href="../src/Persistord.Protection/README.md"><code>Persistord.Protection</code></a><span>Encrypts <code>[Protected]</code> string columns at rest.</span></li>
        <li><a href="../src/Persistord.Testing/README.md"><code>Persistord.Testing</code></a><span>In-memory SQLite fixtures and EF Core model assertions.</span></li>
      </ul>
    </li>
  </ol>
  <p class="pd-more"><a href="articles/packages.md">Compare all ten packages<span aria-hidden="true"> →</span></a></p>
</section>
```

(The add-on sentences are the PR 1 landing copy, unchanged. Every mapper call is checked by the snippet test against the adapter's own source.)

- [ ] **Step 5: Wire the tabs**

In `docs/templates/persistord/public/main.js`, insert this function directly before `/** Runs a callback once the document has parsed. */`:

```js
/**
 * Upgrades each `[data-pd-tabs]` container of stacked panels into a tabs
 * widget, following the WAI-ARIA Authoring Practices pattern with automatic
 * activation: one tab stop (roving tabindex), Left/Right arrows that wrap, and
 * Home/End. Panels are authored stacked, each under its own h4, so the page
 * still reads correctly when this never runs; the h4 becomes the tab's label
 * and landing.css hides it once the tablist exists.
 */
function wireTabs() {
  for (const container of document.querySelectorAll('[data-pd-tabs]')) {
    const panels = [...container.querySelectorAll(':scope > [data-pd-tab]')]
    if (panels.length < 2) {
      continue
    }

    const tablist = document.createElement('div')
    tablist.className = 'pd-tablist'
    tablist.setAttribute('role', 'tablist')
    tablist.setAttribute('aria-label', container.dataset.pdTabs)

    const tabs = panels.map(panel => {
      const tab = document.createElement('button')
      tab.type = 'button'
      tab.className = 'pd-tab'
      tab.id = `${panel.id}-tab`
      tab.textContent = panel.querySelector(':scope > h4')?.textContent.trim() ?? panel.id
      tab.setAttribute('role', 'tab')
      tab.setAttribute('aria-controls', panel.id)
      panel.setAttribute('role', 'tabpanel')
      panel.setAttribute('aria-labelledby', tab.id)
      tablist.append(tab)
      return tab
    })

    const select = (index, focus) => {
      tabs.forEach((tab, i) => {
        const selected = i === index
        tab.setAttribute('aria-selected', String(selected))
        tab.tabIndex = selected ? 0 : -1
        panels[i].hidden = !selected
      })
      if (focus) {
        tabs[index].focus()
      }
    }

    tablist.addEventListener('click', event => {
      const index = tabs.indexOf(event.target.closest('[role="tab"]'))
      if (index >= 0) {
        select(index, false)
      }
    })

    tablist.addEventListener('keydown', event => {
      const current = tabs.indexOf(document.activeElement)
      if (current < 0) {
        return
      }
      const last = tabs.length - 1
      const next = {
        ArrowRight: current === last ? 0 : current + 1,
        ArrowLeft: current === 0 ? last : current - 1,
        Home: 0,
        End: last,
      }[event.key]
      if (next === undefined) {
        return
      }
      event.preventDefault()
      select(next, true)
    })

    container.prepend(tablist)
    container.classList.add('pd-tabs-ready')
    select(0, false)
  }
}
```

In `start`, add `wireTabs()` after `wireCopyButtons()`:

```js
  start: () => {
    onReady(() => {
      wireCopyButtons()
      wireTabs()
      labelCodeBlocks()
      wireSearchShortcut()
      trackAffix()
    })
  },
```

- [ ] **Step 6: Style the stack and retire the package cards**

In `docs/templates/persistord/public/css/landing.css`, replace **everything from the `sections` banner to the end of the file** with:

```css
/* -------------------------------------------------------------- sections -- */

.pd-section {
  margin: 0 0 4rem;
}

.pd-section > h2 {
  margin: 0 0 0.55rem;
  font-size: 1.5rem;
}

.pd-section > p {
  margin: 0 0 1.5rem;
  max-width: 74ch;
  color: var(--pd-muted);
}

/* Package and API names in landing copy read as texture, so the inline-code
   tint is dropped. */
.pd-section > p code,
.pd-step > p code,
.pd-adapter-links code,
.pd-addons code,
.pd-card code {
  padding: 0;
  background-color: transparent;
  color: inherit;
  font-size: 0.94em;
}

.pd-section > .pd-more {
  margin: 1.75rem 0 0;
}

.pd-more a {
  font-weight: 600;
  text-decoration: none;
}

.pd-more a:hover {
  text-decoration: underline;
}

/* ----------------------------------------------------------------- stack -- */

.pd-steps {
  display: grid;
  gap: 2.25rem;
  margin: 1.75rem 0 0;
  padding: 0;
  list-style: none;
  counter-reset: pd-step;
}

.pd-step {
  position: relative;
  min-width: 0;
  padding-left: 3rem;
  counter-increment: pd-step;
}

/* The step number is a boundary-drawn badge, so its ring takes the 3:1
   control border. */
.pd-step::before {
  content: counter(pd-step);
  position: absolute;
  top: 0;
  left: 0;
  display: grid;
  place-items: center;
  width: 2rem;
  height: 2rem;
  border: 1px solid var(--pd-control-border);
  border-radius: 50%;
  color: var(--pd-heading);
  font-size: 0.9rem;
  font-weight: 700;
}

.pd-step > h3 {
  margin: 0.2rem 0 0.4rem;
  font-size: 1.1rem;
}

.pd-step > p {
  margin: 0 0 0.9rem;
  max-width: 72ch;
  color: var(--pd-muted);
}

.pd-step .pd-install {
  max-width: 40rem;
}

/* Stacked until main.js builds the tablist; stays stacked without JS. */
.pd-adapters {
  display: grid;
  gap: 1.5rem;
}

.pd-tabs-ready {
  gap: 0;
}

.pd-adapter {
  display: grid;
  gap: 0.75rem;
  min-width: 0;
}

.pd-adapter[hidden] {
  display: none;
}

.pd-adapter > h4 {
  margin: 0;
  font-size: 1rem;
}

.pd-tabs-ready > .pd-adapter > h4 {
  display: none;
}

.pd-adapter pre {
  margin: 0;
}

.pd-adapter-links {
  margin: 0;
  color: var(--pd-muted);
  font-size: 0.9rem;
}

.pd-tablist {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  margin: 0 0 1rem;
  border-bottom: 1px solid var(--pd-hairline);
}

.pd-tab {
  margin-bottom: -1px;
  padding: 0.5rem 0.9rem;
  border: 0;
  border-bottom: 2px solid transparent;
  background: none;
  color: var(--pd-muted);
  font-family: var(--pd-sans);
  font-size: 0.95rem;
  font-weight: 600;
  cursor: pointer;
  transition: color var(--pd-ease), border-color var(--pd-ease);
}

.pd-tab:hover {
  color: var(--pd-text);
}

.pd-tab[aria-selected='true'] {
  border-bottom-color: var(--pd-accent);
  color: var(--pd-accent);
}

.pd-addons {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 1rem 1.5rem;
  margin: 0;
  padding: 0;
  list-style: none;
}

@media (max-width: 900px) {
  .pd-addons {
    grid-template-columns: minmax(0, 1fr);
  }
}

.pd-addons > li > a {
  font-weight: 600;
  text-decoration: none;
}

.pd-addons > li > a:hover {
  text-decoration: underline;
}

.pd-addons > li > span {
  display: block;
  margin-top: 0.2rem;
  color: var(--pd-muted);
  font-size: 0.9rem;
  line-height: 1.55;
}

/* ----------------------------------------------------------------- cards -- */

.pd-card {
  display: block;
  position: relative;
  height: 100%;
  padding: 0.75rem 0.95rem;
  border: 1px solid var(--pd-hairline);
  border-radius: var(--pd-radius);
  background-color: var(--pd-surface);
  color: var(--pd-text);
  text-decoration: none;
  transition: background-color var(--pd-ease), border-color var(--pd-ease);
}

.pd-card:hover,
.pd-card:focus-visible {
  border-color: var(--pd-muted);
  background-color: var(--pd-raised);
  color: var(--pd-text);
  text-decoration: none;
}

.pd-card > strong {
  display: block;
  font-size: 0.95rem;
  font-weight: 600;
  overflow-wrap: anywhere;
}

.pd-card > span {
  display: block;
  margin-top: 0.25rem;
  color: var(--pd-muted);
  font-size: 0.85rem;
  line-height: 1.5;
}

.pd-cards,
.pd-next {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(15.5rem, 1fr));
  gap: 0.8rem;
}
```

(The `cards` block is interim: Task 5 rewrites it. This replacement drops `.pd-packages`, `.pd-package-group`, `.pd-package`, `.pd-note`, and the old shared tint rule, whose last users this task removed.)

- [ ] **Step 7: Rebuild and run the tests to verify they pass**

Run:

```bash
cd docs/tools/site-check && npm run build:fast && node --test tests/adapters.test.mjs tests/landing.test.mjs tests/model.test.mjs tests/tokens.test.mjs tests/crawl.test.mjs tests/components.test.mjs
```

Expected: `0 warning(s)`; all tests pass. `components.test.mjs` is included because the snippets reuse its code-block styles, which must still pass on the component page.

- [ ] **Step 8: Look at it**

Run `npm run shots`. Read `out/landing-light-1440.png` and `out/landing-dark-375.png`. Check that the step badges line up with their headings, the tab underline sits on the tablist rule, the C# label and copy button sit on the snippet frame, and nothing overflows at 375px. Fix any defect in `landing.css` and re-run Step 7.

- [ ] **Step 9: Lint and commit**

```bash
npx --no-install markdownlint-cli2 docs/index.md
git add docs/index.md docs/templates/persistord/public/css/landing.css docs/templates/persistord/public/main.js docs/tools/site-check/lib/site.mjs docs/tools/site-check/tests/adapters.test.mjs
git commit -m "docs: landing build-your-stack steps with an accessible adapter picker

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: What's in the box, Start here, and the page-level guards

**Files:**

- Modify: `docs/index.md`, `docs/templates/persistord/public/css/landing.css`, `docs/tools/site-check/tests/landing.test.mjs`

**Interfaces:**

- Consumes: `site`, `LANDING` (Task 1); every section class from Tasks 1–4.
- Produces: `section.pd-section.pd-box > ul.pd-features > li > a.pd-feature` ×9 and `section.pd-section.pd-start > div.pd-start-cards > a.pd-card` ×3. `.pd-card` exists only in Start here. Two static guards: no unused landing class, and no retired block.

- [ ] **Step 1: Write the failing page-level tests**

At the top of `docs/tools/site-check/tests/landing.test.mjs`, add this import after the `node:assert/strict` import:

```js
import { readFile } from 'node:fs/promises'
```

Then append:

```js
test('the page is six sections in the spec order', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const sections = await page.$$eval('.content article .pd-hero, .content article .pd-section', list =>
      list.map(section => [section.className, section.querySelector(':scope > h1, :scope > h2')?.textContent.trim()])
    )
    assert.deepEqual(sections, [
      ['pd-hero', 'Every Discord bot rewrites the same tables. Persistord ships them.'],
      ['pd-section pd-transform', 'A gateway payload, a mapper call, a row'],
      ['pd-section pd-model', "The model you'd have written"],
      ['pd-section pd-stack', 'Build your stack'],
      ['pd-section pd-box', "What's in the box"],
      ['pd-section pd-start', 'Start here'],
    ])
  } finally {
    await close()
  }
})

test("What's in the box is a 3 x 3 list of guides with a left rule and no card chrome", async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const items = await page.$$eval('.pd-box .pd-features > li > a.pd-feature', links =>
      links.map(link => {
        const style = getComputedStyle(link)
        return {
          href: link.getAttribute('href'),
          title: link.querySelector(':scope > strong')?.textContent.trim(),
          sentence: link.querySelector(':scope > span')?.textContent.trim() ?? '',
          left: Math.round(link.getBoundingClientRect().left),
          rule: style.borderLeftWidth,
          top: style.borderTopWidth,
          radius: style.borderTopLeftRadius,
          background: style.backgroundColor,
        }
      })
    )

    assert.equal(items.length, 9)
    for (const item of items) {
      assert.match(item.href, /^articles\/[\w-]+\.html$/, `${item.title} does not link a guide`)
      assert.ok(item.title, 'an item has no title')
      assert.equal(item.sentence.match(/\.(?:\s|$)/g)?.length ?? 0, 1, `${item.title} is not one sentence`)
      assert.equal(item.rule, '2px', `${item.title} left rule`)
      assert.equal(item.top, '0px', `${item.title} has a top border`)
      assert.equal(item.radius, '0px', `${item.title} is rounded`)
      assert.equal(parseColor(item.background)[3], 0, `${item.title} has a background`)
    }
    assert.equal(new Set(items.map(({ left }) => left)).size, 3, 'the list is not three columns at desktop width')
  } finally {
    await close()
  }
})

test('Start here holds the only card chrome on the page: three cards', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const cards = await page.$$eval('.content article .pd-card', links =>
      links.map(link => [link.querySelector(':scope > strong')?.textContent.trim(), link.getAttribute('href'), Boolean(link.closest('.pd-start'))])
    )
    assert.deepEqual(cards, [
      ['Getting Started', 'articles/getting-started.html', true],
      ['Guides', 'articles/introduction.html', true],
      ['API Reference', 'api/index.html', true],
    ])
    assert.equal(await page.locator('.pd-cards, .pd-next, .pd-packages, .pd-package, .pd-stats, .pd-note').count(), 0, 'a retired landing block is still on the page')
  } finally {
    await close()
  }
})

test('every package the page names in code links to its README page', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const named = await page.$$eval('.content article code', codes =>
      codes
        .map(code => [code.textContent.trim(), code.closest('a')?.getAttribute('href') ?? null])
        .filter(([text]) => /^Persistord(?:\.[A-Za-z]+)+$/.test(text))
    )
    assert.ok(named.length >= 9, `only ${named.length} package names found`)
    const unlinked = named.filter(([name, href]) => href !== `src/${name}/README.html`).map(([name, href]) => `${name} -> ${href}`)
    assert.deepEqual(unlinked, [])
  } finally {
    await close()
  }
})

test('every class landing.css styles is used by the page or main.js', async () => {
  const docs = new URL('../../../', import.meta.url)
  const css = (await readFile(new URL('templates/persistord/public/css/landing.css', docs), 'utf8')).replace(/\/\*[\s\S]*?\*\//g, '')
  const used = (await readFile(new URL('index.md', docs), 'utf8')) + (await readFile(new URL('templates/persistord/public/main.js', docs), 'utf8'))
  const unused = [...new Set([...css.matchAll(/\.(pd-[a-z0-9-]+)/g)].map(([, name]) => name))].filter(
    name => !new RegExp(`(?<![\\w-])${name}(?![\\w-])`).test(used)
  )
  assert.deepEqual(unused, [])
})
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
cd docs/tools/site-check && node --test tests/landing.test.mjs
```

Expected: Tasks 1–2's tests pass; `six sections` fails (`pd-section` / `What you get` rows); `What's in the box…` fails (`0 !== 9`); `Start here…` fails (sixteen cards, none inside `.pd-start`). The package-link and unused-class tests already pass: Tasks 3–4 link every package name, and every class in today's `landing.css` is still on the page. They guard Step 4, which retires `.pd-cards` and `.pd-next`.

- [ ] **Step 3: Replace the last two sections**

In `docs/index.md`, replace the `<section class="pd-section">` headed `<h2>What you get</h2>` **and** the one headed `<h2>Where to go next</h2>` (through the end of the file) with:

```html
<section class="pd-section pd-box">
  <h2>What's in the box</h2>
  <ul class="pd-features">
    <li><a class="pd-feature" href="articles/snowflake-conversion.md"><strong>Snowflake conversion</strong><span>Registered once in <code>ConfigureConventions</code>: every <code>ulong</code> and <code>ulong?</code> in your model, never an annotation.</span></a></li>
    <li><a class="pd-feature" href="articles/core-graph.md"><strong>Core graph</strong><span>Guilds, channels, users, members and roles — five skeleton entities you opt into, or ignore entirely.</span></a></li>
    <li><a class="pd-feature" href="articles/upsert.md"><strong>Upsert</strong><span>Insert-or-update keyed on the snowflake, for the gateway events that arrive out of order.</span></a></li>
    <li><a class="pd-feature" href="articles/soft-delete-and-query-filters.md"><strong>Soft-delete &amp; query filters</strong><span>Deleted messages stay addressable so history rows keep a valid foreign key.</span></a></li>
    <li><a class="pd-feature" href="articles/history.md"><strong>History</strong><span>Append-only edit history, one row per revision, with a real FK back to the message.</span></a></li>
    <li><a class="pd-feature" href="articles/guild-lifecycle.md"><strong>Guild purge</strong><span>Joining and leaving a guild, both directions, without orphan rows.</span></a></li>
    <li><a class="pd-feature" href="articles/managed-resources.md"><strong>Managed resources</strong><span>Track the Discord objects your bot created and owns, apart from the ones it only mirrors.</span></a></li>
    <li><a class="pd-feature" href="articles/protection.md"><strong>Protection</strong><span>Encrypt marked string columns at rest through ASP.NET Core Data Protection.</span></a></li>
    <li><a class="pd-feature" href="articles/testing.md"><strong>Testing fixtures</strong><span>In-memory SQLite contexts and assertions over the built EF Core model.</span></a></li>
  </ul>
</section>

<section class="pd-section pd-start">
  <h2>Start here</h2>
  <div class="pd-start-cards">
    <a class="pd-card" href="articles/getting-started.md"><strong>Getting Started</strong><span>Install, derive a context, pick a provider, save your first rows.</span></a>
    <a class="pd-card" href="articles/introduction.md"><strong>Guides</strong><span>Concepts, providers, adapters, add-ons and recipes, one topic per guide.</span></a>
    <a class="pd-card" href="api/index.md"><strong>API Reference</strong><span>Generated from the XML doc comments across the nine packages that ship code.</span></a>
  </div>
</section>
```

(The nine capability sentences are the PR 1 card copy, unchanged.)

- [ ] **Step 4: Style the list and the cards**

In `docs/templates/persistord/public/css/landing.css`:

1. In the shared inline-code tint rule, add `.pd-feature code,` before `.pd-card code`.
2. Replace **everything from the `cards` banner to the end of the file** with:

```css
/* ------------------------------------------------------------------- box -- */

.pd-features {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 1.75rem 2rem;
  margin: 1.5rem 0 0;
  padding: 0;
  list-style: none;
}

@media (max-width: 900px) {
  .pd-features {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 600px) {
  .pd-features {
    grid-template-columns: minmax(0, 1fr);
  }
}

/* A left rule and no card chrome. The rule is decorative - the title and
   sentence carry the item - so it rests as a hairline and takes the accent on
   hover and focus. */
.pd-feature {
  display: block;
  height: 100%;
  padding: 0.1rem 0 0.1rem 1rem;
  border-left: 2px solid var(--pd-hairline);
  color: var(--pd-text);
  text-decoration: none;
  transition: border-color var(--pd-ease);
}

.pd-feature:hover,
.pd-feature:focus-visible {
  border-left-color: var(--pd-accent);
  color: var(--pd-text);
  text-decoration: none;
}

.pd-feature > strong {
  display: block;
  color: var(--pd-heading);
  font-weight: 600;
}

.pd-feature > span {
  display: block;
  margin-top: 0.3rem;
  color: var(--pd-muted);
  font-size: 0.9rem;
  line-height: 1.55;
}

/* ----------------------------------------------------------------- start -- */

.pd-start-cards {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 1rem;
  margin-top: 1.5rem;
}

@media (max-width: 900px) {
  .pd-start-cards {
    grid-template-columns: minmax(0, 1fr);
  }
}

/* The page's only card chrome (spec 3.3). */
.pd-card {
  display: block;
  height: 100%;
  padding: 1.1rem 1.25rem;
  border: 1px solid var(--pd-hairline);
  border-radius: var(--pd-radius);
  background-color: var(--pd-surface);
  color: var(--pd-text);
  text-decoration: none;
  transition: background-color var(--pd-ease), border-color var(--pd-ease);
}

.pd-card:hover,
.pd-card:focus-visible {
  border-color: var(--pd-muted);
  background-color: var(--pd-raised);
  color: var(--pd-text);
  text-decoration: none;
}

.pd-card > strong {
  display: block;
  color: var(--pd-heading);
  font-size: 1.05rem;
  font-weight: 600;
}

.pd-card > span {
  display: block;
  margin-top: 0.35rem;
  color: var(--pd-muted);
  font-size: 0.9rem;
  line-height: 1.55;
}
```

- [ ] **Step 5: Rebuild and run the tests to verify they pass**

Run:

```bash
cd docs/tools/site-check && npm run build:fast && node --test tests/landing.test.mjs tests/model.test.mjs tests/adapters.test.mjs tests/tokens.test.mjs tests/crawl.test.mjs tests/chrome.test.mjs
```

Expected: `0 warning(s)`; all tests pass. If `every class landing.css styles…` lists a name, delete that rule from `landing.css`. Do not add the class to the page to satisfy the test.

- [ ] **Step 6: Lint and commit**

```bash
npx --no-install markdownlint-cli2 docs/index.md
git add docs/index.md docs/templates/persistord/public/css/landing.css docs/tools/site-check/tests/landing.test.mjs
git commit -m "docs: landing capability list and start-here cards; guard retired blocks

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Keyboard, phone width, spec amendments, and the full gate

**Files:**

- Modify: `docs/tools/site-check/tests/keyboard.test.mjs`, `docs/tools/site-check/tests/landing.test.mjs`, `docs/superpowers/specs/2026-09-14-docs-site-revamp-design.md`

**Interfaces:**

- Consumes: everything above.
- Produces: the landing page covered by the site-wide Tab walk, a 375px overflow guard, and a spec that matches what shipped.

- [ ] **Step 1: Walk the landing page with the keyboard**

In `docs/tools/site-check/tests/keyboard.test.mjs`, replace the `PAGES` constant and its comment with:

```js
/* An article with the in-page rail and prev/next; the component page, which
   adds copy buttons and a tab group; and the landing page, which has no
   sidebar but adds buttons, copy strips, the diagram region and the adapter
   tabs. `ready` lists what must render before the walk starts. */
const PAGES = [
  { path: 'articles/upsert.html', ready: ['#toc li a', '.next-article a', 'footer a'] },
  { path: 'development/components.html', ready: ['#toc li a', '.next-article a', 'footer a'] },
  { path: 'index.html', ready: ['[data-pd-tabs] [role="tab"]', 'footer a'] },
]
```

Add these entries to the end of `MUST_REACH`:

```js
  'a landing button': '.pd-btn',
  'a copy strip': '.pd-copy',
  'the diagram region': '.pd-model-scroll',
  'an adapter tab': '.pd-tablist [role="tab"]',
  'a capability': '.pd-feature',
  'a start-here card': '.pd-card',
```

Change the page loop header from `for (const path of PAGES) {` to:

```js
  for (const { path, ready } of PAGES) {
```

and replace its three fixed waits:

```js
        await page.waitForSelector('#toc li a')
        await page.waitForSelector('.next-article a')
        await page.waitForSelector('footer a')
```

with:

```js
        for (const selector of ready) {
          await page.waitForSelector(selector)
        }
```

Append to `docs/tools/site-check/tests/landing.test.mjs`:

```js
for (const theme of THEMES) {
  test(`${theme}: the landing page never scrolls sideways on a phone`, async () => {
    const { page, close } = await site.open(LANDING, { theme, width: 375, height: 800 })
    try {
      const overflow = await page.evaluate(() => {
        const width = document.documentElement.clientWidth
        /* Code, install commands and the diagram scroll inside their own frames. */
        const offenders = [...document.querySelectorAll('.content article *')]
          .filter(element => !element.closest('.pd-model-scroll, pre, .pd-install > code'))
          .filter(element => element.getBoundingClientRect().right > width + 1)
          .map(element => `<${element.tagName.toLowerCase()} class="${element.getAttribute('class') ?? ''}">`)
        return { page: document.documentElement.scrollWidth - width, offenders: offenders.slice(0, 10) }
      })
      assert.deepEqual(overflow.offenders, [], `${theme}: elements past the right edge`)
      assert.equal(overflow.page, 0, `${theme}: the page is wider than the viewport`)
    } finally {
      await close()
    }
  })
}
```

- [ ] **Step 2: Run the new checks**

Run:

```bash
cd docs/tools/site-check && node --test tests/keyboard.test.mjs tests/landing.test.mjs
```

Expected: all tests pass. These are guards over work already done, so a failure is a defect in Tasks 1–5. Fix it at its source. If the walk names an element with no indicator, it needs the `:focus-visible` outline: add a `landing.css` rule only if `base.css`'s global rule is being overridden, and never weaken the test. If an element overflows at 375px, give it `min-width: 0` or `overflow-wrap: anywhere` in `landing.css`.

- [ ] **Step 3: Amend the spec to match what shipped**

Edit `docs/superpowers/specs/2026-09-14-docs-site-revamp-design.md`:

1. Section 3.2, item 1. Replace:

```markdown
   - Actions: **Get started** (primary, gradient), **Browse packages** (outline), "GitHub →"
     (text link).
```

with:

```markdown
   - Actions: **Get started** (primary, gradient), **Browse packages** (outline), "GitHub"
     (text link; its arrow is DocFX's external-link icon).
```

2. Section 3.2, item 3. Replace the paragraph that starts `3. **The model you'd have written.**` and ends `the diagram draws only relationships the model\n   configures.` with:

```markdown
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
```

3. Section 3.2, **Mobile.** Replace:

```markdown
**Mobile.** All sections stack; the SVG scales through its `viewBox`; the proof panes stack
vertically with connectors rotated to point down.
```

with:

```markdown
**Mobile.** All sections stack; the SVG scales through its `viewBox` down to an 880 px minimum and
scrolls inside its figure below that, as the Mermaid diagrams do, so its labels stay legible; the
proof panes stack vertically with connectors rotated to point down.
```

4. Section 3.3. Replace:

```markdown
- The model SVG has been checked edge-by-edge against source, and the review comment on the PR
  lists the configuration each edge came from.
```

with:

```markdown
- The model SVG has been checked edge-by-edge against source (`tests/model.test.mjs` re-checks it on
  every run), and the PR description lists the configuration or column each edge came from.
```

5. Section 5.2. Replace `` - `fileMetadata._description` covers the pages `` with `` - `fileMetadata.description` covers the pages ``, and replace `` `src/*/README.md`, and `samples/README.md`. `` with `` `src/*/README.md`, `samples/README.md`, and `license.md`. ``.

6. Section 6.2, check 2. Replace `` one API class page; the 404 `` with `` one API class page; the search results view; the 404 ``.

Then run:

```bash
npx --no-install markdownlint-cli2 docs/superpowers/specs/2026-09-14-docs-site-revamp-design.md
```

Expected: 0 issues.

- [ ] **Step 4: Run the full gate from a clean build**

Run:

```bash
rm -rf docs/_site docs/api/*.yml docs/api/.manifest docs/api/toc.yml
cd docs/tools/site-check && npm run build && npm test
```

Expected: the full build (metadata + build) exits 0 with `0 warning(s)`; every suite passes, both PR 1's eleven and this PR's three new ones. A failure in a PR 1 suite means a landing change leaked: `landing.css` rules must be scoped to `.pd-*` classes or `body[data-layout='landing']`.

- [ ] **Step 5: Take and review the screenshot matrix**

Run:

```bash
cd docs/tools/site-check && npm run shots
```

Expected: 40 PNGs in `out/`. Use the Read tool on all four `landing-*` shots and on one shot of every other page. For the landing page check that:

- the hero reads left-aligned, with the wordmark gradient visible in both themes;
- the panes sit in one row at 1440px, and stack with downward arrows at 375px;
- the diagram is legible, every arrowhead is visible, and dashed and solid edges are distinct;
- one adapter panel shows beneath the tablist;
- the capability list is 3 × 3 with left rules;
- there are exactly three cards;
- nothing is clipped at 375px.

For the other pages, check that nothing changed from PR 1. Fix any defect in `landing.css`, add a test that would have caught it to the owning suite, and re-run `npm run build:fast && npm test && npm run shots`.

- [ ] **Step 6: Confirm scope**

Run:

```bash
git diff --stat develop -- .github src samples docs/articles docs/docfx.json docs/404.html docs/templates/persistord/public/css/tokens.css docs/templates/persistord/public/css/base.css docs/templates/persistord/public/css/chrome.css docs/templates/persistord/public/css/components.css docs/templates/persistord/public/css/api.css docs/templates/persistord/public/css/readme.css docs/templates/persistord/public/css/not-found.css
npx --no-install markdownlint-cli2 docs/index.md
```

Expected: the `git diff` prints nothing, and lint reports 0 issues.

- [ ] **Step 7: Commit**

```bash
git add docs/tools/site-check/tests/keyboard.test.mjs docs/tools/site-check/tests/landing.test.mjs docs/superpowers/specs/2026-09-14-docs-site-revamp-design.md
git commit -m "docs: keyboard and phone-width checks for the landing page; amend the revamp spec

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

(If Steps 4–5 required fixes, commit those first with their own messages.)

---

## After this plan

Hand off with superpowers:finishing-a-development-branch. The PR (base `develop`) description must:

- link the spec and this plan;
- list the six spec section 6 checks with their results, noting that contrast, links, keyboard and motion run inside `npm test`;
- attach the four landing screenshots and note that the rest of the matrix is unchanged;
- include Task 3's **Source facts** table verbatim, which is spec 3.3's edge-by-edge record;
- list the five recorded deviations and point to the spec amendments that absorb them.

The PR 3 plan (information architecture and content enrichment, spec section 4) is written after this PR merges, against what it actually ships.

