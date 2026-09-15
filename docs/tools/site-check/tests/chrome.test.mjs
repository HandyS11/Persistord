import assert from 'node:assert/strict'
import { after, before, test } from 'node:test'
import { parseColor, sameColor } from '../lib/color.mjs'
import { THEMES, computed, launchSite, tokens } from '../lib/site.mjs'

const ARTICLE = 'articles/upsert.html'

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

const horizontalBox = (page, selector) =>
  page.$eval(selector, element => {
    const rect = element.getBoundingClientRect()
    const style = getComputedStyle(element)
    const viewport = document.documentElement.clientWidth
    return {
      left: rect.left,
      right: viewport - rect.right,
      width: rect.width,
      contentWidth: rect.width - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight),
    }
  })

test('the page shell is capped at 1440px and centred on wide screens', async () => {
  const { page, close } = await site.open(ARTICLE, { width: 1920 })
  try {
    const box = await horizontalBox(page, 'body > main')
    assert.ok(box.width <= 1440, `shell is ${box.width}px wide`)
    assert.ok(Math.abs(box.left - box.right) <= 2, `shell is off-centre: ${box.left}px / ${box.right}px`)
  } finally {
    await close()
  }
})

test('the landing page content sits in a centred 1120px column', async () => {
  const { page, close } = await site.open('index.html')
  try {
    const box = await horizontalBox(page, 'body > main > .content > article')
    assert.ok(box.contentWidth <= 1120, `landing column is ${box.contentWidth}px wide`)
    assert.ok(Math.abs(box.left - box.right) <= 2, `landing column is off-centre: ${box.left}px / ${box.right}px`)
  } finally {
    await close()
  }
})

test('the brand mark and wordmark are separated', async () => {
  const { page, close } = await site.open(ARTICLE)
  try {
    const gap = await page.$eval('.navbar-brand', brand => {
      const logo = brand.querySelector('#logo')
      const text = [...brand.childNodes].find(node => node.nodeType === Node.TEXT_NODE && node.textContent.trim())
      const start = text.textContent.indexOf(text.textContent.trim())
      const range = document.createRange()
      range.setStart(text, start)
      range.setEnd(text, start + 1)
      return range.getBoundingClientRect().left - logo.getBoundingClientRect().right
    })
    assert.ok(gap >= 8, `brand gap is ${gap}px`)
  } finally {
    await close()
  }
})

test('the active navbar tab is underlined with the gradient', async () => {
  const { page, close } = await site.open(ARTICLE)
  try {
    await page.waitForSelector('#navbar .nav-link.active')
    const after = await computed(page, '#navbar .nav-link.active', ['background-image', 'height'], '::after')
    assert.match(after['background-image'], /linear-gradient/)
    assert.equal(after.height, '2px')
  } finally {
    await close()
  }
})

test('the header is translucent over a backdrop blur', async () => {
  const { page, close } = await site.open(ARTICLE)
  try {
    const header = await computed(page, 'body > header', ['background-color', 'backdrop-filter'])
    assert.ok(parseColor(header['background-color'])[3] < 1, `header background is ${header['background-color']}`)
    assert.match(header['backdrop-filter'], /blur/)
  } finally {
    await close()
  }
})

test('pressing / focuses search, but not while typing in another field', async () => {
  const { page, close } = await site.open(ARTICLE)
  try {
    await page.waitForSelector('#search-query:not([disabled])', { timeout: 15000 })
    await page.waitForSelector('.toc form.filter input')

    await page.keyboard.press('/')
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'search-query')

    await page.focus('.toc form.filter input')
    await page.keyboard.press('/')
    assert.equal(await page.evaluate(() => document.activeElement?.matches('.toc form.filter input')), true)
  } finally {
    await close()
  }
})

test('sidebar group labels are small uppercase captions and the sidebar is 260px', async () => {
  const { page, close } = await site.open(ARTICLE)
  try {
    await page.waitForSelector('#toc span.name-only')
    const label = await computed(page, '#toc span.name-only', ['text-transform', 'font-size'])
    assert.equal(label['text-transform'], 'uppercase')
    assert.ok(parseFloat(label['font-size']) <= 12, `label is ${label['font-size']}`)

    const width = await page.$eval('main > .toc-offcanvas', element => element.getBoundingClientRect().width)
    assert.ok(Math.abs(width - 260) <= 1, `sidebar is ${width}px`)
  } finally {
    await close()
  }
})

test('the current page is marked in the sidebar with the gradient bar', async () => {
  const { page, close } = await site.open(ARTICLE)
  try {
    const current = '#toc li.active:not(:has(li.active)) > a'
    await page.waitForSelector(current)
    const bar = await computed(page, current, ['background-image', 'width'], '::before')
    assert.match(bar['background-image'], /linear-gradient/)
    assert.equal(bar.width, '3px')
  } finally {
    await close()
  }
})

