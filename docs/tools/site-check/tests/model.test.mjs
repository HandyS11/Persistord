import assert from 'node:assert/strict'
import { readdir, readFile } from 'node:fs/promises'
import { after, before, test } from 'node:test'
import { parseColor, sameColor } from '../lib/color.mjs'
import { THEMES, computed, launchSite, tokens } from '../lib/site.mjs'

const LANDING = 'index.html'
const SVG = '.pd-model-svg'
const REPO = new URL('../../../../', import.meta.url)
const PACKAGES = ['Persistord.Core', 'Persistord.Messages', 'Persistord.History']

/* Whitespace-insensitive source, so a fluent chain split over lines matches. */
const flatten = text => text.replace(/\s+/g, ' ')

/*
 * Every edge the landing diagram draws. `fk`: a foreign key the model
 * configures, proven by the `evidence` statements ([repo path, statement]).
 * `id`: a snowflake column that refers to another entity with no foreign key.
 * Arrows run from the entity holding the column to the entity it refers to.
 */
const EDGES = [
  {
    kind: 'fk', from: 'ChannelEntity', to: 'ChannelEntity', column: 'ParentId',
    evidence: [
      ['src/Persistord.Core/Configurations/ChannelEntityConfiguration.cs', 'builder.HasOne<ChannelEntity>() .WithMany() .HasForeignKey(c => c.ParentId)'],
    ],
  },
  {
    kind: 'fk', from: 'Embed', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.Messages/Entities/MessageEntity.cs', 'public List<Embed> Embeds'],
      ['src/Persistord.Messages/Configurations/MessageEntityConfiguration.cs', 'builder.HasMany(m => m.Embeds).WithOne().HasForeignKey(e => e.MessageId);'],
    ],
  },
  {
    kind: 'fk', from: 'AttachmentEntity', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.Messages/Entities/MessageEntity.cs', 'public List<AttachmentEntity> Attachments'],
      ['src/Persistord.Messages/Configurations/MessageEntityConfiguration.cs', 'builder.HasMany(m => m.Attachments).WithOne().HasForeignKey(a => a.MessageId);'],
    ],
  },
  {
    kind: 'fk', from: 'ReactionEntity', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.Messages/Entities/MessageEntity.cs', 'public List<ReactionEntity> Reactions'],
      ['src/Persistord.Messages/Configurations/MessageEntityConfiguration.cs', 'builder.HasMany(m => m.Reactions).WithOne().HasForeignKey(r => r.MessageId);'],
    ],
  },
  {
    kind: 'fk', from: 'EmbedField', to: 'Embed', column: 'EmbedId',
    evidence: [
      ['src/Persistord.Messages/Owned/Embed.cs', 'public List<EmbedField> Fields'],
      ['src/Persistord.Messages/Configurations/EmbedEntityConfiguration.cs', 'builder.HasMany(e => e.Fields).WithOne().HasForeignKey(f => f.EmbedId);'],
    ],
  },
  {
    kind: 'fk', from: 'MessageHistoryEntity', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.History/Configurations/MessageHistoryEntityConfiguration.cs', 'builder.HasOne<MessageEntity>() .WithMany() .HasForeignKey(h => h.MessageId)'],
    ],
  },
  { kind: 'id', from: 'GuildEntity', to: 'UserEntity', column: 'OwnerId' },
  { kind: 'id', from: 'MemberEntity', to: 'UserEntity', column: 'UserId' },
  { kind: 'id', from: 'MemberEntity', to: 'GuildEntity', column: 'GuildId' },
  { kind: 'id', from: 'RoleEntity', to: 'GuildEntity', column: 'GuildId' },
  { kind: 'id', from: 'ChannelEntity', to: 'GuildEntity', column: 'GuildId' },
  { kind: 'id', from: 'MessageEntity', to: 'ChannelEntity', column: 'ChannelId' },
  { kind: 'id', from: 'MessageEntity', to: 'UserEntity', column: 'AuthorId' },
]

