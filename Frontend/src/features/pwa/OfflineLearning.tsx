import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import { Download, LoaderCircle } from 'lucide-react'
import { LegalLink } from '../legal/LegalLinks.tsx'
import type { DvarTorahClient } from '../dvarTorah/dvarTorahClient.ts'
import { OfflineLibraryChanged, OfflineLibraryCleared, readOfflineLibrary, saveOfflineHolidays, setOfflineAudioEnabled, type OfflineLibrary } from './offlineLibrary.ts'
import { syncOfflineTeaching } from './syncOfflineTeaching.ts'
import { CalendarPreferencesChanged, type CalendarClient } from '../calendar/calendarClient.ts'
import { canPreloadLearning, getConnectionInformation, scheduleIdlePreload } from './backgroundConnection.ts'

interface OfflineLearningState {
  library: OfflineLibrary | null
  isSaving: boolean
  error: string | null
  changeAudio(enabled: boolean): Promise<void>
  refresh(): void
}
const OfflineLearningContext = createContext<OfflineLearningState | null>(null)

export function OfflineLearningProvider({ client, calendarClient, children }: { client: DvarTorahClient; calendarClient?: CalendarClient; children: ReactNode }) {
  const [library, setLibrary] = useState<OfflineLibrary | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const requestRef = useRef<AbortController | null>(null)
  const clearedRef = useRef(false)
  const lastAutomaticRun = useRef(0)
  const automaticRequest = useRef(false)

  const start = useCallback((automatic: boolean) => {
    if (requestRef.current || clearedRef.current || !navigator.onLine || automatic && !canPreloadLearning()) { return }
    if (automatic) { lastAutomaticRun.current = Date.now() }
    automaticRequest.current = automatic
    const controller = new AbortController()
    requestRef.current = controller
    setIsSaving(true)
    setError(null)
    async function refreshLibrary() {
      const value = await readOfflineLibrary().catch(() => null)
      if (!controller.signal.aborted) { setLibrary(value) }
    }
    void (async () => {
      try {
        const initial = await readOfflineLibrary().catch(() => null)
        controller.signal.throwIfAborted()
        // Independent downloads start together; navigation shares their in-flight requests.
        const results = await Promise.allSettled([
          initial ? syncOfflineTeaching(client, controller.signal, () => { void refreshLibrary() }) : client.getCurrent(false, controller.signal),
          calendarClient?.getOverview(360, controller.signal).then(async overview => {
            if (initial && !controller.signal.aborted) { await saveOfflineHolidays(overview, initial.revision, controller.signal) }
          }),
          import('../calendar/CalendarPage.tsx'),
          import('../dvarTorah/WeeklyDvarTorahPage.tsx'),
        ])
        if (results.some(result => result.status === 'rejected')) { throw new Error('Learning preloading was incomplete.') }
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
  }, [client, calendarClient])
  const refresh = useCallback(() => start(false), [start])

  useEffect(() => {
    clearedRef.current = false
    let active = true
    let cancelScheduled: (() => void) | null = null
    const connection = getConnectionInformation()
    function readSaved() {
      void readOfflineLibrary().then(value => { if (active && !clearedRef.current) { setLibrary(value) } }).catch(() => undefined)
    }
    function cancel() {
      cancelScheduled?.()
      cancelScheduled = null
      requestRef.current?.abort()
      requestRef.current = null
      setIsSaving(false)
    }
    function schedule() {
      cancelScheduled?.()
      cancelScheduled = null
      if (!canPreloadLearning()) {
        if (automaticRequest.current) { cancel(); lastAutomaticRun.current = 0 }
        return
      }
      if (requestRef.current || clearedRef.current || lastAutomaticRun.current > 0 && Date.now() - lastAutomaticRun.current < 5 * 60 * 1000) { return }
      cancelScheduled = scheduleIdlePreload(() => { cancelScheduled = null; start(true) })
    }
    function settingsChanged() { cancel(); lastAutomaticRun.current = 0; readSaved(); schedule() }
    function clear() {
      clearedRef.current = true
      cancel()
      setLibrary(null)
    }
    readSaved()
    schedule()
    window.addEventListener('online', schedule)
    window.addEventListener('offline', schedule)
    window.addEventListener('focus', schedule)
    document.addEventListener('visibilitychange', schedule)
    connection?.addEventListener('change', schedule)
    window.addEventListener(CalendarPreferencesChanged, settingsChanged)
    window.addEventListener(OfflineLibraryChanged, readSaved)
    window.addEventListener(OfflineLibraryCleared, clear)
    return () => {
      active = false
      cancel()
      lastAutomaticRun.current = 0
      window.removeEventListener('online', schedule)
      window.removeEventListener('offline', schedule)
      window.removeEventListener('focus', schedule)
      document.removeEventListener('visibilitychange', schedule)
      connection?.removeEventListener('change', schedule)
      window.removeEventListener(CalendarPreferencesChanged, settingsChanged)
      window.removeEventListener(OfflineLibraryChanged, readSaved)
      window.removeEventListener(OfflineLibraryCleared, clear)
    }
  }, [start])

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
      <p className="mt-3 text-sm leading-6 text-muted">On a good connection, this week’s teaching and the next 360 days of holidays save in the background. Slow connections and Data Saver skip automatic downloads. The offline library excludes chats, your location, and account details. Anyone using this browser can read saved learning. Signing out attempts to remove it; clear this site’s browser data on shared devices. See <LegalLink document="privacy-policy" section="device-storage">device storage and privacy</LegalLink> (available online).</p>
      {state?.isSaving ? <p role="status" className="mt-4 flex items-center gap-2 text-sm leading-6 text-muted"><LoaderCircle aria-hidden="true" className="size-4 shrink-0 animate-spin motion-reduce:animate-none" />Preparing learning for offline use…</p> : null}
      {state?.error ? <div className="mt-3"><p role="alert" className="text-sm text-pomegranate">{state.error}</p><button type="button" onClick={state.refresh} className="mt-2 min-h-11 text-sm font-semibold text-pomegranate">Try offline download again</button></div> : null}
      <a id="setting-saved-teaching" href="/offline.html" className="settings-target mt-3 inline-flex min-h-11 items-center gap-2 text-sm font-semibold text-pomegranate hover:underline"><Download aria-hidden="true" className="size-4" />Open saved teaching</a>
      <a href="/offline.html#holidays" className="ml-4 mt-3 inline-flex min-h-11 items-center text-sm font-semibold text-pomegranate hover:underline">Open saved holidays</a>
    </div>
  )
}
