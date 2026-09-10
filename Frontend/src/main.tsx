import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { registerServiceWorker } from './features/pwa/registerServiceWorker.ts'

const root = document.getElementById('root')

if (root === null) {
  throw new Error('The application root element was not found.')
}

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

void registerServiceWorker()
