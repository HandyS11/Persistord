/*
 * Persistord documentation theme behaviour.
 *
 * Uses only docfx's documented `main.js` extension points (`defaultTheme`,
 * `iconLinks`, `mermaid`, `start`) so a docfx upgrade does not break it.
 */

const COPY_SHORTCUT = /Mac|iPhone|iPad/.test(navigator.userAgent)
  ? '⌘C'
  : 'Ctrl+C'

/**
 * Puts a node's text under the user's selection, ready for a manual copy.
 * Returns whether the selection was actually made, so the caller can avoid
 * telling the user to press a copy shortcut over an empty selection.
 */
function selectContents(node) {
  /* getSelection() is null when the window has no associated document. */
  const selection = window.getSelection()
  if (!selection) {
    return false
  }

  const range = document.createRange()
  range.selectNodeContents(node)
  selection.removeAllRanges()
  selection.addRange(range)
  return true
}

/**
 * Copies the adjacent command, then reports the result on the button itself.
 *
 * The button carries `aria-live="polite"` (set in the markup, so the contract holds
 * even if this module never loads), which makes every message below - the success,
 * the shortcut prompt, and the manual-selection fallback - reach a screen reader
 * instead of only the sighted reader. It deliberately does NOT carry `role="status"`:
 * an explicit role replaces the implicit one, so `<button role="status">` is exposed
 * as a status and stops being announced as a button at all. `aria-live` on its own
 * adds the announcement and leaves the role intact.
 */
function wireCopyButtons() {
  for (const button of document.querySelectorAll('.pd-copy')) {
    const code = button.parentElement?.querySelector('code')
    if (!code) {
      continue
    }

    const label = button.textContent
    let revertTimer

    /* Every outcome reverts to the original label, so the button can never be
       left showing a stale message. */
    const report = (message, copied, revertAfterMs) => {
      clearTimeout(revertTimer)
      button.textContent = message
      button.dataset.copied = copied
      revertTimer = setTimeout(() => {
        button.textContent = label
        button.dataset.copied = 'false'
      }, revertAfterMs)
    }

    button.addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(code.textContent.trim())
        report('Copied', 'true', 1600)
      } catch {
        /* Clipboard access can be refused - over plain HTTP, or by permission.
           Select the command first so the shortcut has something to act on, and
           fall back to asking for a manual selection when even that is
           unavailable. Either way the button reports something. */
        const selected = selectContents(code)
        report(
          selected ? `Press ${COPY_SHORTCUT}` : 'Select to copy',
          'false',
          4000
        )
      }
    })
  }
}

/* Display names for the fence languages the docs use. A language missing here
   simply gets no label. */
const LANGUAGE_LABELS = {
  bash: 'Shell',
  console: 'Shell',
  cs: 'C#',
  csharp: 'C#',
  css: 'CSS',
  diff: 'Diff',
  html: 'HTML',
  ini: 'INI',
  javascript: 'JavaScript',
  js: 'JavaScript',
  json: 'JSON',
  markdown: 'Markdown',
  md: 'Markdown',
  powershell: 'PowerShell',
  pwsh: 'PowerShell',
  sh: 'Shell',
  shell: 'Shell',
  sql: 'SQL',
  toml: 'TOML',
  xml: 'XML',
  yaml: 'YAML',
  yml: 'YAML',
}

/**
 * Tags each fenced code block with a display name for its language, which
 * components.css renders as a caption. API signatures (.codewrapper) are always
 * C# and stay unlabelled.
 */
function labelCodeBlocks() {
  for (const code of document.querySelectorAll('.content article pre > code[class*="lang-"]')) {
    const pre = code.parentElement
    if (pre.closest('.codewrapper')) {
      continue
    }
    const language = [...code.classList].find(name => name.startsWith('lang-'))?.slice(5)
    const label = LANGUAGE_LABELS[language]
    if (label) {
      pre.dataset.pdLang = label
    }
  }
}

/**
 * Upgrades each `[data-pd-tabs]` container of stacked panels into a tabs
 * widget, following the WAI-ARIA Authoring Practices pattern with automatic
 * activation: one tab stop (roving tabindex), Left/Right arrows that wrap, and
 * Home/End. Panels are authored stacked, each under its own h4, so the page
 * still reads correctly when this never runs; the h4 becomes the tab's label
 * and landing.css hides it once the tablist exists.
 */
