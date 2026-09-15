import assert from 'node:assert/strict'
import { after, before, test } from 'node:test'
import { contrast, parseColor, sameColor } from '../lib/color.mjs'
import { THEMES, computed, launchSite, tokens } from '../lib/site.mjs'

const PAGE = 'development/components.html'
const CSHARP = 'pre:has(> code.lang-csharp)'

const CALLOUTS = [
  ['NOTE', 'note'],
  ['TIP', 'tip'],
  ['IMPORTANT', 'note'],
  ['WARNING', 'warning'],
  ['CAUTION', 'caution'],
]

/* Classes the component page must keep producing, so the syntax check below
   cannot pass on a page that lost its samples. The check itself covers every
   .hljs-* span that renders, listed here or not. */
const SYNTAX = [
  'hljs-keyword',
  'hljs-string',
  'hljs-number',
  'hljs-comment',
  'hljs-doctag',
  'hljs-meta',
  'hljs-title',
  'hljs-function',
  'hljs-params',
  'hljs-attr',
  'hljs-link',
  'hljs-bullet',
]

/* Every colour a highlighted span may take: plain code text or a code token. */
const SYNTAX_PALETTE = ['text', 'code-keyword', 'code-string', 'code-number', 'code-comment', 'code-title', 'code-attr']

/* Resolves once every Mermaid fence on the page has been replaced by its SVG. */
const diagramsRendered = page =>
  page.waitForFunction(
    () => {
      const diagrams = document.querySelectorAll('pre.mermaid')
      return diagrams.length > 0 && [...diagrams].every(pre => pre.querySelector(':scope > svg'))
    },
    null,
    { timeout: 15000 }
  )

let site

before(async () => {
  site = await launchSite()
})

after(async () => {
  await site.close()
})

