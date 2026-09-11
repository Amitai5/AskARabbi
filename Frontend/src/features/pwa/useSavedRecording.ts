import { useEffect, useState } from 'react'
import { validateAudioTimings } from '../dvarTorah/dvarTorahAudio.ts'
import { normalizeDvarTorahText } from '../dvarTorah/dvarTorahText.ts'
import type { DvarTorahAudioTimings } from '../dvarTorah/dvarTorahTypes.ts'
import { OfflineLibraryChanged, readOfflineLibrary } from './offlineLibrary.ts'

interface SavedRecording {
  url: string
  timings: DvarTorahAudioTimings | null
}

export function useSavedRecording(weekKey: string, version: string | undefined, title: string, body: string) {
  const [recording, setRecording] = useState<SavedRecording | null>(null)

  useEffect(() => {
    if (!version || typeof URL.createObjectURL !== 'function') { return }
    const recordingVersion = version
    let active = true
    let request = 0
    let objectUrl: string | null = null
    function refresh() {
      const currentRequest = ++request
      void readOfflineLibrary().then(library => {
        if (!active || request !== currentRequest) { return }
        const saved = library.teaching
        const article = saved?.publication.dvarTorah
        const matches = library.audioEnabled && saved?.audio && article?.week.weekKey === weekKey && article.audio?.version === recordingVersion
          && normalizeDvarTorahText(article.title) === title && normalizeDvarTorahText(article.body) === body
        if (!matches || !saved?.audio) {
          if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null }
          setRecording(null)
          return
        }
        objectUrl ??= URL.createObjectURL(saved.audio)
        setRecording({ url: objectUrl, timings: validateAudioTimings(saved.timings, recordingVersion, title, body) })
      }).catch(() => { /* An unavailable local copy must not prevent online streaming. */ })
    }
    refresh()
    window.addEventListener('offline', refresh)
    window.addEventListener('focus', refresh)
    window.addEventListener(OfflineLibraryChanged, refresh)
    return () => {
      active = false
      if (objectUrl) { URL.revokeObjectURL(objectUrl) }
      window.removeEventListener('offline', refresh)
      window.removeEventListener('focus', refresh)
      window.removeEventListener(OfflineLibraryChanged, refresh)
    }
  }, [body, title, version, weekKey])

  return recording
}
