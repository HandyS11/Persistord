import assert from 'node:assert/strict'
import { readdir, readFile } from 'node:fs/promises'
import { after, before, test } from 'node:test'
import { EDGES, edgeKey, flatten } from '../lib/model-edges.mjs'
import { launchSite } from '../lib/site.mjs'

const ARTICLES = new URL('../../../articles/', import.meta.url)
const REPO = new URL('../../../../', import.meta.url)

/* Articles that follow the page skeleton. Each content task appends its pages;
   Task 8 asserts this covers every article. */
const ENRICHED = [
  'introduction',
  'packages',
  'getting-started',
  'snowflake-conversion',
  'core-graph',
  'messages',
  'history',
  'soft-delete-and-query-filters',
  'dbcontext-lifetime',
  'migrations',
  'providers',
  'upsert',
  'guild-lifecycle',
  'recipes',
]

/* Tab ids per page, one array per group, in page order (spec 4.2). */
const TABS = {
  'getting-started': [['postgresql', 'sqlserver', 'sqlite'], ['discordnet', 'dsharpplus', 'netcord']],
  providers: [['postgresql', 'sqlserver', 'sqlite']],
  recipes: [['discordnet', 'dsharpplus', 'netcord']],
  adapters: [['discordnet', 'dsharpplus', 'netcord']],
}

/* Mermaid diagrams per page (spec 4.2, plus packages.md's existing graph). */
const DIAGRAMS = { packages: 1, 'core-graph': 1, messages: 1, history: 1, 'guild-lifecycle': 1, 'managed-resources': 1, protection: 1 }

const ADAPTERS = {
  discordnet: { namespace: 'Persistord.Adapters.DiscordNet', source: 'src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs' },
  dsharpplus: { namespace: 'Persistord.Adapters.DSharpPlus', source: 'src/Persistord.Adapters.DSharpPlus/DSharpPlusMappingExtensions.cs' },
  netcord: { namespace: 'Persistord.Adapters.NetCord', source: 'src/Persistord.Adapters.NetCord/NetCordMappingExtensions.cs' },
}

const PROVIDERS = {
  postgresql: { package: 'Npgsql.EntityFrameworkCore.PostgreSQL', call: 'UseNpgsql(' },
  sqlserver: { package: 'Microsoft.EntityFrameworkCore.SqlServer', call: 'UseSqlServer(' },
  sqlite: { package: 'Microsoft.EntityFrameworkCore.Sqlite', call: 'UseSqlite(' },
}

/* The entities each ER diagram draws; it must draw every model edge between them, and nothing else. */
const ER_NODES = {
  'core-graph': ['GuildEntity', 'ChannelEntity', 'UserEntity', 'MemberEntity', 'RoleEntity'],
  messages: ['MessageEntity', 'Embed', 'EmbedField', 'AttachmentEntity', 'ReactionEntity'],
  history: ['MessageEntity', 'MessageHistoryEntity'],
}

/* `Referenced ||--o{ Holder : "Column"`; `--` is a foreign key, `..` a plain id column. */
const ER_LINE = /^\s*(\w+)\s+[|}o{]{2}(--|\.\.)[|}o{]{2}\s+(\w+)\s*:\s*"(\w+)"\s*$/gm

const diagramsRendered = page =>
  page.waitForFunction(
    () => {
      const diagrams = document.querySelectorAll('pre.mermaid')
      return diagrams.length > 0 && [...diagrams].every(pre => pre.querySelector(':scope > svg'))
    },
    null,
    { timeout: 15000 }
  )

