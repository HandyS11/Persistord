/*
 * Screenshot matrix for review: every page type, both themes, desktop and
 * mobile. Output goes to ./out/ (git-ignored). Attach the set to the PR.
 */
import { mkdir } from 'node:fs/promises'
import { launchSite, THEMES } from './lib/site.mjs'

/* Runs a query through docfx's own input listener. The field is collapsed
   into the navbar menu on a phone, so the value is set directly rather than
   typed. */
const searchFor = query => async page => {
  await page.waitForSelector('#search-query:not([disabled])', { state: 'attached', timeout: 15000 })
  await page.$eval('#search-query', (input, value) => {
    input.value = value
    input.dispatchEvent(new Event('input'))
  }, query)
  await page.waitForSelector('#search-results .sr-item')
}

/* [name, path, prepare?] - prepare runs before the screenshot. */
const PAGES = [
  ['landing', 'index.html'],
  ['guide-core-graph', 'articles/core-graph.html'],
  ['guide-getting-started', 'articles/getting-started.html'],
  ['guide-protection', 'articles/protection.html'],
  ['components', 'development/components.html'],
  ['readme-core', 'src/Persistord.Core/README.html'],
  ['api-namespace', 'api/Persistord.Core.html'],
  ['api-class', 'api/Persistord.Core.UpsertExtensions.html'],
  ['search-results', 'articles/upsert.html', searchFor('message')],
  ['not-found', 'articles/missing/deeper.html'],
]

const WIDTHS = [1440, 375]
const OUT = new URL('./out/', import.meta.url)

await mkdir(OUT, { recursive: true })
const site = await launchSite()

try {
  for (const [name, path, prepare] of PAGES) {
    for (const theme of THEMES) {
      for (const width of WIDTHS) {
        const { page, close } = await site.open(path, { theme, width, height: 900 })
        try {
          await prepare?.(page)
          /* Mermaid renders after load. */
          await page.waitForTimeout(600)
          const file = new URL(`${name}-${theme}-${width}.png`, OUT)
          await page.screenshot({ path: file.pathname, fullPage: true })
          console.log(`shot ${name}-${theme}-${width}.png`)
        } finally {
          await close()
        }
      }
    }
  }
} finally {
  await site.close()
}
