import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import { OfflineDvarTorahPage } from './features/pwa/OfflineDvarTorahPage.tsx'

createRoot(document.getElementById('root')!).render(<StrictMode><OfflineDvarTorahPage /></StrictMode>)
