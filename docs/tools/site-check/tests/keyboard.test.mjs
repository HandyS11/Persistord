import assert from 'node:assert/strict'
import { after, before, test } from 'node:test'
import { contrast, parseColor } from '../lib/color.mjs'
import { THEMES, computed, launchSite, tokens } from '../lib/site.mjs'

/* An article with the in-page rail and prev/next, and the component page,
   which adds copy buttons and a tab group. */
const PAGES = ['articles/upsert.html', 'development/components.html']

/* Far more stops than either page has; the walk ends at the footer. */
const MAX_TABS = 400

/* Groups the walk must reach, so a walk that stalls in the navigation cannot
   pass vacuously. A group is required only when the page renders it. */
const MUST_REACH = {
  'an article link': '.content article a',
  'a copy button': '.content article pre > .code-action',
  'a tab': '.content article .tabGroup [role="tab"]',
  'a previous/next link': '.next-article a',
  'a footer link': 'footer a',
}

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

for (const theme of THEMES) {
  for (const path of PAGES) {
    test(`${theme}: ${path} shows the theme focus indicator on every Tab stop through to the footer`, async () => {
      const { page, close } = await site.open(path, { theme })
      try {
        await page.waitForSelector('#toc li a')
        await page.waitForSelector('.next-article a')
        await page.waitForSelector('footer a')
        if (await page.$('.content article pre > code')) {
          await page.waitForSelector('.code-action', { state: 'attached' })
        }

        const { page: pageColor } = await tokens(page, ['page'])
        const background = parseColor(pageColor)
        const present = {}
        for (const [name, selector] of Object.entries(MUST_REACH)) {
          present[name] = (await page.$(selector)) !== null
        }

        const failures = []
        const reached = new Set()
        let finished = false
        for (let i = 0; i < MAX_TABS && !finished; i++) {
          await page.keyboard.press('Tab')
          const state = await page.evaluate(groups => {
            const element = document.activeElement
            if (!element || element === document.body) {
              return null
            }
            window.pdSeen ??= new Set()
            const wrapped = window.pdSeen.has(element)
            window.pdSeen.add(element)

            const footerLinks = document.querySelectorAll('footer a')
            const style = getComputedStyle(element)
            const text = (element.textContent || element.getAttribute('aria-label') || '').trim().slice(0, 40)
            return {
              wrapped,
              last: element === footerLinks[footerLinks.length - 1],
              control: element.matches('input, select, textarea'),
              outlineStyle: style.outlineStyle,
              outlineWidth: parseFloat(style.outlineWidth),
              outlineColor: style.outlineColor,
              boxShadow: style.boxShadow,
              groups: Object.entries(groups)
                .filter(([, selector]) => element.matches(selector))
                .map(([name]) => name),
              label: `<${element.tagName.toLowerCase()} class="${element.className}"> ${text}`,
            }
          }, MUST_REACH)

          if (!state) {
            continue
          }
          if (state.wrapped) {
            break
          }
          state.groups.forEach(name => reached.add(name))
          finished = state.last

          const outlined =
            !['none', 'auto'].includes(state.outlineStyle) &&
            state.outlineWidth >= 2 &&
            contrast(parseColor(state.outlineColor), background) >= 3
          const shadowed = state.control && state.boxShadow !== 'none'
          if (!outlined && !shadowed) {
            failures.push(`${state.label} (outline ${state.outlineStyle} ${state.outlineWidth}px ${state.outlineColor}, shadow ${state.boxShadow})`)
          }
        }

        assert.deepEqual(failures, [], `${theme} ${path}: Tab stops without a theme focus indicator`)
        for (const [name, isPresent] of Object.entries(present)) {
          if (isPresent) {
            assert.ok(reached.has(name), `${theme} ${path}: the Tab walk never reached ${name}`)
          }
        }
      } finally {
        await close()
      }
    })
  }
}

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
