import assert from 'node:assert/strict'
import { readdir, readFile } from 'node:fs/promises'
import { after, before, test } from 'node:test'
import { contrast, parseColor } from '../lib/color.mjs'
import { THEMES, launchSite, tokens } from '../lib/site.mjs'

const CSS_DIR = new URL('../../../templates/persistord/public/css/', import.meta.url)

const stripComments = css => css.replace(/\/\*[\s\S]*?\*\//g, '')

async function themeStylesheets() {
  const names = (await readdir(CSS_DIR)).filter(name => name.endsWith('.css'))
  return Promise.all(
    names.map(async name => ({
      name,
      css: stripComments(await readFile(new URL(name, CSS_DIR), 'utf8')),
    }))
  )
}

/* WCAG AA: 4.5:1 for text, 3:1 for the boundary of a UI component. */
const TEXT = 4.5
const BOUNDARY = 3

/* [foreground, background, minimum] - every pairing text or a control
   boundary is actually set in. */
const TOKEN_PAIRS = [
  ['heading', 'page', TEXT],
  ['text', 'page', TEXT],
  ['text', 'surface', TEXT],
  ['text', 'raised', TEXT],
  ['muted', 'page', TEXT],
  ['muted', 'surface', TEXT],
  ['muted', 'raised', TEXT],
  ['accent', 'page', TEXT],
  ['accent', 'surface', TEXT],
  ['accent', 'raised', TEXT],
  ['control-border', 'page', BOUNDARY],
  ['control-border', 'surface', BOUNDARY],
]

const GRADIENT_USES = new Set([
  '#navbar .navbar-nav .nav-link.active::after',
  '.toc li.active:not(:has(li.active)) > a::before',
  '.pd-btn-primary',
  '.pd-wordmark',
])

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

for (const theme of THEMES) {
  test(`${theme}: text, muted, accent, and control-border tokens meet WCAG AA on every surface`, async () => {
    const { page, close } = await site.open('development/components.html', { theme })
    try {
      const names = [...new Set(TOKEN_PAIRS.flatMap(([fg, bg]) => [fg, bg]))]
      const values = await tokens(page, names)
      for (const [fg, bg, minimum] of TOKEN_PAIRS) {
        assert.ok(values[fg], `${theme}: --pd-${fg} is not declared`)
        const ratio = contrast(parseColor(values[fg]), parseColor(values[bg]))
        assert.ok(ratio >= minimum, `${theme}: --pd-${fg} on --pd-${bg} is ${ratio.toFixed(2)}:1, needs ${minimum}:1`)
      }
    } finally {
      await close()
    }
  })

  test(`${theme}: every gradient stop holds contrast in its role`, async () => {
    const { page, close } = await site.open('development/components.html', { theme })
    try {
      const values = await tokens(page, ['gradient-fill', 'gradient-ink', 'on-fill', 'page'])
      const stops = value => value.match(/#[0-9a-f]{6}/gi) ?? []

      assert.ok(stops(values['gradient-fill']).length >= 2, '--pd-gradient-fill has no stops')
      for (const stop of stops(values['gradient-fill'])) {
        const ratio = contrast(parseColor(values['on-fill']), parseColor(stop))
        assert.ok(ratio >= 4.5, `${theme}: --pd-on-fill on fill stop ${stop} is ${ratio.toFixed(2)}:1`)
      }

      assert.ok(stops(values['gradient-ink']).length >= 2, '--pd-gradient-ink has no stops')
      for (const stop of stops(values['gradient-ink'])) {
        const ratio = contrast(parseColor(stop), parseColor(values.page))
        assert.ok(ratio >= 4.5, `${theme}: ink stop ${stop} on --pd-page is ${ratio.toFixed(2)}:1`)
      }
    } finally {
      await close()
    }
  })
}

test('no stylesheet except tokens.css hard-codes a colour', async () => {
  const offenders = []
  for (const { name, css } of await themeStylesheets()) {
    if (name === 'tokens.css') {
      continue
    }
    for (const match of css.matchAll(/:[^;{}]*?(#[0-9a-f]{3,8}\b|\b(?:rgba?|hsla?)\()/gi)) {
      offenders.push(`${name}: ${match[0].trim()}`)
    }
  }
  assert.deepEqual(offenders, [])
})

test('gradient tokens are consumed only by the sanctioned elements', async () => {
  const unsanctioned = []
  for (const { name, css } of await themeStylesheets()) {
    for (const block of css.split('}')) {
      const parts = block.split('{')
      if (parts.length < 2 || !parts.at(-1).includes('var(--pd-gradient-')) {
        continue
      }
      for (const selector of parts.at(-2).split(',')) {
        const normalised = selector.trim().replace(/\s+/g, ' ')
        if (!GRADIENT_USES.has(normalised)) {
          unsanctioned.push(`${name}: ${normalised}`)
        }
      }
    }
  }
  assert.deepEqual(unsanctioned, [])
})