function wireTabs() {
  for (const container of document.querySelectorAll('[data-pd-tabs]')) {
    const panels = [...container.querySelectorAll(':scope > [data-pd-tab]')]
    if (panels.length < 2) {
      continue
    }

    const tablist = document.createElement('div')
    tablist.className = 'pd-tablist'
    tablist.setAttribute('role', 'tablist')
    tablist.setAttribute('aria-label', container.dataset.pdTabs)

    const tabs = panels.map(panel => {
      const tab = document.createElement('button')
      tab.type = 'button'
      tab.className = 'pd-tab'
      tab.id = `${panel.id}-tab`
      tab.textContent = panel.querySelector(':scope > h4')?.textContent.trim() ?? panel.id
      tab.setAttribute('role', 'tab')
      tab.setAttribute('aria-controls', panel.id)
      panel.setAttribute('role', 'tabpanel')
      panel.setAttribute('aria-labelledby', tab.id)
      tablist.append(tab)
      return tab
    })

    const select = (index, focus) => {
      tabs.forEach((tab, i) => {
        const selected = i === index
        tab.setAttribute('aria-selected', String(selected))
        tab.tabIndex = selected ? 0 : -1
        panels[i].hidden = !selected
      })
      if (focus) {
        tabs[index].focus()
      }
    }

    tablist.addEventListener('click', event => {
      const index = tabs.indexOf(event.target.closest('[role="tab"]'))
      if (index >= 0) {
        select(index, false)
      }
    })

    tablist.addEventListener('keydown', event => {
      const current = tabs.indexOf(document.activeElement)
      if (current < 0) {
        return
      }
      const last = tabs.length - 1
      const next = {
        ArrowRight: current === last ? 0 : current + 1,
        ArrowLeft: current === 0 ? last : current - 1,
        Home: 0,
        End: last,
      }[event.key]
      if (next === undefined) {
        return
      }
      event.preventDefault()
      select(next, true)
    })

    container.prepend(tablist)
    container.classList.add('pd-tabs-ready')
    select(0, false)
  }
}

const TAB_CHOICES = 'pd-tab-choices'
const TAB_LINKS = '.tabGroup > ul > li > a[data-tab]'

/** The tab ids of the group a tab link belongs to. */
const groupTabIds = link => [...link.closest('ul').querySelectorAll('a[data-tab]')].map(tab => tab.dataset.tab)

/**
 * docfx syncs tab groups that share an id within a page and honours a `tabs`
 * query parameter, but forgets the choice on the next page. This remembers
 * each tab the reader picks for the browsing session (sessionStorage), a new
 * pick replacing the one from the same group, and on every page selects the
 * remembered tab of each group - with the same click docfx dispatches for
 * `?tabs=`, so docfx keeps syncing groups and the URL. A group the loaded URL
 * names follows the URL instead. Only a reader's click is a MouseEvent, so the
 * clicks dispatched here are never recorded as choices.
 */
function rememberTabChoices() {
  const read = () => {
    try {
      return JSON.parse(sessionStorage.getItem(TAB_CHOICES)) ?? []
    } catch {
      return []
    }
  }
  const explicit = new URLSearchParams(location.search).get('tabs')?.split(',') ?? []

  document.addEventListener('click', event => {
    const link = event.target instanceof Element ? event.target.closest(TAB_LINKS) : null
    if (!link || !(event instanceof MouseEvent)) {
      return
    }
    const group = groupTabIds(link)
    try {
      sessionStorage.setItem(TAB_CHOICES, JSON.stringify([...read().filter(id => !group.includes(id)), link.dataset.tab]))
    } catch {
      /* Storage refused: the choice lasts for this page only. */
    }
  })

  /* docfx marks each group with data-bi-name once it has wired it; the markup
     already selects the first tab, so aria-selected alone proves nothing. */
  const groups = [...document.querySelectorAll('.tabGroup')]
  let attempts = 100
  const apply = () => {
    if (groups.some(group => group.getAttribute('data-bi-name') !== 'tab-group')) {
      if (--attempts > 0) {
        setTimeout(apply, 50)
      }
      return
    }
    const choices = read()
    for (const group of groups) {
      const first = group.querySelector(TAB_LINKS)
      const ids = first ? groupTabIds(first) : []
      const id = ids.find(tab => explicit.includes(tab)) ?? [...choices].reverse().find(tab => ids.includes(tab))
      const link = id && group.querySelector(`:scope > ul > li > a[data-tab="${CSS.escape(id)}"]`)
      if (link && link.getAttribute('aria-selected') !== 'true') {
        link.dispatchEvent(new CustomEvent('click', { bubbles: true }))
      }
    }
  }
  if (groups.length > 0) {
    apply()
  }
}

