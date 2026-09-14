import assert from 'node:assert/strict'
import { after, before, test } from 'node:test'
import { computed, launchSite } from '../lib/site.mjs'

const CLASS_PAGE = 'api/Persistord.Core.UpsertExtensions.html'
const NAMESPACE_PAGE = 'api/Persistord.Core.html'

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

test('cross-references are not underlined at rest', async () => {
  const { page, close } = await site.open(CLASS_PAGE)
  try {
    const link = await computed(page, 'article a.xref', ['text-decoration-line'])
    assert.equal(link['text-decoration-line'], 'none')
  } finally {
    await close()
  }
})

test('every member after the first in a section is ruled off, in the mono face', async () => {
  const { page, close } = await site.open(CLASS_PAGE)
  try {
    const members = await page.$$eval('article h3[data-uid]', headings =>
      headings.map(heading => ({
        border: getComputedStyle(heading).borderTopWidth,
        font: getComputedStyle(heading).fontFamily,
      }))
    )
    assert.ok(members.length >= 2, 'expected at least two members on the class page')
    assert.equal(members[0].border, '0px', 'the first member follows its section heading unruled')
    assert.ok(members.slice(1).every(member => member.border === '1px'), JSON.stringify(members))
    assert.match(members[0].font, /^"Persistord Mono"/)
  } finally {
    await close()
  }
})

test('parameters and returns lay out as a framed definition grid', async () => {
  const { page, close } = await site.open(CLASS_PAGE)
  try {
    const grid = await computed(page, 'article dl.parameters', ['display', 'border-top-width', 'grid-template-columns'])
    assert.equal(grid.display, 'grid')
    assert.equal(grid['border-top-width'], '1px')
    assert.equal(grid['grid-template-columns'].split(' ').length, 2)

    const label = await computed(page, 'article h4.section', ['text-transform'])
    assert.equal(label['text-transform'], 'uppercase')
  } finally {
    await close()
  }
})

test('the facts line sits inline under the title', async () => {
  const { page, close } = await site.open(CLASS_PAGE)
  try {
    assert.equal((await computed(page, 'article .facts', ['display'])).display, 'flex')
  } finally {
    await close()
  }
})

test('namespace pages list types as aligned two-column rows', async () => {
  const { page, close } = await site.open(NAMESPACE_PAGE)
  try {
    const row = await computed(page, 'article dl.jumplist', ['display', 'grid-template-columns'])
    assert.equal(row.display, 'grid')
    assert.equal(row['grid-template-columns'].split(' ').length, 2)

    const section = await computed(page, 'article > h3:not([data-uid])', ['margin-top'])
    assert.ok(parseFloat(section['margin-top']) >= 40, `section heading margin is ${section['margin-top']}`)
  } finally {
    await close()
  }
})

for (const path of [CLASS_PAGE, NAMESPACE_PAGE]) {
  test(`${path} does not overflow horizontally at 375px`, async () => {
    const { page, close } = await site.open(path, { width: 375, height: 800 })
    try {
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
      assert.ok(overflow <= 0, `page overflows by ${overflow}px`)
    } finally {
      await close()
    }
  })
}