const NODE_NAMES = [...new Set(EDGES.flatMap(edge => [edge.from, edge.to]))]
const edgeKey = ({ kind, from, column, to }) => `${kind} ${from}.${column} -> ${to}`

/*
 * Reads every .cs file in the three model packages exactly once, so a repeated class name
 * (every package has its own `ModelBuilderExtensions`) can never shadow another file's source
 * out of the drift guards below. `files` is the source of truth for "does this codebase mention
 * X anywhere"; `classes` is a name-keyed lookup for tests that already know which single class
 * they want (entity and configuration names are unique across the three packages).
 */
async function modelSource() {
  const classes = new Map()
  const files = []

  async function walk(directory, relativePath, packageName) {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      if (entry.name === 'bin' || entry.name === 'obj') {
        continue
      }
      if (entry.isDirectory()) {
        await walk(new URL(`${entry.name}/`, directory), `${relativePath}${entry.name}/`, packageName)
        continue
      }
      if (!entry.name.endsWith('.cs')) {
        continue
      }
      const source = flatten(await readFile(new URL(entry.name, directory), 'utf8'))
      const path = `${relativePath}${entry.name}`
      files.push({ path, source })

      const declarations = source.matchAll(/\bpublic (?:sealed |abstract |static )*class (\w+)(?:\([^)]*\))? ?(:[^{]*)?\{/g)
      for (const [, name, bases] of declarations) {
        classes.set(name, { packageName, source, bases: bases?.trim() ?? '' })
      }
    }
  }

  for (const packageName of PACKAGES) {
    await walk(new URL(`src/${packageName}/`, REPO), `src/${packageName}/`, packageName)
  }
  return { classes, files }
}

/*
 * The only navigations a drawn entity has to another drawn entity today. Each backs an `fk` edge
 * already drawn from the *dependent's* foreign key column (Embed.MessageId, AttachmentEntity.MessageId,
 * ReactionEntity.MessageId, EmbedField.EmbedId) - these four are the matching *principal-side*
 * collection navigations, not a second relationship.
 */
const ALLOWED_NAVIGATIONS = new Set([
  'MessageEntity.Embeds',
  'MessageEntity.Attachments',
  'MessageEntity.Reactions',
  'Embed.Fields',
])

let site
let classes
let files

before(async () => {
  site = await launchSite()
  ;({ classes, files } = await modelSource())
})

after(async () => {
  await site.close()
})

test('the diagram draws exactly the recorded nodes, packages and edges', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const drawn = await page.$eval(SVG, svg => ({
      nodes: [...svg.querySelectorAll('.pd-model-node')].map(node => [
        node.dataset.node,
        node.closest('[data-package]')?.dataset.package,
      ]),
      edges: [...svg.querySelectorAll('.pd-model-edge')].map(({ dataset }) => ({ ...dataset })),
    }))

    assert.deepEqual(drawn.edges.map(edgeKey).toSorted(), EDGES.map(edgeKey).toSorted())
    assert.deepEqual(drawn.nodes.map(([name]) => name).toSorted(), NODE_NAMES.toSorted())
    for (const [name, packageName] of drawn.nodes) {
      assert.ok(classes.has(name), `${name} is not a class in ${PACKAGES.join(', ')}`)
      assert.equal(packageName, classes.get(name).packageName, `${name} is drawn inside the wrong package`)
    }
  } finally {
    await close()
  }
})

test('every solid edge is a foreign key the configurations declare', async () => {
  for (const edge of EDGES.filter(({ kind }) => kind === 'fk')) {
    for (const [path, statement] of edge.evidence) {
      const source = flatten(await readFile(new URL(path, REPO), 'utf8'))
      assert.ok(source.includes(flatten(statement)), `${edge.from}.${edge.column}: "${statement}" is not in ${path}`)
    }
  }
})

