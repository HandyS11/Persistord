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
