import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { after, before, test } from 'node:test'
import { launchSite } from '../lib/site.mjs'

const LANDING = 'index.html'
const REPO = new URL('../../../../', import.meta.url)
const TAB = '[data-pd-tabs] [role="tab"]'

const ADAPTERS = [
  {
    label: 'Discord.Net',
    panel: 'adapter-discordnet',
    package: 'Persistord.Adapters.DiscordNet',
    mappers: 'src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs',
    guide: 'articles/discord-net-adapter.html',
  },
  {
    label: 'DSharpPlus',
    panel: 'adapter-dsharpplus',
    package: 'Persistord.Adapters.DSharpPlus',
    mappers: 'src/Persistord.Adapters.DSharpPlus/DSharpPlusMappingExtensions.cs',
    guide: 'articles/dsharpplus-adapter.html',
  },
  {
    label: 'NetCord',
    panel: 'adapter-netcord',
    package: 'Persistord.Adapters.NetCord',
    mappers: 'src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs',
    guide: 'articles/netcord-adapter.html',
  },
]

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

test('the adapter picker is an ARIA tablist with the first adapter selected', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    await page.waitForSelector(TAB)
    const state = await page.$eval('[data-pd-tabs]', container => {
      const tablist = container.querySelector(':scope > [role="tablist"]')
      return {
        label: tablist?.getAttribute('aria-label'),
        tabs: [...tablist.querySelectorAll('[role="tab"]')].map(tab => {
          const panel = document.getElementById(tab.getAttribute('aria-controls'))
          return {
            name: tab.textContent.trim(),
            tag: tab.tagName.toLowerCase(),
            selected: tab.getAttribute('aria-selected'),
            tabIndex: tab.tabIndex,
            panel: panel?.id,
            panelRole: panel?.getAttribute('role'),
            labelledBy: panel?.getAttribute('aria-labelledby') === tab.id,
            hidden: panel?.hidden,
            headingShown: getComputedStyle(panel.querySelector(':scope > h4')).display !== 'none',
          }
        }),
      }
    })

    assert.equal(state.label, 'Discord library')
    assert.deepEqual(
      state.tabs,
      ADAPTERS.map((adapter, i) => ({
        name: adapter.label,
        tag: 'button',
        selected: String(i === 0),
        tabIndex: i === 0 ? 0 : -1,
        panel: adapter.panel,
        panelRole: 'tabpanel',
        labelledBy: true,
        hidden: i !== 0,
        headingShown: false,
      }))
    )
  } finally {
    await close()
  }
})

test('arrow keys wrap, Home and End jump, a click selects, and Tab leaves the tablist', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    await page.waitForSelector(TAB)
    const selection = () =>
      page.$eval('[data-pd-tabs]', container => {
        const tab = container.querySelector('[role="tab"][aria-selected="true"]')
        return {
          tab: tab.textContent.trim(),
          focused: document.activeElement === tab,
          visible: [...container.querySelectorAll('[role="tabpanel"]')].filter(panel => !panel.hidden).map(panel => panel.id),
        }
      })

    await page.focus(`${TAB}[aria-selected="true"]`)
    const steps = [
      ['ArrowRight', 'DSharpPlus', 'adapter-dsharpplus'],
      ['ArrowRight', 'NetCord', 'adapter-netcord'],
      ['ArrowRight', 'Discord.Net', 'adapter-discordnet'],
      ['ArrowLeft', 'NetCord', 'adapter-netcord'],
      ['Home', 'Discord.Net', 'adapter-discordnet'],
      ['End', 'NetCord', 'adapter-netcord'],
    ]
    for (const [key, tab, panel] of steps) {
      await page.keyboard.press(key)
      assert.deepEqual(await selection(), { tab, focused: true, visible: [panel] }, `after ${key}`)
    }

    await page.getByRole('tab', { name: 'DSharpPlus' }).click()
    assert.deepEqual((await selection()).visible, ['adapter-dsharpplus'], 'after a click')

    await page.keyboard.press('Tab')
    assert.notEqual(await page.evaluate(() => document.activeElement.getAttribute('role')), 'tab', 'Tab moved to another tab instead of leaving the tablist')
  } finally {
    await close()
  }
})

test('without JavaScript the three adapters render stacked under their own headings', async () => {
  const { page, close } = await site.open(LANDING, { javaScriptEnabled: false })
  try {
    assert.equal(await page.locator('[role="tablist"]').count(), 0)
    const panels = await page.$$eval('[data-pd-tabs] > [data-pd-tab]', sections =>
      sections.map(section => {
        const rect = section.getBoundingClientRect()
        return { heading: section.querySelector(':scope > h4')?.textContent.trim(), top: rect.top, bottom: rect.bottom, height: rect.height }
      })
    )
    assert.deepEqual(panels.map(({ heading }) => heading), ADAPTERS.map(({ label }) => label))
    panels.forEach((panel, i) => {
      assert.ok(panel.height > 0, `${panel.heading} is not rendered`)
      if (i > 0) {
        assert.ok(panel.top >= panels[i - 1].bottom, `${panel.heading} is not below ${panels[i - 1].heading}`)
      }
    })
  } finally {
    await close()
  }
})

test('each panel installs its adapter and calls only mappers it declares, with matching arguments', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    for (const adapter of ADAPTERS) {
      const panel = await page.$eval(`#${adapter.panel}`, section => ({
        install: section.querySelector('.pd-install > code')?.textContent.trim(),
        snippet: section.querySelector('pre > code.lang-csharp')?.textContent.trim() ?? '',
        links: [...section.querySelectorAll('a')].map(link => link.getAttribute('href')),
      }))
      const source = (await readFile(new URL(adapter.mappers, REPO), 'utf8')).replace(/\s+/g, ' ')

      assert.equal(panel.install, `dotnet add package ${adapter.package}`)
      const lines = panel.snippet.split('\n').length
      assert.ok(panel.snippet && lines <= 3, `${adapter.label} snippet has ${panel.snippet ? lines : 0} lines`)

      const calls = [...panel.snippet.matchAll(/\.To(\w+)Entity\(([^)]*)\)/g)]
      assert.ok(calls.length > 0, `${adapter.label} snippet calls no mapper`)
      for (const [call, name, args] of calls) {
        const signature = new RegExp(`public static \\w+ To${name}Entity\\(this [\\w.<>?]+ \\w+((?:, [\\w.<>?]+ \\w+)*)\\)`).exec(source)
        assert.ok(signature, `${adapter.label}: ${call} is not declared in ${adapter.mappers}`)
        const declared = signature[1] ? signature[1].split(',').length - 1 : 0
        const passed = args.trim() === '' ? 0 : args.split(',').length
        assert.equal(passed, declared, `${adapter.label}: ${call} passes ${passed} argument(s); the mapper takes ${declared}`)
      }

      assert.ok(panel.links.includes(`src/${adapter.package}/README.html`), `${adapter.label} does not link its README`)
      assert.ok(panel.links.includes(adapter.guide), `${adapter.label} does not link its guide`)
    }

    const dsharp = await page.$eval('#adapter-dsharpplus pre > code', code => code.textContent)
    assert.match(dsharp, /\.To(?:Member|Role)Entity\(\s*[\w.]+\s*\)/, 'the DSharpPlus snippet must show the guildId parameter')
  } finally {
    await close()
  }
})