for (const theme of THEMES) {
  test(`${theme}: code blocks have exactly one frame`, async () => {
    const { page, close } = await site.open(PAGE, { theme })
    try {
      const pre = await computed(page, CSHARP, ['border-top-width', 'border-top-left-radius', 'padding-top'])
      assert.equal(pre['border-top-width'], '1px')
      assert.equal(pre['border-top-left-radius'], '12px')
      assert.equal(pre['padding-top'], '0px')

      const code = await computed(page, `${CSHARP} > code`, ['background-color'])
      assert.equal(parseColor(code['background-color'])[3], 0, `inner code panel is ${code['background-color']}`)
    } finally {
      await close()
    }
  })

  test(`${theme}: callouts carry their status colour and readable text`, async () => {
    const { page, close } = await site.open(PAGE, { theme })
    try {
      const values = await tokens(page, ['note', 'tip', 'warning', 'caution'])
      for (const [kind, token] of CALLOUTS) {
        const box = await computed(page, `.alert.${kind}`, ['border-left-width', 'border-left-color', 'background-color', 'color'])
        const label = await computed(page, `.alert.${kind} > h5`, ['color'])
        const background = parseColor(box['background-color'])

        assert.equal(box['border-left-width'], '3px', `${kind} rule width`)
        assert.ok(sameColor(parseColor(box['border-left-color']), parseColor(values[token])), `${kind} rule is ${box['border-left-color']}`)

        const labelRatio = contrast(parseColor(label.color), background)
        assert.ok(labelRatio >= 4.5, `${theme} ${kind} label is ${labelRatio.toFixed(2)}:1`)
        const textRatio = contrast(parseColor(box.color), background)
        assert.ok(textRatio >= 4.5, `${theme} ${kind} text is ${textRatio.toFixed(2)}:1`)
      }
    } finally {
      await close()
    }
  })

  test(`${theme}: the active tab is underlined in the accent colour`, async () => {
    const { page, close } = await site.open(PAGE, { theme })
    try {
      const { accent } = await tokens(page, ['accent'])
      const tab = await computed(page, '.tabGroup .nav-link.active', ['border-bottom-color', 'color'])
      assert.ok(sameColor(parseColor(tab['border-bottom-color']), parseColor(accent)))
      assert.ok(sameColor(parseColor(tab.color), parseColor(accent)))

      const panel = await computed(page, '.tabGroup > section', ['border-top-width', 'border-left-width'])
      assert.equal(panel['border-top-width'], '0px')
      assert.equal(panel['border-left-width'], '0px')
    } finally {
      await close()
    }
  })

  test(`${theme}: tables are framed with a raised header row`, async () => {
    const { page, close } = await site.open(PAGE, { theme })
    try {
      const { raised } = await tokens(page, ['raised'])
      const frame = await computed(page, '.table-responsive', ['border-top-width', 'border-top-left-radius'])
      assert.equal(frame['border-top-width'], '1px')
      assert.equal(frame['border-top-left-radius'], '12px')
      const th = await computed(page, '.table-responsive thead th', ['background-color'])
      assert.ok(sameColor(parseColor(th['background-color']), parseColor(raised)), `th is ${th['background-color']}`)
    } finally {
      await close()
    }
  })

  test(`${theme}: every highlighted span takes a palette colour that meets AA on the code surface`, async () => {
    const { page, close } = await site.open(PAGE, { theme })
    try {
      const palette = Object.values(await tokens(page, SYNTAX_PALETTE)).map(parseColor)
      const spans = await page.$$eval('.content article pre > code [class*="hljs-"]', elements =>
        elements.map(element => ({
          name: [...element.classList].join('.'),
          within: element.parentElement.className.split(' ').find(name => name.startsWith('hljs-')) ?? 'code',
          color: getComputedStyle(element).color,
          background: getComputedStyle(element.closest('pre')).backgroundColor,
        }))
      )

      const rendered = new Set(spans.flatMap(span => span.name.split('.')))
      assert.deepEqual(SYNTAX.filter(name => !rendered.has(name)), [], 'classes missing from the component page')

      const failures = new Set()
      for (const span of spans) {
        const color = parseColor(span.color)
        const ratio = contrast(color, parseColor(span.background))
        if (ratio < 4.5) {
          failures.add(`.${span.name} in .${span.within} is ${ratio.toFixed(2)}:1`)
        }
        if (!palette.some(token => sameColor(color, token))) {
          failures.add(`.${span.name} in .${span.within} is off-palette ${span.color}`)
        }
      }
      assert.deepEqual([...failures], [], `${theme} syntax colours`)
    } finally {
      await close()
    }
  })

  test(`${theme}: a highlighted line is tinted with the accent wash`, async () => {
    const { page, close } = await site.open(PAGE, { theme })
    try {
      /* docfx wraps the lines named by a snippet's highlight= query in this
         span; the component page has no snippet include, so one is made. */
      await page.$eval(`${CSHARP} > code`, code => {
        const span = document.createElement('span')
        span.className = 'line-highlight'
        span.textContent = 'highlighted'
        code.prepend(span)
      })
      const { 'accent-wash': wash } = await tokens(page, ['accent-wash'])
      const line = await computed(page, `${CSHARP} > code > .line-highlight`, ['background-color'])
      assert.ok(sameColor(parseColor(line['background-color']), parseColor(wash), 0.01), `highlighted line is ${line['background-color']}`)
    } finally {
      await close()
    }
  })

  test(`${theme}: Mermaid diagrams are recoloured from the tokens`, async () => {
    const { page, close } = await site.open(PAGE, { theme })
    try {
      await page.waitForSelector('pre.mermaid svg .node rect')
      const values = await tokens(page, ['raised', 'text'])
      const node = await computed(page, 'pre.mermaid .node rect', ['fill'])
      assert.ok(sameColor(parseColor(node.fill), parseColor(values.raised)), `node fill is ${node.fill}`)
      const label = await computed(page, 'pre.mermaid .nodeLabel', ['color'])
      assert.ok(sameColor(parseColor(label.color), parseColor(values.text)), `node label is ${label.color}`)
    } finally {
      await close()
    }
  })

  /* Markers live in <defs>, so they are never "visible" to waitForSelector. */
  test(`${theme}: Mermaid markers are recoloured from the tokens`, async () => {
    const { page, close } = await site.open(PAGE, { theme })
    try {
      await diagramsRendered(page)
      const values = await tokens(page, ['surface', 'muted'])

      const zero = await computed(page, 'pre.mermaid marker[id*="er-zeroOr"] circle', ['fill', 'stroke'])
      assert.ok(sameColor(parseColor(zero.fill), parseColor(values.surface)), `ER zero marker fill is ${zero.fill}`)
      assert.ok(sameColor(parseColor(zero.stroke), parseColor(values.muted)), `ER zero marker stroke is ${zero.stroke}`)

      const head = await computed(page, 'pre.mermaid marker[id$="arrowhead"] path', ['fill'])
      assert.ok(sameColor(parseColor(head.fill), parseColor(values.muted)), `sequence arrowhead fill is ${head.fill}`)
    } finally {
      await close()
    }
  })
}