const CALLOUT = /^> \[!(?:NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]$/gm
const TAB_HEADING = /^# \[[^\]]+\]\(#tab\/([\w-]+)\)$/

/**
 * Splits an article into its description, its body, and its prose: the body
 * with every fenced block blanked line for line, so a heading, callout or tab
 * marker inside code never counts.
 */
async function article(slug) {
  const text = await readFile(new URL(`${slug}.md`, ARTICLES), 'utf8')
  const header = /^---\n([\s\S]*?)\n---\n/.exec(text)
  const body = header ? text.slice(header[0].length) : text
  const description = header ? /^description: (.+)$/m.exec(header[1])?.[1] ?? null : null
  const prose = body.replace(/^(`{3,}|~{3,})[^\n]*\n[\s\S]*?^\1[ \t]*$/gm, block => block.replace(/[^\n]/g, ''))
  return { text, body, prose, description }
}

/** Tab ids grouped as DocFX groups them: consecutive tab headings until a `---` line. */
function tabGroups(prose) {
  const groups = []
  let open = null
  for (const line of prose.split('\n')) {
    const tab = TAB_HEADING.exec(line)
    if (tab) {
      if (!open) {
        open = []
        groups.push(open)
      }
      open.push(tab[1])
    } else if (line === '---') {
      open = null
    }
  }
  return groups
}

/** Each tab's raw Markdown (code included), keyed `id#n` for the n-th group using that id. */
function tabPanels(body) {
  const panels = new Map()
  let current = null
  let fence = null
  for (const line of body.split('\n')) {
    const marker = /^(`{3,}|~{3,})/.exec(line)?.[1]
    if (marker && (!fence || marker === fence)) {
      fence = fence ? null : marker
    }
    const tab = fence ? null : TAB_HEADING.exec(line)
    if (tab) {
      let n = 0
      while (panels.has(`${tab[1]}#${n}`)) {
        n++
      }
      current = `${tab[1]}#${n}`
      panels.set(current, '')
    } else if (!fence && line === '---') {
      current = null
    } else if (current) {
      panels.set(current, `${panels.get(current)}${line}\n`)
    }
  }
  return panels
}

/** The lede: the first paragraph after the `# ` title. */
function lede(prose) {
  const lines = prose.split('\n')
  let index = lines.findIndex(line => line.startsWith('# '))
  index++
  while (index < lines.length && !lines[index].trim()) {
    index++
  }
  const paragraph = []
  while (index < lines.length && lines[index].trim()) {
    paragraph.push(lines[index])
    index++
  }
  return paragraph.join(' ')
}

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

for (const slug of ENRICHED) {
  test(`${slug}: front matter carries a one-line description`, async () => {
    const { description } = await article(slug)
    assert.ok(description, 'no description: in front matter')
    assert.ok(description.length >= 50 && description.length <= 160, `description is ${description.length} characters`)
    assert.ok(!description.includes(': '), 'a ": " breaks the YAML plain scalar')
    assert.ok(!/^['"]/.test(description), 'description is quoted')
  })

  test(`${slug}: the title is followed by a one-sentence lede`, async () => {
    const { prose } = await article(slug)
    const text = lede(prose)
    assert.ok(text, 'no paragraph after the title')
    assert.ok(!/^(?:[>|#-]|\d+\.|!\[)/.test(text), `the first block after the title is not a paragraph: ${text.slice(0, 40)}`)
    const plain = text.replace(/`[^`]*`/g, 'code').replace(/\]\([^)]*\)/g, ']')
    assert.equal(plain.match(/[.!?](?=\s|$)/g)?.length ?? 0, 1, `lede is not one sentence: ${text}`)
    assert.ok(plain.trimEnd().endsWith('.'), 'lede does not end with a full stop')
  })

  test(`${slug}: the last section is a uniform See also list`, async () => {
    const { prose } = await article(slug)
    const sections = prose.split(/^## /m)
    const last = sections.at(-1)
    assert.match(last, /^See also\n/, 'the last ## section is not See also')
    assert.equal(sections.filter(section => /^(?:See also|Next steps)\n/.test(section)).length, 1, 'more than one See also / Next steps section')
    const lines = last.split('\n').slice(1).filter(line => line.trim())
    assert.ok(lines.some(line => line.startsWith('- ')), 'See also is empty')
    for (const line of lines) {
      assert.ok(/^- \[[^\]]+\]\([^)]+\) — \S/.test(line) || /^ {2}\S/.test(line), `not a See also item: ${line}`)
    }
  })

  test(`${slug}: callouts are real callouts, at most two`, async () => {
    const { prose } = await article(slug)
    assert.ok((prose.match(CALLOUT)?.length ?? 0) <= 2, 'more than two callouts')
    assert.doesNotMatch(prose, /^> \*\*/m, 'a bold blockquote should be a callout')
  })

  test(`${slug}: tabs and diagrams appear only where spec 4.2 puts them`, async () => {
    const { prose, body } = await article(slug)
    assert.deepEqual(tabGroups(prose), TABS[slug] ?? [])
    assert.equal(body.match(/^```mermaid$/gm)?.length ?? 0, DIAGRAMS[slug] ?? 0)
  })

  test(`${slug}: the built page carries its description exactly once`, async () => {
    const { description } = await article(slug)
    const { page, close } = await site.open(`articles/${slug}.html`)
    try {
      const contents = await page.$$eval('meta[name="description"]', metas => metas.map(meta => meta.content))
      assert.deepEqual(contents, [description])
    } finally {
      await close()
    }
  })
}

test('a tab choice carries to the next page the reader opens', async () => {
  const { page, close } = await site.open('development/components.html')
  try {
    await page.waitForSelector('.tabGroup a[data-tab="sqlite"]')
    await page.click('.tabGroup a[data-tab="sqlite"]')
    await page.waitForURL(/[?&]tabs=sqlite/)
    await page.waitForSelector('#toc a[href$="articles/getting-started.html"]')
    await page.click('#toc a[href$="articles/getting-started.html"]')
    await page.waitForURL(/articles\/getting-started\.html\?tabs=sqlite$/)
  } finally {
    await close()
  }
})

test('links leave the tabs parameter alone when the reader chose no tab', async () => {
  /* development/components.html would not do: docfx's own tab-sync script
     writes a default `tabs=` query parameter into the URL for any page that
     has a `.tabGroup`, before the reader ever clicks anything (verified by
     inspecting docfx.min.js — `i(document.body)` calls `s(G)` unconditionally
     once `G.groups.length` is nonzero). introduction.html has no tab group,
     so its URL genuinely stays bare — the real "reader chose no tab" case. */
  const { page, close } = await site.open('articles/introduction.html')
  try {
    await page.waitForSelector('#toc a[href$="articles/getting-started.html"]')
    await page.click('#toc a[href$="articles/getting-started.html"]')
    await page.waitForURL(/articles\/getting-started\.html$/)
  } finally {
    await close()
  }
})

for (const slug of ENRICHED.filter(slug => TABS[slug])) {
  test(`${slug}: mapper calls in adapter tabs match the adapter source`, async () => {
    const panels = tabPanels((await article(slug)).body)
    for (const [key, markdown] of panels) {
      const adapter = ADAPTERS[key.split('#')[0]]
      if (!adapter) {
        continue
      }
      const source = flatten(await readFile(new URL(adapter.source, REPO), 'utf8'))
      const calls = [...markdown.matchAll(/\.To(\w+)Entity\(([^)]*)\)/g)]
      assert.ok(calls.length > 0, `${key}: no mapper call`)
      assert.ok(markdown.includes(`using ${adapter.namespace};`), `${key}: does not import ${adapter.namespace}`)
      for (const [call, name, args] of calls) {
        const signature = new RegExp(`public static \\w+ To${name}Entity\\(this [\\w.<>?]+ \\w+((?:, [\\w.<>?]+ \\w+)*)\\)`).exec(source)
        assert.ok(signature, `${key}: ${call} has no To${name}Entity in ${adapter.source}`)
        const declared = signature[1] ? signature[1].split(',').length - 1 : 0
        const passed = args.trim() ? args.split(',').length : 0
        assert.equal(passed, declared, `${key}: ${call} passes ${passed} argument(s), the mapper takes ${declared}`)
      }
    }
  })

  test(`${slug}: provider tabs install and register their provider`, async () => {
    const panels = tabPanels((await article(slug)).body)
    for (const [key, markdown] of panels) {
      const provider = PROVIDERS[key.split('#')[0]]
      if (!provider) {
        continue
      }
      assert.ok(markdown.includes(`dotnet add package ${provider.package}`), `${key}: does not install ${provider.package}`)
      assert.ok(markdown.includes(provider.call), `${key}: does not call ${provider.call}`)
    }
  })

  test(`${slug}: the built page renders the tab groups`, async () => {
    const { page, close } = await site.open(`articles/${slug}.html`)
    try {
      await page.waitForSelector('.tabGroup a[data-tab]')
      const groups = await page.$$eval('.tabGroup', elements =>
        elements.map(group => [...group.querySelectorAll(':scope > ul a[data-tab]')].map(link => link.dataset.tab))
      )
      assert.deepEqual(groups, TABS[slug])
    } finally {
      await close()
    }
  })
}

test('getting-started: reads as a tutorial', async () => {
  const { prose } = await article('getting-started')
  const headings = [...prose.matchAll(/^## (.+)$/gm)].map(match => match[1])
  assert.deepEqual(headings, [
    "What you'll build",
    'Prerequisites',
    '1. Install the packages',
    '2. Derive a context',
    '3. Register a provider',
    '4. Write your first records',
    '5. Map from your Discord library',
    '6. Run it',
    'See also',
  ])
})

test('a carried tab choice opens the matching tab on the next page', async () => {
  const { page, close } = await site.open('articles/providers.html?tabs=sqlite')
  try {
    await page.waitForSelector('#toc a[href$="articles/getting-started.html"]')
    await page.click('#toc a[href$="articles/getting-started.html"]')
    await page.waitForURL(/getting-started\.html\?tabs=sqlite$/)
    await page.waitForSelector('.tabGroup a[data-tab="sqlite"][aria-selected="true"]')
  } finally {
    await close()
  }
})

for (const slug of ENRICHED.filter(slug => ER_NODES[slug])) {
  test(`${slug}: ER relationships match the model`, async () => {
    const { body } = await article(slug)
    const source = /^```mermaid\n([\s\S]*?)^```$/m.exec(body)?.[1] ?? ''
    assert.match(source, /^erDiagram$/m)
    const drawn = [...source.matchAll(ER_LINE)].map(([, to, line, from, column]) =>
      edgeKey({ kind: line === '--' ? 'fk' : 'id', from, to, column })
    )
    const nodes = ER_NODES[slug]
    const expected = EDGES.filter(edge => nodes.includes(edge.from) && nodes.includes(edge.to)).map(edgeKey)
    assert.deepEqual(drawn.toSorted(), expected.toSorted())
  })
}

for (const slug of ENRICHED.filter(slug => DIAGRAMS[slug])) {
  test(`${slug}: diagrams render without a syntax error`, async () => {
    const { page, close } = await site.open(`articles/${slug}.html`)
    try {
      await diagramsRendered(page)
      const diagrams = await page.$$eval('pre.mermaid', frames =>
        frames.map(frame => ({
          error: frame.querySelector('svg[aria-roledescription="error"]') !== null || /Syntax error/i.test(frame.textContent),
          overflow: frame.scrollWidth - frame.clientWidth,
        }))
      )
      assert.equal(diagrams.length, DIAGRAMS[slug])
      for (const [index, diagram] of diagrams.entries()) {
        assert.equal(diagram.error, false, `diagram ${index} failed to parse`)
        assert.ok(diagram.overflow <= 0, `diagram ${index} overflows the reading column by ${diagram.overflow}px at 1440px`)
      }
    } finally {
      await close()
    }
  })
}
