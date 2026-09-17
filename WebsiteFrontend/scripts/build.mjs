import { pathToFileURL } from 'node:url'
import { resolve } from 'node:path'
import { build } from 'vite'

// Render once at build time. The published page needs only HTML, CSS, and its local artwork.
const renderDirectory = 'node_modules/.website-prerender'

await build({
  build: {
    ssr: 'src/entry-server.tsx',
    outDir: renderDirectory,
    emptyOutDir: true,
    copyPublicDir: false,
  },
})

const { render } = await import(pathToFileURL(resolve(renderDirectory, 'entry-server.js')).href)
const markup = render()

await build({
  plugins: [{
    name: 'website-static-html',
    transformIndexHtml(html) {
      const outlet = '<!--app-html-->'
      if (!html.includes(outlet)) {
        throw new Error('The website template is missing its static HTML outlet.')
      }
      return html.replace(outlet, () => markup)
    },
  }],
})