test('on a phone, diagrams keep their natural size and scroll inside their frame', async () => {
  const { page, close } = await site.open(PAGE, { width: 375, height: 800 })
  try {
    await diagramsRendered(page)
    const diagrams = await page.$$eval('pre.mermaid', frames =>
      frames.map(frame => {
        const svg = frame.querySelector(':scope > svg')
        const box = svg.getBoundingClientRect()
        return {
          natural: svg.viewBox.baseVal.width,
          width: box.width,
          clipped: box.left < frame.getBoundingClientRect().left,
          scrolls: frame.scrollWidth > frame.clientWidth,
          fits: svg.viewBox.baseVal.width <= frame.clientWidth,
        }
      })
    )
    for (const [index, diagram] of diagrams.entries()) {
      assert.ok(diagram.width >= diagram.natural - 1, `diagram ${index} shrank to ${diagram.width}px of ${diagram.natural}px`)
      assert.equal(diagram.clipped, false, `diagram ${index} starts left of its frame, out of scroll reach`)
      if (!diagram.fits) {
        assert.ok(diagram.scrolls, `diagram ${index} is wider than its frame but does not scroll`)
      }
    }
  } finally {
    await close()
  }
})

test('on a wide screen, diagrams never overflow the reading column', async () => {
  const { page, close } = await site.open(PAGE)
  try {
    await diagramsRendered(page)
    const overflows = await page.$$eval('pre.mermaid', frames => frames.map(frame => frame.scrollWidth - frame.clientWidth))
    assert.deepEqual(overflows.filter(overflow => overflow > 0), [])
  } finally {
    await close()
  }
})

test('code blocks are labelled with their language', async () => {
  const { page, close } = await site.open(PAGE)
  try {
    assert.equal(await page.$eval(CSHARP, pre => pre.dataset.pdLang), 'C#')
    const label = await computed(page, CSHARP, ['content'], '::before')
    assert.equal(label.content, '"C#"')
    assert.equal(await page.$eval('pre:has(> code.lang-bash)', pre => pre.dataset.pdLang), 'Shell')
  } finally {
    await close()
  }
})

test('the copy button is reachable and visible by keyboard', async () => {
  const { page, close } = await site.open(PAGE)
  try {
    await page.waitForSelector('.code-action')
    let reached = false
    for (let i = 0; i < 120 && !reached; i++) {
      await page.keyboard.press('Tab')
      reached = await page.evaluate(() => document.activeElement?.matches('.code-action') ?? false)
    }
    assert.ok(reached, 'Tab never reached a copy button')
    await page.waitForTimeout(250)
    assert.equal(await page.evaluate(() => getComputedStyle(document.activeElement).opacity), '1')
  } finally {
    await close()
  }
})

test('inline code is a borderless tint that wraps as one span', async () => {
  const { page, close } = await site.open(PAGE)
  try {
    const code = await computed(page, '.content article p > code', ['border-top-width', 'box-decoration-break'])
    assert.equal(code['border-top-width'], '0px')
    assert.equal(code['box-decoration-break'], 'clone')
  } finally {
    await close()
  }
})

test('the component page does not overflow horizontally at 375px', async () => {
  const { page, close } = await site.open(PAGE, { width: 375, height: 800 })
  try {
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    assert.ok(overflow <= 0, `page overflows by ${overflow}px`)
  } finally {
    await close()
  }
})
