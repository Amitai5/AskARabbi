import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'
import { resolve } from 'node:path'

const homepage = resolve(import.meta.dirname, 'index.html')

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    {
      name: 'website-development-entry',
      apply: 'serve',
      transformIndexHtml: {
        order: 'pre',
        handler: (_, context) => resolve(context.filename) === homepage ? [{ tag: 'script', attrs: { type: 'module', src: '/src/main.tsx' }, injectTo: 'body' }] : [],
      },
    },
  ],
  build: {
    rolldownOptions: {
      input: {
        main: homepage,
        privacy: resolve(import.meta.dirname, 'privacy.html'),
        terms: resolve(import.meta.dirname, 'terms.html'),
      },
    },
  },
  server: {
    host: '127.0.0.1',
    port: 5174,
    strictPort: true,
  },
  preview: {
    host: '127.0.0.1',
    port: 4174,
    strictPort: true,
  },
})
