import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
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

for (const width of [1440, 1024, 901, 375]) {
  test(`the proof panes fit their content without a horizontal scroll at ${width}px`, async () => {
    const { page, close } = await site.open(LANDING, { width })
    try {
      const panes = await page.$$eval('.pd-pane pre', pres =>
        pres.map(pre => ({ scrollWidth: pre.scrollWidth, clientWidth: pre.clientWidth }))
      )
      assert.ok(panes.length > 0, 'no proof panes found')
      panes.forEach((pane, i) => {
        assert.ok(pane.scrollWidth <= pane.clientWidth, `pane ${i + 1} clips at ${width}px: scrollWidth ${pane.scrollWidth} > clientWidth ${pane.clientWidth}`)
      })
    } finally {
      await close()
    }
  })
}

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

test('the only .pd-card elements on the page are the three Start-here cards, and retired blocks are gone', async () => {
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
        .filter(([text]) => /^Persistord(?:\.[A-Za-z]+)*$/.test(text))
    )
    assert.ok(named.length >= 9, `only ${named.length} package names found`)
    const unlinked = named.filter(([name, href]) => href !== `src/${name}/README.html`).map(([name, href]) => `${name} -> ${href}`)
    assert.deepEqual(unlinked, [])
  } finally {
    await close()
  }
})

test('the meta package Persistord is named in code and links to its README page', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const meta = await page.$eval('.pd-stack .pd-step:first-child', step => {
      const code = [...step.querySelectorAll('p code')].find(candidate => candidate.textContent.trim() === 'Persistord')
      return { found: Boolean(code), href: code?.closest('a')?.getAttribute('href') ?? null }
    })
    assert.ok(meta.found, 'step 1 never names Persistord in <code>')
    assert.equal(meta.href, 'src/Persistord/README.html')
  } finally {
    await close()
  }
})

test('every class landing.css styles is used by the page, 404.html or main.js', async () => {
  const docs = new URL('../../../', import.meta.url)
  const css = (await readFile(new URL('templates/persistord/public/css/landing.css', docs), 'utf8')).replace(/\/\*[\s\S]*?\*\//g, '')
  const used =
    (await readFile(new URL('index.md', docs), 'utf8')) +
    (await readFile(new URL('404.html', docs), 'utf8')) +
    (await readFile(new URL('templates/persistord/public/main.js', docs), 'utf8'))
  const unused = [...new Set([...css.matchAll(/\.(pd-[a-z0-9-]+)/g)].map(([, name]) => name))].filter(
    name => !new RegExp(`(?<![\\w-])${name}(?![\\w-])`).test(used)
  )
  assert.deepEqual(unused, [])
})

test('every install strip command fits without clipping at 375px', async () => {
  const { page, close } = await site.open(LANDING, { width: 375 })
  try {
    const overflow = selector =>
      page.$eval(selector, code => ({ scrollWidth: code.scrollWidth, clientWidth: code.clientWidth }))
    const assertFits = ({ scrollWidth, clientWidth }, label) =>
      assert.ok(scrollWidth <= clientWidth, `${label} clips: scrollWidth ${scrollWidth} > clientWidth ${clientWidth}`)

    assertFits(await overflow('.pd-hero .pd-install > code'), 'hero strip')
    assertFits(await overflow('.pd-stack .pd-step:first-child .pd-install > code'), 'step-1 strip')

    await page.waitForSelector('[data-pd-tabs] [role="tab"]')
    const tabs = await page.$$('[data-pd-tabs] [role="tab"]')
    assert.ok(tabs.length > 0, 'no adapter tabs found')
    for (const tab of tabs) {
      const panelId = await tab.getAttribute('aria-controls')
      await tab.click()
      assertFits(await overflow(`#${panelId} .pd-install > code`), `${panelId} strip`)
    }
  } finally {
    await close()
  }
})

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
