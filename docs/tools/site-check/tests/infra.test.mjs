import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { join } from 'node:path'
import { after, before, test } from 'node:test'
import { parseColor, sameColor } from '../lib/color.mjs'
import { SITE_ROOT } from '../lib/server.mjs'
import { THEMES, computed, launchSite, tokens } from '../lib/site.mjs'

/* Pages with no front matter of their own, described through fileMetadata. */
const DESCRIBED_BY_METADATA = [
  'api/index.html',
  'api/Persistord.Core.UpsertExtensions.html',
  'src/Persistord.Core/README.html',
  'samples/README.html',
  'license.html',
]

/* Pages whose front matter carries a description. */
const DESCRIBED_BY_FRONT_MATTER = ['development/index.html', 'development/components.html']

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

test('the sitemap lists absolute URLs under the Pages base', async () => {
  const xml = await readFile(join(SITE_ROOT, 'sitemap.xml'), 'utf8')
  assert.match(xml, /<loc>https:\/\/handys11\.github\.io\/Persistord\/articles\/getting-started\.html<\/loc>/)
})

test('pages declare English as their language', async () => {
  for (const path of ['articles/upsert.html', 'api/Persistord.Core.html', 'src/Persistord.Core/README.html']) {
    const { page, close } = await site.open(path)
    try {
      assert.equal(await page.getAttribute('html', 'lang'), 'en', path)
    } finally {
      await close()
    }
  }
})

test('described pages carry exactly one meta description', async () => {
  for (const path of [...DESCRIBED_BY_METADATA, ...DESCRIBED_BY_FRONT_MATTER]) {
    const { page, close } = await site.open(path)
    try {
      assert.equal(await page.locator('meta[name="description"]').count(), 1, path)
    } finally {
      await close()
    }
  }
})

test('no page carries two meta descriptions', async () => {
  for (const path of ['index.html', 'articles/upsert.html', 'articles/getting-started.html']) {
    const { page, close } = await site.open(path)
    try {
      assert.ok((await page.locator('meta[name="description"]').count()) <= 1, path)
    } finally {
      await close()
    }
  }
})

for (const theme of THEMES) {
  test(`${theme}: a deep missing path renders the fully styled 404`, async () => {
    const { page, response, close } = await site.open('articles/missing/deeper.html', { theme })
    try {
      assert.equal(response.status(), 404)
      assert.equal(await page.getAttribute('html', 'data-bs-theme'), theme)
      assert.match(await page.textContent('h1'), /isn.t in the model/)

      const body = await computed(page, 'body', ['font-family', 'background-color'])
      assert.match(body['font-family'], /^"Persistord Sans"/)
      const { page: pageToken } = await tokens(page, ['page'])
      assert.ok(sameColor(parseColor(body['background-color']), parseColor(pageToken)), `404 body is ${body['background-color']}`)

      for (const href of ['/Persistord/', '/Persistord/articles/introduction.html', '/Persistord/articles/packages.html', '/Persistord/api/index.html']) {
        assert.ok(await page.$(`a[href="${href}"]`), `404 is missing a link to ${href}`)
        const linked = await fetch(`${site.server.origin}${href}`)
        assert.equal(linked.status, 200, `404 link ${href} answered ${linked.status}`)
      }
    } finally {
      await close()
    }
  })
}
