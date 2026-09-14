/*
 * Screenshot matrix for review: every page type, both themes, desktop and
 * mobile. Output goes to ./out/ (git-ignored). Attach the set to the PR.
 */
import { mkdir } from 'node:fs/promises'
import { launchSite, THEMES } from './lib/site.mjs'

const PAGES = [
  ['landing', 'index.html'],
  ['guide-core-graph', 'articles/core-graph.html'],
  ['guide-getting-started', 'articles/getting-started.html'],
  ['guide-protection', 'articles/protection.html'],
  ['components', 'development/components.html'],
  ['readme-core', 'src/Persistord.Core/README.html'],
  ['api-namespace', 'api/Persistord.Core.html'],
  ['api-class', 'api/Persistord.Core.UpsertExtensions.html'],
  ['not-found', 'articles/missing/deeper.html'],
]

const WIDTHS = [1440, 375]
const OUT = new URL('./out/', import.meta.url)

await mkdir(OUT, { recursive: true })
const site = await launchSite()

try {
  for (const [name, path] of PAGES) {
    for (const theme of THEMES) {
      for (const width of WIDTHS) {
        const { page, close } = await site.open(path, { theme, width, height: 900 })
        try {
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
