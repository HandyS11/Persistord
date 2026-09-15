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
