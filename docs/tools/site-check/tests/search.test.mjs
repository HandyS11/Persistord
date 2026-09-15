import assert from 'node:assert/strict'
import { after, before, test } from 'node:test'
import { contrast, parseColor, sameColor } from '../lib/color.mjs'
import { THEMES, computed, launchSite, tokens } from '../lib/site.mjs'

const ARTICLE = 'articles/upsert.html'

/* Matches dozens of pages; docfx shows ten per page, so the pager renders. */
const QUERY = 'message'

const RESULT_TEXT = [
  '#search-results > .search-list',
  '#search-results .sr-item > .item-title a',
  '#search-results .sr-item > .item-href',
  '#search-results .sr-item > .item-brief',
]

async function search(page, query) {
  await page.waitForSelector('#search-query:not([disabled])', { timeout: 15000 })
  await page.focus('#search-query')
  await page.keyboard.type(query)
  await page.waitForSelector('#search-results .sr-item', { timeout: 15000 })
}

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

for (const theme of THEMES) {
  test(`${theme}: search results are readable and set on the type scale`, async () => {
    const { page, close } = await site.open(ARTICLE, { theme })
    try {
      await search(page, QUERY)
      const values = await tokens(page, ['page', 'heading'])
      const background = parseColor(values.page)

      for (const selector of RESULT_TEXT) {
        const { color } = await computed(page, selector, ['color'])
        const ratio = contrast(parseColor(color), background)
        assert.ok(ratio >= 4.5, `${theme} ${selector} is ${ratio.toFixed(2)}:1 on the page`)
      }

      const title = '#search-results .sr-item > .item-title a'
      const rest = await computed(page, title, ['color', 'font-size', 'text-decoration-line'])
      assert.ok(sameColor(parseColor(rest.color), parseColor(values.heading)), `result title is ${rest.color}`)
      assert.equal(rest['font-size'], '19.2px')
      assert.equal(rest['text-decoration-line'], 'none')

      await page.hover(title)
      assert.equal((await computed(page, title, ['text-decoration-line']))['text-decoration-line'], 'underline')
    } finally {
      await close()
    }
  })

  test(`${theme}: the search pager takes the accent, not Bootstrap blue`, async () => {
    const { page, close } = await site.open(ARTICLE, { theme })
    try {
      await search(page, QUERY)
      await page.waitForSelector('#search-results .pagination .page-link.active')
      const { accent } = await tokens(page, ['accent'])

      const active = await computed(page, '#search-results .page-link.active', ['background-color', 'border-top-color', 'color'])
      assert.ok(sameColor(parseColor(active['background-color']), parseColor(accent)), `active page is ${active['background-color']}`)
      assert.ok(sameColor(parseColor(active['border-top-color']), parseColor(accent)), `active page border is ${active['border-top-color']}`)
      const activeRatio = contrast(parseColor(active.color), parseColor(active['background-color']))
      assert.ok(activeRatio >= 4.5, `${theme} active page number is ${activeRatio.toFixed(2)}:1`)

      const other = '#search-results .page-link:not(.active):not(.disabled)'
      const idle = await computed(page, other, ['background-color', 'color'])
      const idleRatio = contrast(parseColor(idle.color), parseColor(idle['background-color']))
      assert.ok(idleRatio >= 4.5, `${theme} page number is ${idleRatio.toFixed(2)}:1`)

      await page.focus(other)
      const focused = await computed(page, other, ['outline-style', 'outline-color', 'box-shadow'])
      assert.equal(focused['outline-style'], 'solid')
      assert.ok(sameColor(parseColor(focused['outline-color']), parseColor(accent)), `focused page outline is ${focused['outline-color']}`)
      assert.doesNotMatch(focused['box-shadow'], /13, 110, 253/)
    } finally {
      await close()
    }
  })

  /* docfx renders the message one element below .toc, so its own
     `.toc > .no-result` rule never matched and the text fell back to body
     colour at body size. */
  test(`${theme}: a sidebar filter with no match says so in muted caption text`, async () => {
    const { page, close } = await site.open(ARTICLE, { theme })
    try {
      await page.waitForSelector('.toc form.filter input')
      await page.fill('.toc form.filter input', 'zzqxv')
      await page.waitForSelector('.toc .no-result')
      const values = await tokens(page, ['page', 'muted'])
      const message = await computed(page, '.toc .no-result', ['color', 'font-size'])
      assert.ok(sameColor(parseColor(message.color), parseColor(values.muted)), `no-result message is ${message.color}`)
      assert.equal(message['font-size'], '14px')
      const ratio = contrast(parseColor(message.color), parseColor(values.page))
      assert.ok(ratio >= 4.5, `${theme} .toc .no-result is ${ratio.toFixed(2)}:1 on the page`)
    } finally {
      await close()
    }
  })
}