test('the in-page rail highlights the section being read', async () => {
  const { page, close } = await site.open(ARTICLE)
  try {
    await page.waitForSelector('#affix li:nth-child(2) > a')
    assert.equal((await computed(page, '#affix ul', ['border-left-width']))['border-left-width'], '1px')

    const hash = await page.$eval('#affix li:nth-child(2) > a', link => link.hash)
    await page.evaluate(id => document.getElementById(id).scrollIntoView({ block: 'start' }), decodeURIComponent(hash.slice(1)))
    await page.waitForFunction(expected => document.querySelector('#affix a.pd-current')?.hash === expected, hash)
    assert.equal(await page.$eval('#affix a.pd-current', link => link.getAttribute('aria-current')), 'location')
  } finally {
    await close()
  }
})

test('the breadcrumb is a small caption above the title', async () => {
  const { page, close } = await site.open(ARTICLE)
  try {
    await page.waitForSelector('#breadcrumb .breadcrumb')
    const crumb = await computed(page, '#breadcrumb .breadcrumb', ['font-size'])
    assert.ok(parseFloat(crumb['font-size']) <= 13, `breadcrumb is ${crumb['font-size']}`)
  } finally {
    await close()
  }
})

test('previous and next render as whole-card links', async () => {
  const { page, close } = await site.open(ARTICLE)
  try {
    await page.waitForSelector('.next-article > div.next > a')
    const card = await computed(page, '.next-article > div.next', ['border-top-width', 'border-top-left-radius'])
    assert.equal(card['border-top-width'], '1px')
    assert.equal(card['border-top-left-radius'], '12px')

    /* A raw mouse click on the caption: Playwright's element click would refuse,
       because the stretched link's ::after (the point of the test) covers it.
       locator.boundingBox() never scrolls the target into view (only element
       actions like click() do), and upsert.html is far taller than the default
       viewport, so the card is scrolled into view explicitly first. */
    const href = await page.$eval('.next-article > div.next > a', link => link.href)
    await page.locator('.next-article > div.next > span').scrollIntoViewIfNeeded()
    const caption = await page.locator('.next-article > div.next > span').boundingBox()
    await page.mouse.click(caption.x + caption.width / 2, caption.y + caption.height / 2)
    await page.waitForURL(href)
  } finally {
    await close()
  }
})

test('on a phone, previous and next stack as full-width cards', async () => {
  const { page, close } = await site.open(ARTICLE, { width: 375 })
  try {
    await page.waitForSelector('.next-article > div.prev')
    await page.waitForSelector('.next-article > div.next')
    const { row, cards } = await page.$eval('.next-article', element => ({
      row: element.getBoundingClientRect().width,
      cards: [...element.children].map(card => [card.className, card.getBoundingClientRect().width]),
    }))
    for (const [name, width] of cards) {
      assert.ok(Math.abs(width - row) <= 1, `${name} card is ${width}px of a ${row}px row`)
    }
  } finally {
    await close()
  }
})

for (const theme of THEMES) {
  test(`${theme}: the search and sidebar filter fields are bounded by the control border`, async () => {
    const { page, close } = await site.open(ARTICLE, { theme })
    try {
      await page.waitForSelector('.toc form.filter > input')
      const { 'control-border': border } = await tokens(page, ['control-border'])
      for (const selector of ['#search-query', '.toc form.filter > input']) {
        const field = await computed(page, selector, ['border-top-color'])
        assert.ok(sameColor(parseColor(field['border-top-color']), parseColor(border)), `${selector} border is ${field['border-top-color']}`)
      }
    } finally {
      await close()
    }
  })

  test(`${theme}: the edit link is muted`, async () => {
    const { page, close } = await site.open(ARTICLE, { theme })
    try {
      const { muted } = await tokens(page, ['muted'])
      const link = await computed(page, '.contribution a.edit-link', ['color'])
      assert.ok(sameColor(parseColor(link.color), parseColor(muted)), `edit link is ${link.color}`)
    } finally {
      await close()
    }
  })
}

test('the footer has three link groups whose links resolve from every depth', async () => {
  for (const path of ['index.html', ARTICLE, 'src/Persistord.Core/README.html']) {
    const { page, close } = await site.open(path)
    try {
      assert.equal(await page.locator('footer .pd-footer-group').count(), 3, `${path}: footer groups`)
      const hrefs = await page.$$eval('footer .pd-footer a', links =>
        links.map(link => link.href).filter(href => href.startsWith(location.origin))
      )
      assert.ok(hrefs.length >= 6, `${path}: only ${hrefs.length} internal footer links`)
      for (const href of hrefs) {
        const response = await fetch(href)
        assert.equal(response.status, 200, `${path}: footer link ${href} answered ${response.status}`)
      }
    } finally {
      await close()
    }
  }
})

/* docfx pins the footer to 60px, and repeats the rule for the landing layout
   with a more specific selector; a footer that keeps that height paints its
   background behind the first row only. */
test('the footer grows to hold its content on every layout', async () => {
  for (const path of ['index.html', ARTICLE]) {
    const { page, close } = await site.open(path)
    try {
      const overhang = await page.$eval(
        'body > footer',
        footer => footer.querySelector('.pd-footer').getBoundingClientRect().bottom - footer.getBoundingClientRect().bottom
      )
      assert.ok(overhang <= 0, `${path}: footer content runs ${overhang}px past the footer`)
    } finally {
      await close()
    }
  }
})
