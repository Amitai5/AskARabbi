import { useEffect, useState } from 'react'
import { ArrowLeft, WifiOff } from 'lucide-react'
import { Brand } from '../../components/Brand.tsx'
import { WeeklyDvarTorahPage } from '../dvarTorah/WeeklyDvarTorahPage.tsx'
import type { DvarTorahClient } from '../dvarTorah/dvarTorahClient.ts'
import { OfflineLibraryChanged, readOfflineLibrary, type SavedTeaching } from './offlineLibrary.ts'

interface OfflineReader {
  client: DvarTorahClient
  savedAt: string
  hasAudio: boolean
}

export function OfflineDvarTorahPage() {
  const [reader, setReader] = useState<OfflineReader | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    const refresh = () => setRevision(value => value + 1)
    window.addEventListener('focus', refresh)
    window.addEventListener(OfflineLibraryChanged, refresh)
    return () => {
      window.removeEventListener('focus', refresh)
      window.removeEventListener(OfflineLibraryChanged, refresh)
    }
  }, [])
  useEffect(() => {
    let active = true
    let objectUrl: string | null = null
    void readOfflineLibrary().then(library => {
      if (!active) { return }
      const teaching = library.teaching
      if (!teaching?.publication.dvarTorah) { setReader(null); return }
      objectUrl = library.audioEnabled && teaching.audio ? URL.createObjectURL(teaching.audio) : null
      setReader({ client: createOfflineClient(teaching, objectUrl), savedAt: teaching.savedAt, hasAudio: objectUrl !== null })
    }).catch(() => {
      if (active) { setError('Offline storage could not be opened. Reconnect and check your browser’s storage settings.'); setReader(null) }
    }).finally(() => { if (active) { setIsLoading(false) } })
    return () => { active = false; if (objectUrl) { URL.revokeObjectURL(objectUrl) } }
  }, [revision])

  return (
    <main className="flex h-dvh min-h-0 flex-col overflow-hidden bg-parchment text-ink">
      <header className="flex shrink-0 flex-wrap items-center justify-between gap-3 border-b border-line px-4 py-3 sm:px-8">
        <Brand compact />
        <a href="/" className="inline-flex min-h-11 items-center gap-2 rounded-lg px-3 text-sm font-semibold text-ink-soft hover:text-pomegranate"><ArrowLeft aria-hidden="true" className="size-4" />Back to AskRabbi online</a>
      </header>
      <div className="shrink-0 border-b border-line px-4 py-2 text-center text-sm leading-6 text-muted" role="status"><WifiOff aria-hidden="true" className="mr-2 inline size-4" />Offline library · {reader?.hasAudio ? 'Text, references, and audio saved on this device.' : 'Audio is only available here after its offline download finishes. Change this in Settings.'}</div>
      {reader ? <WeeklyDvarTorahPage key={reader.savedAt} client={reader.client} offlineSavedAt={reader.savedAt} /> : <section className="flex min-h-0 flex-1 items-center justify-center overflow-y-auto p-6 text-center"><div className="max-w-md"><h1 className="font-display text-3xl">{isLoading ? 'Opening your saved teaching…' : 'No teaching saved yet.'}</h1><p className="mt-4 text-base leading-7 text-ink-soft">{error ?? 'Connect and sign in to AskRabbi to save this week’s D’var Torah. Chats and account details are not stored in the offline library.'}</p></div></section>}
    </main>
  )
}

function createOfflineClient(teaching: SavedTeaching, audioUrl: string | null): DvarTorahClient {
  const publication = { ...teaching.publication, dvarTorah: teaching.publication.dvarTorah ? { ...teaching.publication.dvarTorah, audio: audioUrl ? teaching.publication.dvarTorah.audio : null } : null }
  return {
    getCurrent: async () => publication,
    getArchive: async () => ({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 }),
    getArchived: async () => { throw new Error('Past teachings require a connection.') },
    getAudioUrl: () => audioUrl ?? '',
    getAudioTimings: async () => teaching.timings,
  }
}