test('every dashed edge is a snowflake column with no foreign key behind it', () => {
  /*
   * GuildRootExtensions.cs calls HasForeignKey(nameof(IGuildScoped.GuildId)), but generically -
   * it never names a specific entity, and it only reaches types that implement IGuildScoped. The
   * "no bases" assertion below already proves no drawn entity does, so this file can never be the
   * source of a foreign key on a drawn dashed-edge column and is exempt from the statement scan.
   */
  const EXEMPT = 'src/Persistord.Core/GuildRootExtensions.cs'
  const foreignKeyStatements = files
    .filter(({ path }) => path !== EXEMPT)
    .flatMap(({ source }) => source.split(';'))
    .filter(statement => statement.includes('HasForeignKey'))

  for (const edge of EDGES.filter(({ kind }) => kind === 'id')) {
    const label = `${edge.from}.${edge.column}`
    const entity = classes.get(edge.from)
    assert.match(entity.source, new RegExp(`public ulong\\?? ${edge.column} \\{`), `${label} is not a ulong column`)
    /* Matches any form - lambda, nameof, string, generic type argument, composite key - because a
       HasForeignKey statement anywhere that mentions this column name means it may no longer be
       a bare, unconfigured snowflake reference. */
    const column = new RegExp(`\\b${edge.column}\\b`)
    assert.ok(
      !foreignKeyStatements.some(statement => column.test(statement)),
      `${label} now has a configured foreign key - redraw it solid`
    )
    /* ApplyGuildRoot keys only IGuildScoped types; a base list could add one. */
    assert.equal(entity.bases, '', `${edge.from} now derives from "${entity.bases}" - re-check ApplyGuildRoot`)
  }
})

