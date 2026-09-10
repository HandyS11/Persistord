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

export default {
  defaultTheme: 'dark',

  /* docfx merges this over its own `{ startOnLoad, theme }`, picking the base
     theme from the current light/dark setting. Only theme-agnostic values are
     set here: anything with a fixed lightness would be wrong in one of the two
     themes, so surfaces and text are left to mermaid's own ramp. */
  mermaid: {
    fontFamily:
      "'Persistord Mono', ui-monospace, 'SFMono-Regular', Consolas, monospace",
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
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', wireCopyButtons, {
        once: true,
      })
    } else {
      wireCopyButtons()
    }
  },
}
