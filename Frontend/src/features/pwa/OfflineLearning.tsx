import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import { Download, LoaderCircle } from 'lucide-react'
import type { DvarTorahClient } from '../dvarTorah/dvarTorahClient.ts'
import { OfflineLibraryChanged, OfflineLibraryCleared, readOfflineLibrary, setOfflineAudioEnabled, type OfflineLibrary } from './offlineLibrary.ts'
import { syncOfflineTeaching } from './syncOfflineTeaching.ts'

interface OfflineLearningState {
  library: OfflineLibrary | null
  isSaving: boolean
  error: string | null
  changeAudio(enabled: boolean): Promise<void>
  refresh(): void
}
const OfflineLearningContext = createContext<OfflineLearningState | null>(null)

export function OfflineLearningProvider({ client, children }: { client: DvarTorahClient; children: ReactNode }) {
  const [library, setLibrary] = useState<OfflineLibrary | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const requestRef = useRef<AbortController | null>(null)
  const clearedRef = useRef(false)

  const refresh = useCallback(() => {
    if (requestRef.current || clearedRef.current || !('indexedDB' in window)) { return }
    const controller = new AbortController()
    requestRef.current = controller
    setIsSaving(true)
    setError(null)
    async function refreshLibrary() {
      const value = await readOfflineLibrary()
      if (!controller.signal.aborted) { setLibrary(value) }
    }
    void (async () => {
      try {
        await refreshLibrary()
        if (navigator.onLine && !controller.signal.aborted) {
          await syncOfflineTeaching(client, controller.signal, () => { void refreshLibrary().catch(() => undefined) })
        }
        await refreshLibrary()
      } catch {
        if (!controller.signal.aborted) { setError('Offline saving could not finish. Check your connection and free storage, then try again.') }
      } finally {
        if (requestRef.current === controller) {
          requestRef.current = null
          setIsSaving(false)
        }
      }
    })()
  }, [client])

  useEffect(() => {
    clearedRef.current = false
    function clear() {
      clearedRef.current = true
      requestRef.current?.abort()
      requestRef.current = null
      setIsSaving(false)
      setLibrary(null)
    }
    refresh()
    window.addEventListener('online', refresh)
    window.addEventListener('focus', refresh)
    window.addEventListener(OfflineLibraryCleared, clear)
    return () => {
      requestRef.current?.abort()
      requestRef.current = null
      window.removeEventListener('online', refresh)
      window.removeEventListener('focus', refresh)
      window.removeEventListener(OfflineLibraryCleared, clear)
    }
  }, [refresh])

  async function changeAudio(enabled: boolean) {
    requestRef.current?.abort()
    requestRef.current = null
    setIsSaving(true)
    setError(null)
    try {
      setLibrary(await setOfflineAudioEnabled(enabled))
      window.dispatchEvent(new Event(OfflineLibraryChanged))
      if (enabled) { refresh() }
    } catch {
      setError('This device’s offline preference could not be saved. Please try again.')
    } finally {
      if (!requestRef.current) { setIsSaving(false) }
    }
  }

  return <OfflineLearningContext.Provider value={{ library, isSaving, error, changeAudio, refresh }}>{children}</OfflineLearningContext.Provider>
}

export function OfflineLearningSettings() {
  const state = useContext(OfflineLearningContext)
  const enabled = state?.library?.audioEnabled ?? true
  return (
    <div>
      <div className="flex items-start justify-between gap-5">
        <div>
          <p className="font-semibold text-ink">Make weekly audio available offline</p>
          <p id="offline-audio-description" className="mt-1 text-muted">Save this week’s recording with word highlighting and tap-to-seek. Uses this device’s storage. Turn off to keep text and references only.</p>
        </div>
        <button type="button" role="switch" aria-checked={enabled} aria-label="Make weekly audio available offline" aria-describedby="offline-audio-description" disabled={!state?.library || state.isSaving && !state.library.teaching} onClick={() => void state?.changeAudio(!enabled)} className={`relative mt-1 h-7 w-12 shrink-0 rounded-full transition disabled:opacity-50 ${enabled ? 'bg-pomegranate' : 'bg-stone-deep'}`}><span className={`absolute top-1 size-5 rounded-full bg-white shadow-sm transition-all ${enabled ? 'left-6' : 'left-1'}`} /></button>
      </div>
      <p className="mt-3 text-sm leading-6 text-muted">Saved automatically on this device, not in your account. Only the latest teaching is kept—never chats or account details. Anyone using this browser can read it; logging out removes it.</p>
      <OfflineLearningStatus />
      {state?.error ? <div className="mt-3"><p role="alert" className="text-sm text-pomegranate">{state.error}</p><button type="button" onClick={state.refresh} className="mt-2 min-h-11 text-sm font-semibold text-pomegranate">Try offline download again</button></div> : null}
      <a id="setting-saved-teaching" href="/offline.html" className="settings-target mt-3 inline-flex min-h-11 items-center gap-2 text-sm font-semibold text-pomegranate hover:underline"><Download aria-hidden="true" className="size-4" />Open saved teaching</a>
    </div>
  )
}

export function OfflineLearningStatus() {
  const state = useContext(OfflineLearningContext)
  if (!state) { return null }
  const saved = state.library?.teaching
  return <p role="status" className="mt-4 flex items-center gap-2 text-sm leading-6 text-muted">{state.isSaving ? <LoaderCircle aria-hidden="true" className="size-4 shrink-0 animate-spin motion-reduce:animate-none" /> : null}{state.isSaving ? 'Preparing this week for offline use…' : saved ? `Saved on this device: ${saved.audio ? 'text, references, and audio' : 'text and references'}.` : 'Open AskRabbi online to save this week’s teaching when it is published.'}</p>
}
