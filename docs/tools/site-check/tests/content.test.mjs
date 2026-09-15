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
  'managed-resources',
  'protection',
  'testing',
  'adapters',
  'discord-net-adapter',
  'dsharpplus-adapter',
  'netcord-adapter',
  'samples',
  'troubleshooting',
  'upgrading',
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

/* An attribute inside an entity block: `type Name`, optional keys, optional "comment". */
const ER_ATTRIBUTE = /^\s*\w+\s+\w+(?:\s+(?:PK|FK|UK)(?:\s*,\s*(?:PK|FK|UK))*)?(?:\s+"[^"]*")?\s*$/

const diagramsRendered = page =>
  page.waitForFunction(
    () => {
      const diagrams = document.querySelectorAll('pre.mermaid')
      /* Any svg, not just a direct child: Mermaid nests its error graphic in a
         wrapper, and that must reach the "failed to parse" assertion below. */
      return diagrams.length > 0 && [...diagrams].every(pre => pre.querySelector('svg'))
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

/** Clicks a tab as a reader would and waits for docfx to select it. */
async function chooseTab(page, id) {
  const tab = `.tabGroup > ul > li > a[data-tab="${id}"]`
  await page.waitForFunction(() => [...document.querySelectorAll('.tabGroup')].every(group => group.tabGroup))
  await page.click(tab)
  await page.waitForSelector(`${tab}[aria-selected="true"]`)
}

/** Follows the sidebar link to an article and waits for the page to load. */
async function openFromSidebar(page, slug) {
  const link = `#toc a[href$="articles/${slug}.html"]`
  await page.waitForSelector(link)
  await Promise.all([page.waitForURL(new RegExp(`articles/${slug}\\.html`)), page.click(link)])
  await page.waitForFunction(() => window.docfx?.ready)
}

/** Asserts the tab ids selected on the page, one per group in page order, once they settle. */
async function assertSelected(page, expected) {
  const deadline = Date.now() + 5000
  let actual
  do {
    actual = await page.$$eval('.tabGroup > ul a[aria-selected="true"]', links => links.map(link => link.dataset.tab))
    if (JSON.stringify(actual) === JSON.stringify(expected)) {
      break
    }
    await page.waitForTimeout(100)
  } while (Date.now() < deadline)
  assert.deepEqual(actual, expected)
}

test('a provider choice on getting-started selects the same provider on providers', async () => {
  const { page, close } = await site.open('articles/getting-started.html')
  try {
    await chooseTab(page, 'sqlite')
    await openFromSidebar(page, 'providers')
    await assertSelected(page, ['sqlite'])
  } finally {
    await close()
  }
})

test('an adapter choice on getting-started selects the same adapter on recipes', async () => {
  const { page, close } = await site.open('articles/getting-started.html')
  try {
    await chooseTab(page, 'netcord')
    await openFromSidebar(page, 'recipes')
    await assertSelected(page, ['netcord'])
  } finally {
    await close()
  }
})

test('a page without the provider group does not forget the provider choice', async () => {
  const { page, close } = await site.open('articles/getting-started.html')
  try {
    await chooseTab(page, 'sqlite')
    await openFromSidebar(page, 'adapters')
    await assertSelected(page, ['discordnet'])
    await openFromSidebar(page, 'providers')
    await assertSelected(page, ['sqlite'])
  } finally {
    await close()
  }
})

test('with no choice made, a tab page opens on the docfx default tabs', async () => {
  const { page, close } = await site.open('articles/getting-started.html')
  try {
    await page.waitForFunction(() => window.docfx?.ready)
    await page.waitForTimeout(500)
    await assertSelected(page, ['postgresql', 'discordnet'])
  } finally {
    await close()
  }
})

test('an explicit tabs parameter wins over the remembered choice', async () => {
  const { page, close } = await site.open('articles/getting-started.html')
  try {
    await chooseTab(page, 'sqlite')
    await page.goto(site.server.url('articles/providers.html?tabs=sqlserver'), { waitUntil: 'networkidle' })
    await page.waitForFunction(() => window.docfx?.ready)
    await page.waitForTimeout(500)
    await assertSelected(page, ['sqlserver'])
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

for (const slug of ENRICHED.filter(slug => ER_NODES[slug])) {
  test(`${slug}: ER relationships match the model`, async () => {
    const { body } = await article(slug)
    const source = /^```mermaid\n([\s\S]*?)^```$/m.exec(body)?.[1] ?? ''
    assert.match(source, /^erDiagram$/m)
    /* Every line must be the keyword, a line ER_LINE reads, or an attribute block,
       so a relationship written in another form cannot slip past the comparison. */
    let inBlock = false
    for (const line of source.split('\n').filter(line => line.trim())) {
      if (inBlock) {
        inBlock = line.trim() !== '}'
        assert.ok(!inBlock || ER_ATTRIBUTE.test(line), `not an attribute line: ${line}`)
      } else if (/^\s*\w+\s*\{\s*$/.test(line)) {
        inBlock = true
      } else {
        assert.ok(line.trim() === 'erDiagram' || new RegExp(ER_LINE.source).test(line), `not a relationship line ER_LINE reads: ${line}`)
      }
    }
    assert.equal(inBlock, false, 'an attribute block is not closed')
    const drawn = [...source.matchAll(ER_LINE)].map(([, to, line, from, column]) =>
      edgeKey({ kind: line === '--' ? 'fk' : 'id', from, to, column })
    )
    const nodes = ER_NODES[slug]
    const expected = EDGES.filter(edge => nodes.includes(edge.from) && nodes.includes(edge.to)).map(edgeKey)
    assert.deepEqual(drawn.toSorted(), expected.toSorted())
  })
}

test('troubleshooting: every entry reads Symptom, Cause, Fix', async () => {
  const { prose } = await article('troubleshooting')
  const entries = prose.split(/^## /m).slice(1).filter(section => !section.startsWith('See also\n'))
  assert.ok(entries.length >= 8, `only ${entries.length} entries`)
  for (const entry of entries) {
    const title = entry.split('\n')[0]
    const labels = [...entry.matchAll(/^\*\*(Symptom|Cause|Fix):\*\*/gm)].map(match => match[1])
    assert.deepEqual(labels, ['Symptom', 'Cause', 'Fix'], title)
  }
})

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

test('every article follows the skeleton, and every tab and diagram page is enriched', async () => {
  const slugs = (await readdir(ARTICLES)).filter(name => name.endsWith('.md')).map(name => name.slice(0, -3))
  assert.deepEqual(slugs.filter(slug => !ENRICHED.includes(slug)), [], 'articles missing from ENRICHED')
  assert.deepEqual([...Object.keys(TABS), ...Object.keys(DIAGRAMS)].filter(slug => !ENRICHED.includes(slug)), [])
})
