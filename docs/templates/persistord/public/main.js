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

  /* docfx merges this over its own `{ startOnLoad, theme }`. Only the font is
     set here - colours come from components.css, which recolours the rendered
     SVG from the design tokens so a diagram follows a theme switch. */
  mermaid: {
    fontFamily:
      "'Persistord Sans', system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif",
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
      wireSearchShortcut()
      trackAffix()
    })
  },
}