/** Runs a callback once the document has parsed. */
function onReady(callback) {
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', callback, { once: true })
  } else {
    callback()
  }
}

/**
 * `/` focuses search, as on most documentation sites. It is ignored while the
 * reader is typing in any field and when the search box is collapsed away on
 * narrow screens, so it never swallows a literal slash.
 */
function wireSearchShortcut() {
  document.addEventListener('keydown', event => {
    if (event.key !== '/' || event.ctrlKey || event.metaKey || event.altKey) {
      return
    }

    const target = event.target
    if (
      target instanceof HTMLElement &&
      (target.isContentEditable || target.closest('input, textarea, select'))
    ) {
      return
    }

    const search = document.getElementById('search-query')
    if (!search || search.disabled || search.offsetParent === null) {
      return
    }

    event.preventDefault()
    search.focus()
  })
}

/**
 * Marks the "In this article" link for the section being read. docfx renders
 * the rail but does not track position. The current section is the last h2/h3
 * whose top has scrolled above the sticky header plus a small margin. The rail
 * renders asynchronously, so a MutationObserver re-applies the mark once its
 * links exist; it watches childList only, so toggling the class cannot loop.
 */
function trackAffix() {
  const headings = [
    ...document.querySelectorAll('.content article h2[id], .content article h3[id]'),
  ]
  const affix = document.getElementById('affix')
  if (headings.length === 0 || !affix) {
    return
  }

  const READING_LINE = 96
  let queued = false

  const update = () => {
    queued = false

    let current = headings[0]
    for (const heading of headings) {
      if (heading.getBoundingClientRect().top > READING_LINE) {
        break
      }
      current = heading
    }

    for (const link of affix.querySelectorAll('a')) {
      const active = decodeURIComponent(link.hash.slice(1)) === current.id
      link.classList.toggle('pd-current', active)
      if (active) {
        link.setAttribute('aria-current', 'location')
      } else {
        link.removeAttribute('aria-current')
      }
    }
  }

  const queue = () => {
    if (!queued) {
      queued = true
      requestAnimationFrame(update)
    }
  }

  window.addEventListener('scroll', queue, { passive: true })
  window.addEventListener('resize', queue)
  new MutationObserver(queue).observe(affix, { childList: true, subtree: true })
  queue()
}

export default {
  /* Follow the reader's OS preference; both themes are designed. */
  defaultTheme: 'auto',

  /* docfx merges this over its own `{ startOnLoad, theme }`. No colours are
     set here - components.css recolours the rendered SVG from the design
     tokens so a diagram follows a theme switch. useMaxWidth: false gives each
     SVG its natural width and height instead of `width: 100%`, so
     components.css can let a wide diagram scroll on a phone rather than shrink
     its labels past legibility. */
  mermaid: {
    fontFamily:
      "'Persistord Sans', system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif",
    flowchart: { useMaxWidth: false },
    sequence: { useMaxWidth: false },
    er: { useMaxWidth: false },
    class: { useMaxWidth: false },
    state: { useMaxWidth: false },
  },

  iconLinks: [
    {
      icon: 'github',
      href: 'https://github.com/HandyS11/Persistord',
      title: 'Source on GitHub',
    },
    {
      icon: 'box-seam',
      href: 'https://www.nuget.org/packages/Persistord',
      title: 'Package on NuGet',
    },
  ],

  start: () => {
    onReady(() => {
      wireCopyButtons()
      wireTabs()
      rememberTabChoices()
      labelCodeBlocks()
      wireSearchShortcut()
      trackAffix()
    })
  },
}
