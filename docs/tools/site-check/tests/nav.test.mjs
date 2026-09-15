import assert from 'node:assert/strict'
import { after, before, test } from 'node:test'
import { launchSite } from '../lib/site.mjs'

/* Spec 4.2: the Docs sidebar, by what the reader is trying to do. */
const SIDEBAR = [
  ['Start', ['Introduction', 'Getting Started', 'Packages']],
  ['Concepts', ['Snowflake Conversion', 'Core Graph', 'Messages', 'History', 'Soft-delete & Query Filters', 'DbContext Lifetime']],
  ['Guides', ['Migrations', 'Providers', 'Upsert', 'Guild Lifecycle', 'Recipes']],
  ['Add-ons', ['Managed Resources', 'Protection', 'Testing']],
  ['Adapters', ['Choosing an Adapter', 'Discord.Net', 'DSharpPlus', 'NetCord']],
  ['Resources', ['Samples', 'Troubleshooting', 'Upgrading', 'Release notes', 'Contributing']],
]

const ARTICLES = [
  'adapters', 'core-graph', 'dbcontext-lifetime', 'discord-net-adapter', 'dsharpplus-adapter',
  'getting-started', 'guild-lifecycle', 'history', 'introduction', 'managed-resources', 'messages',
  'migrations', 'netcord-adapter', 'packages', 'protection', 'providers', 'recipes', 'samples',
  'snowflake-conversion', 'soft-delete-and-query-filters', 'testing', 'troubleshooting', 'upsert',
]

const PACKAGES = [
  'Persistord', 'Persistord.Core', 'Persistord.Messages', 'Persistord.History',
  'Persistord.Adapters.DiscordNet', 'Persistord.Adapters.DSharpPlus', 'Persistord.Adapters.NetCord',
  'Persistord.Managed', 'Persistord.Protection', 'Persistord.Testing',
]

/* Every page URL that existed before the restructure (spec 4.3); none may move. */
const URLS = [
  'index.html',
  'license.html',
  'api/index.html',
  'development/index.html',
  'development/components.html',
  'samples/README.html',
  ...ARTICLES.map(slug => `articles/${slug}.html`),
  ...PACKAGES.map(name => `src/${name}/README.html`),
]

/* Pages outside articles/ that must borrow the Docs sidebar. */
const BORROWERS = ['development/index.html', 'development/components.html', 'samples/README.html']

/* Reads the sidebar's top level as [group label, [entry names]]. */
const sidebar = page =>
  page.$$eval('#toc .flex-fill > ul > li', items => {
    const groups = []
    for (const item of items) {
      const label = item.querySelector(':scope > span.name-only')
      if (label) {
        groups.push([label.textContent.trim(), []])
      } else {
        groups.at(-1)?.[1].push(item.querySelector(':scope > a').textContent.trim())
      }
    }
    return groups
  })

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

test('the navbar has three tabs: Docs, Packages, API', async () => {
  const { page, close } = await site.open('articles/upsert.html')
  try {
    await page.waitForSelector('#navbar .nav-link')
    const tabs = await page.$$eval('#navbar .navbar-nav .nav-link', links => links.map(link => link.textContent.trim()))
    assert.deepEqual(tabs, ['Docs', 'Packages', 'API'])
  } finally {
    await close()
  }
})

test('the Docs sidebar groups pages by what the reader is doing', async () => {
  const { page, close } = await site.open('articles/upsert.html')
  try {
    await page.waitForSelector('#toc li a')
    assert.deepEqual(await sidebar(page), SIDEBAR)
    assert.ok(await page.$('#toc a[href="https://github.com/HandyS11/Persistord/releases"]'), 'Release notes does not link to GitHub releases')
  } finally {
    await close()
  }
})

for (const path of BORROWERS) {
  test(`${path} shows the Docs sidebar under the Docs tab`, async () => {
    const { page, close } = await site.open(path)
    try {
      await page.waitForSelector('#toc li a')
      assert.deepEqual(await sidebar(page), SIDEBAR)
      /* DocFX's navbar highlight (docfx.min.js's Ns()/ks()) picks the tab whose
         resolved homepage shares the longest URL path prefix with the current
         page. A page directly under development/ or samples/ ties at zero
         extra segments with all three tabs (Docs -> articles/…, Packages ->
         src/…, API -> api/…), so the client script lights none of them up —
         confirmed by probing the live DOM, not a sign of a stray folder TOC.
         `docfx:tocrel` is the real signal for which section's TOC (and so
         which tab) a page borrows; assert that instead of the unreachable
         `.nav-link.active`. */
      const tocrel = await page.$eval('head meta[name="docfx:tocrel"]', meta => meta.content)
      const resolved = new URL(tocrel, site.server.url(path)).pathname
      assert.equal(resolved, new URL(site.server.url('articles/toc.html')).pathname, `${path} is not governed by the Docs TOC`)
      const own = path.split('/').at(-1)
      assert.ok(await page.$(`#toc a[href$="${path}"]`), `${own} is not listed in its own sidebar`)
    } finally {
      await close()
    }
  })
}

test('every page that existed before the restructure still resolves', async () => {
  for (const path of [...URLS, 'articles/upgrading.html']) {
    const response = await fetch(site.server.url(path))
    assert.equal(response.status, 200, `${path} answered ${response.status}`)
  }
})
