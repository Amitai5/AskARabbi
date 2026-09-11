import { createHash } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import type { Plugin } from 'vite'

// A small build-time allowlist avoids runtime caching of any authenticated API response.
export function pwaBuild(): Plugin {
  let root = ''
  return {
    name: 'askarabbi-offline-build',
    apply: 'build',
    configResolved(config) { root = config.root },
    generateBundle: {
      // Vite must finish emitting the root offline.html entry before it is hashed.
      order: 'post',
      handler(_, bundle) {
        const assets = ['/offline.html', '/reading-bootstrap.js', '/favicon.svg', '/manifest.webmanifest', '/icons/icon-192.png', '/icons/icon-512.png', '/icons/apple-touch-icon.png',
          ...Object.keys(bundle).filter(path => path.startsWith('assets/') && !path.endsWith('.map')).map(path => `/${path}`)].sort()
        const template = readFileSync(resolve(root, 'public/sw.js'), 'utf8')
        const hash = createHash('sha256').update(template)
        for (const path of assets) {
          const output = bundle[path.slice(1)]
          hash.update(path).update(output ? output.type === 'chunk' ? output.code : output.source : readFileSync(resolve(root, `public${path}`)))
        }
        this.emitFile({ type: 'asset', fileName: 'sw.js', source: template.replace('__BUILD_VERSION__', hash.digest('hex').slice(0, 16)).replace('__OFFLINE_ASSETS__', JSON.stringify(assets)) })
      },
    },
  }
}
