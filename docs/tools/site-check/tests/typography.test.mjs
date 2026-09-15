import assert from 'node:assert/strict'
import { after, before, test } from 'node:test'
import { computed, launchSite } from '../lib/site.mjs'

const PAGE = 'development/components.html'

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

test('article headings follow the fixed type scale', async () => {
  const { page, close } = await site.open(PAGE)
  try {
    const sizes = {}
    for (const tag of ['h1', 'h2', 'h3', 'h4']) {
      sizes[tag] = (await computed(page, `.content article ${tag}`, ['font-size']))['font-size']
    }
    assert.deepEqual(sizes, { h1: '36px', h2: '24px', h3: '19.2px', h4: '16px' })

    const h1 = await computed(page, '.content article h1', ['font-weight', 'letter-spacing'])
    assert.equal(h1['font-weight'], '700')
    assert.equal(h1['letter-spacing'], '-0.72px')
  } finally {
    await close()
  }
})

test('body text uses the vendored sans at 16px with a 1.7 line height', async () => {
  const { page, close } = await site.open(PAGE)
  try {
    const body = await computed(page, 'body', ['font-family', 'font-size', 'line-height'])
    assert.match(body['font-family'], /^"Persistord Sans"/)
    assert.equal(body['font-size'], '16px')
    assert.equal(body['line-height'], '27.2px')
    assert.equal(await page.evaluate(() => document.fonts.check('16px "Persistord Sans"')), true)
  } finally {
    await close()
  }
})

test('code is set without ligatures, so => and -> read as typed', async () => {
  const { page, close } = await site.open(PAGE)
  try {
    for (const selector of ['.content article pre > code', '.content article p > code']) {
      const code = await computed(page, selector, ['font-variant-ligatures'])
      assert.equal(code['font-variant-ligatures'], 'none', selector)
    }
  } finally {
    await close()
  }
})

for (const scheme of ['light', 'dark']) {
  test(`with no stored choice the theme follows an OS ${scheme} preference`, async () => {
    const { page, close } = await site.open(PAGE, { theme: null, colorScheme: scheme })
    try {
      await page.waitForFunction(
        expected => document.documentElement.dataset.bsTheme === expected,
        scheme,
        { timeout: 5000 }
      )
      assert.equal(await page.evaluate(() => document.documentElement.dataset.bsTheme), scheme)
    } finally {
      await close()
    }
  })
}
