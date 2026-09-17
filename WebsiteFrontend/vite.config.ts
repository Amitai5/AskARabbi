import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    {
      name: 'website-development-entry',
      apply: 'serve',
      transformIndexHtml: {
        order: 'pre',
        handler: () => [{ tag: 'script', attrs: { type: 'module', src: '/src/main.tsx' }, injectTo: 'body' }],
      },
    },
  ],
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
