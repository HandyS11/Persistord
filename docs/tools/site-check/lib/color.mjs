/**
 * Colour maths for the contrast checks. Channels are 0-1 floats; alpha is the
 * fourth element. WCAG 2.x relative luminance and contrast ratio.
 */

function alphaOf(raw) {
  if (raw === undefined) {
    return 1
  }
  return raw.endsWith('%') ? Number(raw.slice(0, -1)) / 100 : Number(raw)
}

/**
 * The arguments of a functional colour notation: three numeric channels and an
 * optional alpha, separated by commas, spaces or a slash. Null if malformed.
 */
function argumentsOf(body) {
  const parts = body.trim().split(/[\s,/]+/)
  const numeric = parts.length >= 3 && parts.length <= 4 && parts.every(part => /^[\d.]+%?$/.test(part))
  return numeric && parts.slice(0, 3).every(part => !part.endsWith('%')) ? parts : null
}

/** Parses a hex token or any colour string getComputedStyle returns. */
export function parseColor(value) {
  const text = value.trim()

  const hex = /^#([0-9a-f]{6})$/i.exec(text)
  if (hex) {
    return [0, 2, 4].map(i => Number.parseInt(hex[1].slice(i, i + 2), 16) / 255).concat(1)
  }

  const rgb = /^rgba?\((.*)\)$/i.exec(text)
  const rgbArgs = rgb && argumentsOf(rgb[1])
  if (rgbArgs) {
    return rgbArgs.slice(0, 3).map(v => Number(v) / 255).concat(alphaOf(rgbArgs[3]))
  }

  const srgb = /^color\(srgb\s(.*)\)$/i.exec(text)
  const srgbArgs = srgb && argumentsOf(srgb[1])
  if (srgbArgs) {
    return srgbArgs.slice(0, 3).map(Number).concat(alphaOf(srgbArgs[3]))
  }

  throw new Error(`Unparseable colour: "${value}"`)
}

/** Composites a translucent foreground over an opaque background. */
export function over([r, g, b, a], [br, bg, bb]) {
  return [r * a + br * (1 - a), g * a + bg * (1 - a), b * a + bb * (1 - a), 1]
}

const linear = c => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4)

export function luminance([r, g, b]) {
  return 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b)
}

export function contrast(a, b) {
  const [high, low] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (high + 0.05) / (low + 0.05)
}

export function sameColor(a, b, tolerance = 1.5 / 255) {
  return a.every((channel, i) => Math.abs(channel - b[i]) <= tolerance)
}
