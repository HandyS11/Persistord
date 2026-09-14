import { chromium } from 'playwright'
import { startServer } from './server.mjs'

export const THEMES = ['light', 'dark']

/**
 * One server and one browser per test file. Every `open()` gets a fresh
 * context, so a theme, viewport, or motion preference never leaks into the
 * next assertion.
 */
export async function launchSite() {
  const server = await startServer()
  const browser = await chromium.launch()

  async function open(path, options = {}) {
    const {
      theme = 'light',
      colorScheme = 'light',
      width = 1440,
      height = 900,
      reducedMotion = 'no-preference',
    } = options

    const context = await browser.newContext({
      viewport: { width, height },
      colorScheme,
      reducedMotion,
    })

    /* docfx reads localStorage.theme before first paint; `theme: null` leaves it
       unset so the page falls back to main.js's defaultTheme. */
    if (theme) {
      await context.addInitScript(value => localStorage.setItem('theme', value), theme)
    }

    const page = await context.newPage()
    const response = await page.goto(server.url(path), { waitUntil: 'networkidle' })
    await page.evaluate(() => document.fonts.ready)

    return { page, response, close: () => context.close() }
  }

  return {
    server,
    open,
    close: async () => {
      await browser.close()
      await server.close()
    },
  }
}

/** Reads computed style properties (kebab-case) from the first match. */
export function computed(page, selector, properties, pseudo = null) {
  return page.$eval(
    selector,
    (element, [names, pseudoElement]) => {
      const style = getComputedStyle(element, pseudoElement)
      return Object.fromEntries(names.map(name => [name, style.getPropertyValue(name)]))
    },
    [properties, pseudo]
  )
}

/** Reads design tokens (`--pd-<name>`) from the root element. */
export function tokens(page, names) {
  return page.evaluate(list => {
    const style = getComputedStyle(document.documentElement)
    return Object.fromEntries(list.map(name => [name, style.getPropertyValue(`--pd-${name}`).trim()]))
  }, names)
}