test('every reference column on a drawn entity is drawn', () => {
  const drawn = new Set(EDGES.map(({ from, column }) => `${from}.${column}`))
  for (const name of NODE_NAMES) {
    for (const [, column] of classes.get(name).source.matchAll(/public u?long\?? (\w+Id) \{/g)) {
      assert.ok(drawn.has(`${name}.${column}`), `${name}.${column} refers to another entity but is not drawn`)
    }
  }
})

test('every navigation from a drawn entity to another drawn entity is a recorded edge', () => {
  const NAVIGATION = /public (?:(?:List|ICollection|IEnumerable)<(\w+)>|(\w+)\??) (\w+) \{/g
  for (const name of NODE_NAMES) {
    for (const [, collectionType, scalarType, propertyName] of classes.get(name).source.matchAll(NAVIGATION)) {
      const referenced = collectionType ?? scalarType
      if (!NODE_NAMES.includes(referenced)) {
        continue
      }
      const key = `${name}.${propertyName}`
      assert.ok(ALLOWED_NAVIGATIONS.has(key), `${key} navigates to ${referenced}, which is not drawn as an edge`)
    }
  }
})

test('every IEntityTypeConfiguration in the three packages configures a drawn entity', () => {
  for (const [name, { bases }] of classes) {
    const configured = /IEntityTypeConfiguration<(\w+)>/.exec(bases)
    if (!configured) {
      continue
    }
    assert.ok(NODE_NAMES.includes(configured[1]), `${name} configures ${configured[1]}, which is not drawn`)
  }
})

test('every HasMany element type on a configured entity is drawn', () => {
  for (const [name, { bases, source }] of classes) {
    const configured = /IEntityTypeConfiguration<(\w+)>/.exec(bases)
    if (!configured) {
      continue
    }
    const entityName = configured[1]
    const entity = classes.get(entityName)
    assert.ok(entity, `${name} configures ${entityName}, which is not a class in ${PACKAGES.join(', ')}`)
    for (const [, nav] of source.matchAll(/HasMany\(\w+ => \w+\.(\w+)\)/g)) {
      const declaration = new RegExp(`public (?:List|ICollection|IEnumerable)<(\\w+)> ${nav} \\{`).exec(entity.source)
      assert.ok(declaration, `${name}: HasMany(...${nav}) but ${entityName} declares no List<T> ${nav}`)
      assert.ok(NODE_NAMES.includes(declaration[1]), `${entityName}.${nav} is a HasMany to ${declaration[1]}, which is not drawn`)
    }
  }
})

test('the configured foreign keys in the three packages equal the drawn fk edges', () => {
  const count = files
    .filter(({ path }) => path.includes('/Configurations/'))
    .reduce((total, { source }) => total + (source.match(/\bHasForeignKey\b/g) ?? []).length, 0)
  const fkEdges = EDGES.filter(({ kind }) => kind === 'fk').length
  assert.equal(count, fkEdges, `Configurations/ folders declare ${count} HasForeignKey call(s), but EDGES records ${fkEdges} fk edge(s)`)
})

test('Embed shows the two types it owns', async () => {
  const configuration = classes.get('EmbedEntityConfiguration').source
  assert.ok(configuration.includes('builder.OwnsOne(e => e.Footer);'), 'Embed no longer owns its footer')
  assert.ok(configuration.includes('builder.OwnsOne(e => e.Author);'), 'Embed no longer owns its author')

  const { page, close } = await site.open(LANDING)
  try {
    const text = await page.$eval(`${SVG} [data-node="Embed"]`, node => node.textContent)
    assert.match(text, /Footer/)
    assert.match(text, /Author/)
  } finally {
    await close()
  }
})

test('nodes do not overlap, labels fit their boxes, and edges join only their own nodes', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const problems = await page.$eval(SVG, svg => {
      const box = element => {
        const b = element.getBBox()
        return { left: b.x, top: b.y, right: b.x + b.width, bottom: b.y + b.height }
      }
      const inside = (point, rect, inset) =>
        point.x > rect.left + inset && point.x < rect.right - inset && point.y > rect.top + inset && point.y < rect.bottom - inset
      const near = (point, rect, slack) =>
        point.x >= rect.left - slack && point.x <= rect.right + slack && point.y >= rect.top - slack && point.y <= rect.bottom + slack

      const found = []
      const nodes = [...svg.querySelectorAll('.pd-model-node')].map(node => ({
        name: node.dataset.node,
        rect: box(node.querySelector('rect')),
        texts: [...node.querySelectorAll('text')].map(box),
      }))
      const labels = [...svg.querySelectorAll('.pd-model-package')].map(box)

      for (const node of nodes) {
        for (const text of node.texts) {
          if (text.left < node.rect.left + 4 || text.right > node.rect.right - 4 || text.top < node.rect.top || text.bottom > node.rect.bottom) {
            found.push(`${node.name}: a label overflows its box`)
          }
        }
      }

      nodes.forEach((a, i) => {
        for (const b of nodes.slice(i + 1)) {
          if (a.rect.left < b.rect.right && b.rect.left < a.rect.right && a.rect.top < b.rect.bottom && b.rect.top < a.rect.bottom) {
            found.push(`${a.name} overlaps ${b.name}`)
          }
        }
      })

      const byName = name => nodes.find(node => node.name === name)
      for (const path of svg.querySelectorAll('.pd-model-edge')) {
        const { from, to, column } = path.dataset
        const label = `${from}.${column} -> ${to}`
        const length = path.getTotalLength()
        if (!near(path.getPointAtLength(0), byName(from).rect, 1.5)) {
          found.push(`${label}: does not start on ${from}`)
        }
        if (!near(path.getPointAtLength(length), byName(to).rect, 1.5)) {
          found.push(`${label}: does not end on ${to}`)
        }
        for (let distance = 0; distance <= length; distance += 4) {
          const point = path.getPointAtLength(distance)
          for (const node of nodes) {
            if (node.name !== from && node.name !== to && inside(point, node.rect, 1)) {
              found.push(`${label}: crosses ${node.name}`)
            }
          }
          if (labels.some(rect => inside(point, rect, 0))) {
            found.push(`${label}: crosses a package label`)
          }
        }
      }
      return [...new Set(found)]
    })
    assert.deepEqual(problems, [])
  } finally {
    await close()
  }
})

