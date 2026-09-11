import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import { OfflineDvarTorahPage } from './features/pwa/OfflineDvarTorahPage.tsx'
import { CachedReadingPreferencesProvider } from './features/reading/ReadingPreferencesProvider.tsx'
import { FocusedReadingProvider } from './features/reading/FocusedReading.tsx'

createRoot(document.getElementById('root')!).render(<StrictMode><CachedReadingPreferencesProvider><FocusedReadingProvider><OfflineDvarTorahPage /></FocusedReadingProvider></CachedReadingPreferencesProvider></StrictMode>)
