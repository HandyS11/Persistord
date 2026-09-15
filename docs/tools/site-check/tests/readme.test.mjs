import assert from 'node:assert/strict'
import { readdir, readFile } from 'node:fs/promises'
import { after, before, test } from 'node:test'
import { computed, launchSite } from '../lib/site.mjs'

const REPO = new URL('../../../../', import.meta.url)
const README = 'src/Persistord.Core/README.html'
const SITE_LINK = '[Documentation site](https://handys11.github.io/Persistord/)'

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

test('README badges are left-aligned in a single row', async () => {
  const { page, close } = await site.open(README)
  try {
    const header = await computed(page, 'article div[align="center"]', ['text-align'])
    assert.equal(header['text-align'], 'left')
    const badges = await computed(page, 'article div[align="center"] > p:first-child', ['display', 'flex-wrap'])
    assert.equal(badges.display, 'flex')
  } finally {
    await close()
  }
})

test('the README back-link to this site is hidden on the site', async () => {
  const { page, close } = await site.open(README)
  try {
    const display = await page.$eval('article a[href="https://handys11.github.io/Persistord/"]', link =>
      getComputedStyle(link.closest('p')).display
    )
    assert.equal(display, 'none')
  } finally {
    await close()
  }
})

test('every package README source still carries the back-link', async () => {
  const packages = await readdir(new URL('src/', REPO))
  const missing = []
  for (const name of packages) {
    const text = await readFile(new URL(`src/${name}/README.md`, REPO), 'utf8').catch(() => null)
    if (text !== null && !text.includes(SITE_LINK)) {
      missing.push(name)
    }
  }
  assert.deepEqual(missing, [])
})

test('no hand-written page uses the README header pattern the hide rule targets', async () => {
  const offenders = []
  for (const directory of ['docs/', 'docs/articles/', 'docs/development/']) {
    for (const name of await readdir(new URL(directory, REPO))) {
      if (!name.endsWith('.md')) {
        continue
      }
      const text = await readFile(new URL(`${directory}${name}`, REPO), 'utf8')
      if (text.includes('align="center"')) {
        offenders.push(`${directory}${name}`)
      }
    }
  }
  assert.deepEqual(offenders, [])
})

test('sidebar group labels carry no emoji', async () => {
  for (const path of ['articles/upsert.html', README, 'samples/README.html']) {
    const { page, close } = await site.open(path)
    try {
      await page.waitForSelector('#toc span.name-only')
      const labels = await page.$$eval('#toc span.name-only', spans => spans.map(span => span.textContent.trim()))
      const withEmoji = labels.filter(label => /\p{Extended_Pictographic}/u.test(label))
      assert.deepEqual(withEmoji, [], `${path} sidebar labels`)
    } finally {
      await close()
    }
  }
})
