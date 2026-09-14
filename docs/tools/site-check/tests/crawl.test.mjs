import assert from 'node:assert/strict'
import { readdir, readFile, stat } from 'node:fs/promises'
import { dirname, join, relative, resolve } from 'node:path'
import { test } from 'node:test'
import { BASE, SITE_ROOT } from '../lib/server.mjs'

async function* htmlFiles(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const full = join(directory, entry.name)
    if (entry.isDirectory()) {
      yield* htmlFiles(full)
    } else if (entry.name.endsWith('.html')) {
      yield full
    }
  }
}

async function exists(path) {
  try {
    const info = await stat(path)
    return info.isFile() || (await stat(join(path, 'index.html'))).isFile()
  } catch {
    return false
  }
}

const SKIP = /^(?:#|mailto:|javascript:|data:|tel:)/i
const ABSOLUTE = /^(?:[a-z][a-z0-9+.-]*:)?\/\//i

test('every internal link and asset in _site resolves', async () => {
  const broken = []
  for await (const file of htmlFiles(SITE_ROOT)) {
    const html = await readFile(file, 'utf8')
    for (const [, url] of html.matchAll(/\s(?:href|src)="([^"]*)"/g)) {
      if (!url || SKIP.test(url) || ABSOLUTE.test(url)) {
        continue
      }
      const path = decodeURI(url.split(/[?#]/)[0])
      if (!path) {
        continue
      }
      const target = path.startsWith(BASE)
        ? join(SITE_ROOT, path.slice(BASE.length))
        : resolve(dirname(file), path)
      if (!(await exists(target))) {
        broken.push(`${relative(SITE_ROOT, file)} -> ${url}`)
      }
    }
  }
  assert.deepEqual(broken.slice(0, 50), [], `${broken.length} broken internal links`)
})

test('no theme asset loads from a third-party origin', async () => {
  const offenders = []
  for await (const file of htmlFiles(SITE_ROOT)) {
    const html = await readFile(file, 'utf8')
    const page = relative(SITE_ROOT, file)
    const isReadme = /^(src\/[^/]+|samples)\/README\.html$/.test(page.replaceAll('\\', '/'))

    for (const [tag, name] of html.matchAll(/<(link|script|img|iframe|source)\b[^>]*>/gi)) {
      const url = /\s(?:href|src)="([^"]*)"/i.exec(tag)?.[1] ?? ''
      if (!ABSOLUTE.test(url)) {
        continue
      }
      if (/^link$/i.test(name) && !/rel="(?:stylesheet|icon|preload|modulepreload)"/i.test(tag)) {
        continue
      }
      /* Accepted deviation: shield badges authored into the package READMEs. */
      if (/^img$/i.test(name) && isReadme && new URL(url, 'https://x').host === 'img.shields.io') {
        continue
      }
      offenders.push(`${page}: ${url}`)
    }
  }

  for (const css of ['public/main.css', ...(await readdir(join(SITE_ROOT, 'public/css'))).map(name => `public/css/${name}`)]) {
    const text = await readFile(join(SITE_ROOT, css), 'utf8')
    for (const [match] of text.matchAll(/url\(\s*['"]?(?:https?:)?\/\/[^)]*\)/gi)) {
      offenders.push(`${css}: ${match}`)
    }
  }

  assert.deepEqual(offenders, [])
})
