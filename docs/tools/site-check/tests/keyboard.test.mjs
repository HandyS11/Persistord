import assert from 'node:assert/strict'
import { after, before, test } from 'node:test'
import { computed, launchSite } from '../lib/site.mjs'

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

test('every element reached by Tab shows a focus indicator', async () => {
  const { page, close } = await site.open('articles/upsert.html')
  try {
    await page.waitForSelector('#toc li a')
    await page.waitForSelector('#affix a')

    const missing = []
    for (let i = 0; i < 40; i++) {
      await page.keyboard.press('Tab')
      const state = await page.evaluate(() => {
        const element = document.activeElement
        if (!element || element === document.body) {
          return null
        }
        const style = getComputedStyle(element)
        const outlined = style.outlineStyle !== 'none' && parseFloat(style.outlineWidth) > 0
        const text = (element.textContent || element.getAttribute('aria-label') || '').trim().slice(0, 40)
        return {
          visible: outlined || style.boxShadow !== 'none',
          label: `<${element.tagName.toLowerCase()} class="${element.className}"> ${text}`,
        }
      })
      if (state && !state.visible) {
        missing.push(state.label)
      }
    }
    assert.deepEqual(missing, [])
  } finally {
    await close()
  }
})

test('the landing page animates only when motion is allowed', async () => {
  const reduced = await site.open('index.html', { reducedMotion: 'reduce' })
  try {
    const style = await computed(reduced.page, '.pd-transform .pd-snowflake', ['animation-name'])
    assert.equal(style['animation-name'], 'none')
  } finally {
    await reduced.close()
  }

  const allowed = await site.open('index.html', { reducedMotion: 'no-preference' })
  try {
    const style = await computed(allowed.page, '.pd-transform .pd-snowflake', ['animation-name'])
    assert.equal(style['animation-name'], 'pd-carry')
  } finally {
    await allowed.close()
  }
})
