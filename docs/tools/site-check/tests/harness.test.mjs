import assert from 'node:assert/strict'
import { after, before, test } from 'node:test'
import { contrast, parseColor } from '../lib/color.mjs'
import { launchSite } from '../lib/site.mjs'

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

test('contrast maths matches the WCAG reference values', () => {
  assert.equal(contrast(parseColor('#000000'), parseColor('#ffffff')).toFixed(1), '21.0')
  assert.equal(contrast(parseColor('#767676'), parseColor('#ffffff')).toFixed(2), '4.54')
})

test('parses every colour syntax getComputedStyle returns', () => {
  assert.deepEqual(parseColor('rgb(255, 0, 51)'), [1, 0, 0.2, 1])
  assert.deepEqual(parseColor('rgba(0, 0, 0, 0.5)'), [0, 0, 0, 0.5])
  assert.deepEqual(parseColor('rgb(255 255 255 / 0.25)'), [1, 1, 1, 0.25])
  assert.deepEqual(parseColor('color(srgb 0.5 0.25 1)'), [0.5, 0.25, 1, 1])
})

test('serves the built site under the GitHub Pages prefix', async () => {
  const { page, response, close } = await site.open('articles/introduction.html')
  try {
    assert.equal(response.status(), 200)
    assert.match(await page.title(), /^Introduction/)
  } finally {
    await close()
  }
})

test('answers a missing path with status 404, as GitHub Pages does', async () => {
  const response = await fetch(site.server.url('articles/missing/deeper.html'))
  assert.equal(response.status, 404)
})

test('a malformed percent-encoded path still answers 404, not a dropped connection', async () => {
  const response = await fetch(site.server.url('articles/%E0%A4%A.html'))
  assert.equal(response.status, 404)

  const followUp = await fetch(site.server.url('articles/introduction.html'))
  assert.equal(followUp.status, 200)
})

test('the component reference page exercises every styled component', async () => {
  const { page, close } = await site.open('development/components.html')
  try {
    await page.waitForSelector('pre.mermaid svg')
    const required = [
      '.alert.NOTE',
      '.alert.TIP',
      '.alert.IMPORTANT',
      '.alert.WARNING',
      '.alert.CAUTION',
      '.tabGroup [role="tab"]',
      '.table-responsive > table',
      'pre > code.lang-csharp',
      'pre > code.lang-json',
      'pre > code.lang-bash',
      'article blockquote',
      'article h3',
      'article h4',
    ]
    for (const selector of required) {
      assert.ok(await page.$(selector), `components page is missing ${selector}`)
    }
    assert.equal(await page.locator('pre.mermaid svg').count(), 3)
  } finally {
    await close()
  }
})