test('the diagram has an accessible name and a description naming every entity', async () => {
  const { page, close } = await site.open(LANDING)
  try {
    const a11y = await page.$eval(SVG, svg => {
      const [title, desc] = (svg.getAttribute('aria-labelledby') ?? '').split(/\s+/).map(id => document.getElementById(id))
      return {
        role: svg.getAttribute('role'),
        title: title?.tagName.toLowerCase() === 'title' ? title.textContent.trim() : null,
        desc: desc?.tagName.toLowerCase() === 'desc' ? desc.textContent.replace(/\s+/g, ' ') : '',
        viewBoxWidth: svg.viewBox.baseVal.width,
      }
    })
    assert.equal(a11y.role, 'img')
    assert.ok(a11y.title, 'aria-labelledby does not start with the <title>')
    for (const name of NODE_NAMES) {
      assert.match(a11y.desc, new RegExp(`\\b${name}\\b`), `the <desc> never mentions ${name}`)
    }
    assert.equal(a11y.viewBoxWidth, 1120, 'viewBox did not survive the build (check its casing in the DOM)')
  } finally {
    await close()
  }
})

for (const theme of THEMES) {
  test(`${theme}: the diagram takes every colour from the tokens`, async () => {
    const { page, close } = await site.open(LANDING, { theme })
    try {
      const values = await tokens(page, ['surface', 'hairline', 'raised', 'control-border', 'text', 'muted', 'accent'])
      const expectations = [
        ['.pd-model-frame', 'fill', 'surface'],
        ['.pd-model-frame', 'stroke', 'hairline'],
        ['.pd-model-package', 'fill', 'accent'],
        ['.pd-model-node > rect', 'fill', 'raised'],
        ['.pd-model-node > rect', 'stroke', 'control-border'],
        ['.pd-model-name', 'fill', 'text'],
        ['.pd-model-cols', 'fill', 'muted'],
        ['.pd-model-edge', 'stroke', 'muted'],
        ['.pd-model-head', 'fill', 'muted'],
      ]
      for (const [selector, property, token] of expectations) {
        const value = (await computed(page, `${SVG} ${selector}`, [property]))[property]
        assert.ok(sameColor(parseColor(value), parseColor(values[token])), `${theme}: ${selector} ${property} is ${value}, not --pd-${token}`)
      }

      const dash = async kind => (await computed(page, `${SVG} .pd-model-edge[data-kind="${kind}"]`, ['stroke-dasharray']))['stroke-dasharray']
      assert.equal(await dash('fk'), 'none')
      assert.notEqual(await dash('id'), 'none')

      const painted = await page.locator(`${SVG} [fill], ${SVG} [stroke], ${SVG} [style], ${SVG} [color]`).count()
      assert.equal(painted, 0, 'the SVG hard-codes a colour attribute')
    } finally {
      await close()
    }
  })
}

test('the diagram fills the column on desktop and scrolls inside its figure on a phone', async () => {
  const desktop = await site.open(LANDING, { width: 1440 })
  try {
    const box = await desktop.page.$eval('.pd-model-scroll', scroll => ({ scroll: scroll.scrollWidth, client: scroll.clientWidth }))
    assert.ok(box.scroll <= box.client, `the desktop diagram scrolls: ${box.scroll} > ${box.client}`)
  } finally {
    await desktop.close()
  }

  const phone = await site.open(LANDING, { width: 375 })
  try {
    const box = await phone.page.evaluate(() => {
      const scroll = document.querySelector('.pd-model-scroll')
      return {
        page: document.documentElement.scrollWidth - document.documentElement.clientWidth,
        scrolls: scroll.scrollWidth > scroll.clientWidth,
        svg: document.querySelector('.pd-model-svg').getBoundingClientRect().width,
      }
    })
    assert.equal(box.page, 0, 'the diagram pushes the page wider than the viewport')
    assert.ok(box.scrolls, 'the diagram does not scroll inside its figure')
    assert.ok(box.svg >= 880, `the diagram shrank to ${box.svg}px`)
  } finally {
    await phone.close()
  }
})
