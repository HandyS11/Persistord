import { createServer } from 'node:http'
import { readFile, stat } from 'node:fs/promises'
import { extname, join, normalize } from 'node:path'
import { fileURLToPath } from 'node:url'

export const SITE_ROOT = fileURLToPath(new URL('../../../_site/', import.meta.url))
export const BASE = '/Persistord/'

const TYPES = {
  '.css': 'text/css; charset=utf-8',
  '.html': 'text/html; charset=utf-8',
  '.ico': 'image/x-icon',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.woff2': 'font/woff2',
  '.xml': 'application/xml; charset=utf-8',
}

async function resolveFile(pathname) {
  try {
    const relative = decodeURIComponent(pathname.slice(BASE.length))
    const candidate = normalize(join(SITE_ROOT, relative))
    if (!candidate.startsWith(SITE_ROOT)) {
      return null
    }
    const info = await stat(candidate)
    if (!info.isDirectory()) {
      return candidate
    }
    const index = join(candidate, 'index.html')
    await stat(index)
    return index
  } catch {
    return null
  }
}

/**
 * Serves docs/_site under /Persistord/, the path GitHub Pages publishes it at.
 * Like Pages, every miss is answered with the site's own 404.html and status
 * 404 - served at the missing URL, which is what makes relative asset links
 * on a 404 page break at depth.
 */
export async function startServer() {
  const server = createServer(async (request, response) => {
    const { pathname } = new URL(request.url, 'http://localhost')
    const file = pathname.startsWith(BASE) ? await resolveFile(pathname) : null

    if (file) {
      response.writeHead(200, { 'content-type': TYPES[extname(file)] ?? 'application/octet-stream' })
      response.end(await readFile(file))
      return
    }

    const notFound = await readFile(join(SITE_ROOT, '404.html')).catch(() => 'Not found')
    response.writeHead(404, { 'content-type': TYPES['.html'] })
    response.end(notFound)
  })

  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve))
  const origin = `http://127.0.0.1:${server.address().port}`

  return {
    origin,
    url: path => `${origin}${BASE}${path.replace(/^\//, '')}`,
    close: () => new Promise(resolve => server.close(resolve)),
  }
}
