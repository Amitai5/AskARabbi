import { useCallback, useEffect, useEffectEvent, useMemo, useRef, useState } from 'react'
import type { RefObject } from 'react'
import { createPortal } from 'react-dom'
import { ArrowLeft, BookMarked, BookOpenText, CalendarDays, ChevronLeft, ChevronRight, Clock, LoaderCircle, RefreshCw, Search, Sparkles } from 'lucide-react'
import { SourceReader } from '../conversations/SourceReader.tsx'
import { LegalLink } from '../legal/LegalLinks.tsx'
import type { ConversationSource } from '../conversations/conversationData.ts'
import type { DvarTorahClient } from './dvarTorahClient.ts'
import { DvarTorahReadAloud, type DvarTorahPlaybackHandle } from './DvarTorahReadAloud.tsx'
import { DvarTorahNarratedText, NarratedText } from './DvarTorahNarratedText.tsx'
import { createNarratedParagraphs, estimateReadingMinutes, formatAudioTime } from './dvarTorahAudio.ts'
import { normalizeDvarTorahText } from './dvarTorahText.ts'
import { useNarrationFollow } from './useNarrationFollow.ts'
import type { DvarTorahAudioTimings, DvarTorahAudioWord, DvarTorahWeek, WeeklyDvarTorahArchiveResponse, WeeklyDvarTorahArticle, WeeklyDvarTorahResponse, WeeklyDvarTorahSource } from './dvarTorahTypes.ts'
import { FocusReadingButton } from '../reading/FocusedReading.tsx'
import { useReadingTarget } from '../reading/focusedReadingContext.ts'
import type { TeachingRoute } from '../conversations/pageRoutes.ts'
import { PrintAction } from '../printing/PrintAction.tsx'
import { TeachingReadButton } from './TeachingReadButton.tsx'
import { useTeachingReadState, type TeachingReadProgress } from './useTeachingReadState.ts'
import { useAutomaticTeachingRead } from './useAutomaticTeachingRead.ts'
import type { TeachingReadStatus } from './dvarTorahTypes.ts'
import { TeachingAskActions } from './TeachingAskActions.tsx'
import type { ConversationTeachingContext } from '../conversations/conversationData.ts'

interface WeeklyDvarTorahPageProps {
  client: DvarTorahClient
  offlineSavedAt?: string
  initialRoute?: TeachingRoute
  onNavigate?(route: TeachingRoute): void
  onAsk?(question: string): void
  onAskTeaching?(context: ConversationTeachingContext): void
  isAskDisabled?: boolean
}

const ShabbatDateFormatter = new Intl.DateTimeFormat(undefined, {
  day: 'numeric',
  month: 'long',
  timeZone: 'UTC',
  weekday: 'long',
  year: 'numeric',
})

const SourceDateFormatter = new Intl.DateTimeFormat(undefined, {
  day: 'numeric',
  month: 'short',
  timeZone: 'UTC',
  year: 'numeric',
})

const ArchivePageSize = 10

type WeeklyLearningView = 'current' | 'archive' | 'archivedArticle'

export function WeeklyDvarTorahPage({ client, offlineSavedAt, initialRoute, onNavigate, onAsk, onAskTeaching, isAskDisabled }: WeeklyDvarTorahPageProps) {
  const [publication, setPublication] = useState<WeeklyDvarTorahResponse | null>(() => client.getCachedCurrent?.() ?? null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)
  const [view, setView] = useState<WeeklyLearningView>(initialRoute?.weekKey ? 'archivedArticle' : initialRoute?.archive ? 'archive' : 'current')
  const [archive, setArchive] = useState<WeeklyDvarTorahArchiveResponse | null>(null)
  const [archiveSearchDraft, setArchiveSearchDraft] = useState(initialRoute?.search ?? '')
  const [archiveSearch, setArchiveSearch] = useState(initialRoute?.search ?? '')
  const [archiveReadStatus, setArchiveReadStatus] = useState<TeachingReadStatus>(initialRoute?.readStatus ?? 'all')
  const [archivePage, setArchivePage] = useState(initialRoute?.page ?? 1)
  const [archiveRefreshKey, setArchiveRefreshKey] = useState(0)
  const [isArchiveLoading, setIsArchiveLoading] = useState(true)
  const [archiveError, setArchiveError] = useState<string | null>(null)
  const [archivedArticle, setArchivedArticle] = useState<WeeklyDvarTorahArticle | null>(null)
  const [archivedArticleLoadingKey, setArchivedArticleLoadingKey] = useState<string | null>(initialRoute?.weekKey ?? null)
  const [archivedArticleError, setArchivedArticleError] = useState<string | null>(null)
  const [selectedSourceNumber, setSelectedSourceNumber] = useState<number | null>(null)
  const sourceReaderTriggerRef = useRef<HTMLButtonElement | null>(null)
  const scrollAreaRef = useRef<HTMLElement | null>(null)
  const [audioDock, setAudioDock] = useState<HTMLDivElement | null>(null)
  const archivedArticleRequestIdRef = useRef(0)
  const progress = useTeachingReadState(client, Boolean(offlineSavedAt), () => {
    setIsArchiveLoading(true)
    setArchiveError(null)
    setArchiveRefreshKey(value => value + 1)
  })
  const syncArchivePage = useEffectEvent((page: number) => {
    if (view === 'archive') { onNavigate?.({ archive: true, page, search: archiveSearch, readStatus: archiveReadStatus }) }
  })

  const restoreArticle = useEffectEvent((weekKey: string) => { void openArchivedArticle(weekKey, false) })
  useEffect(() => {
    if (initialRoute?.weekKey && !offlineSavedAt) { restoreArticle(initialRoute.weekKey) }
  }, [initialRoute?.weekKey, offlineSavedAt])

  useEffect(() => {
    let isCurrent = true

    void client.getCurrent(refreshKey > 0)
      .then((value) => {
        if (isCurrent) {
          setPublication(value)
        }
      })
      .catch((error: unknown) => {
        if (isCurrent) {
          setLoadError(error instanceof Error && error.message.trim().length > 0 ? error.message : 'This week’s Dvar Torah could not be loaded.')
        }
      })

    return () => {
      isCurrent = false
    }
  }, [client, refreshKey])

  useEffect(() => {
    if (offlineSavedAt) { return }
    let isCurrent = true

    void client.getArchive({ page: archivePage, pageSize: ArchivePageSize, search: archiveSearch || undefined, ...(archiveReadStatus === 'all' ? {} : { readStatus: archiveReadStatus }) })
      .then((value) => {
        if (isCurrent) {
          const lastPage = Math.max(1, value.totalPages)
          if (archivePage > lastPage) { setArchivePage(lastPage); syncArchivePage(lastPage); return }
          setArchive(value)
        }
      })
      .catch((error: unknown) => {
        if (isCurrent) {
          setArchiveError(error instanceof Error && error.message.trim().length > 0 ? error.message : 'Past Dvar Torahs could not be loaded.')
        }
      })
      .finally(() => {
        if (isCurrent) {
          setIsArchiveLoading(false)
        }
      })

    return () => {
      isCurrent = false
    }
  }, [archivePage, archiveRefreshKey, archiveSearch, archiveReadStatus, client, offlineSavedAt])

  useEffect(() => () => {
    archivedArticleRequestIdRef.current += 1
  }, [])

  const article = view === 'archivedArticle' ? archivedArticle : view === 'current' ? publication?.dvarTorah ?? null : null
  const sources = useMemo(() => article === null ? [] : toConversationSources(article.sources), [article])
  const selectedSourceIndex = selectedSourceNumber === null ? -1 : sources.findIndex((source) => source.number === selectedSourceNumber)

  const openSourceReader = useCallback((sourceNumber: number, trigger: HTMLButtonElement) => {
    sourceReaderTriggerRef.current = trigger
    setSelectedSourceNumber(sourceNumber)
  }, [])

  const closeSourceReader = useCallback(() => {
    const trigger = sourceReaderTriggerRef.current
    setSelectedSourceNumber(null)
    if (trigger?.isConnected === true) {
      trigger.focus()
    }
  }, [])

  function retry() {
    setPublication(null)
    setLoadError(null)
    setSelectedSourceNumber(null)
    setRefreshKey((current) => current + 1)
  }

  function showCurrentTeaching() {
    onNavigate?.({})
    archivedArticleRequestIdRef.current += 1
    setView('current')
    setArchivedArticle(null)
    setArchivedArticleLoadingKey(null)
    setArchivedArticleError(null)
    setSelectedSourceNumber(null)
    scrollToTop()
  }

  function showArchive() {
    onNavigate?.({ archive: true, page: archivePage, search: archiveSearch, readStatus: archiveReadStatus })
    archivedArticleRequestIdRef.current += 1
    setView('archive')
    setArchivedArticle(null)
    setArchivedArticleLoadingKey(null)
    setArchivedArticleError(null)
    setSelectedSourceNumber(null)
    scrollToTop()
  }

  function searchArchive(search: string) {
    const normalizedSearch = search.trim()
    onNavigate?.({ archive: true, page: 1, search: normalizedSearch, readStatus: archiveReadStatus })
    setIsArchiveLoading(true)
    setArchiveError(null)
    setArchiveSearch(normalizedSearch)
    setArchivePage(1)
    if (archivePage === 1 && normalizedSearch === archiveSearch) {
      setArchiveRefreshKey((current) => current + 1)
    }
  }

  function changeArchivePage(page: number) {
    onNavigate?.({ archive: true, page, search: archiveSearch, readStatus: archiveReadStatus })
    setIsArchiveLoading(true)
    setArchiveError(null)
    setArchivePage(page)
  }

  function retryArchive() {
    setIsArchiveLoading(true)
    setArchiveError(null)
    setArchiveRefreshKey((current) => current + 1)
  }

  function changeReadStatus(readStatus: TeachingReadStatus) {
    setArchiveReadStatus(readStatus)
    setArchive(null)
    setArchivePage(1)
    setIsArchiveLoading(true)
    setArchiveError(null)
    onNavigate?.({ archive: true, page: 1, search: archiveSearch, readStatus })
  }

  async function openArchivedArticle(weekKey: string, updateUrl = true) {
    const requestId = archivedArticleRequestIdRef.current + 1
    archivedArticleRequestIdRef.current = requestId
    setArchivedArticleLoadingKey(weekKey)
    setArchivedArticleError(null)

    try {
      const value = await client.getArchived(weekKey)
      if (requestId !== archivedArticleRequestIdRef.current) {
        return
      }

      setArchivedArticle(value)
      if (updateUrl) { onNavigate?.({ weekKey, page: archivePage, search: archiveSearch, readStatus: archiveReadStatus }) }
      setSelectedSourceNumber(null)
      setView('archivedArticle')
      scrollToTop()
    } catch (error: unknown) {
      if (requestId === archivedArticleRequestIdRef.current) {
        setArchivedArticleError(error instanceof Error && error.message.trim().length > 0 ? error.message : 'That Dvar Torah could not be loaded.')
      }
    } finally {
      if (requestId === archivedArticleRequestIdRef.current) {
        setArchivedArticleLoadingKey(null)
      }
    }
  }

  function scrollToTop() {
    scrollAreaRef.current?.scrollTo?.({ top: 0, behavior: 'smooth' })
  }

  return (
    <div className="flex min-h-0 min-w-0 flex-1 overflow-hidden">
      <div className="flex min-h-0 min-w-0 flex-1 flex-col">
        <section ref={scrollAreaRef} data-reading-scroll className="min-h-0 min-w-0 flex-1 overflow-y-auto px-4 sm:px-8" aria-label="Weekly Dvar Torah">
          <div className="reading-column enter-softly mx-auto w-full max-w-[80rem] pb-16 pt-7 sm:pt-9">
            <header className="reading-nonessential flex flex-wrap items-center justify-between gap-6">
              <p className="text-[length:max(0.875rem,14px)] font-semibold uppercase tracking-[0.12em] text-pomegranate">Weekly Dvar Torah</p>
              {offlineSavedAt ? null : <nav className="readable-menu flex w-full max-w-full flex-wrap gap-2 rounded-xl bg-stone/65 p-2 sm:w-[32rem]" aria-label="Weekly learning">
              <button type="button" aria-pressed={view === 'current'} onClick={showCurrentTeaching} className={`inline-flex min-h-[44px] min-w-0 flex-[1_1_12rem] items-center justify-center gap-3 rounded-lg px-5 py-3 text-[length:max(0.875rem,14px)] font-semibold transition ${view === 'current' ? 'bg-paper text-ink shadow-sm' : 'text-ink-soft hover:text-pomegranate'}`}>
                <BookOpenText aria-hidden="true" className="size-4 shrink-0" strokeWidth={1.7} />
                This week
              </button>
              <button type="button" aria-pressed={view !== 'current'} onClick={showArchive} className={`inline-flex min-h-[44px] min-w-0 flex-[1_1_12rem] items-center justify-center gap-3 rounded-lg px-5 py-3 text-[length:max(0.875rem,14px)] font-semibold transition ${view !== 'current' ? 'bg-paper text-ink shadow-sm' : 'text-ink-soft hover:text-pomegranate'}`}>
                <BookMarked aria-hidden="true" className="size-4 shrink-0" strokeWidth={1.7} />
                Past teachings
              </button>
              </nav>}
            </header>
            {offlineSavedAt ? <p className="reading-nonessential mt-4 text-sm leading-6 text-muted">Offline copy saved {formatSourceDate(offlineSavedAt)}. Source excerpts are saved; original websites need a connection.</p> : null}

            {progress.error && (progress.keys === null || view === 'archive') ? <div role="alert" className="reading-nonessential mt-5 rounded-lg border border-pomegranate/25 px-4 py-3 text-sm text-pomegranate">{progress.error}{progress.keys === null ? <button type="button" onClick={progress.retry} className="ml-2 min-h-11 font-semibold underline">Retry reading progress</button> : null}</div> : null}
            {progress.offline ? <p className="reading-nonessential mt-4 text-sm text-muted">Connect to update your reading progress.</p> : null}

            {view === 'archive' ? (
              <DvarTorahArchive archive={archive} progress={progress} readStatus={archiveReadStatus} onReadStatusChange={changeReadStatus} searchDraft={archiveSearchDraft} activeSearch={archiveSearch} isLoading={isArchiveLoading} loadError={archiveError} articleError={archivedArticleError} loadingArticleKey={archivedArticleLoadingKey} onSearchDraftChange={setArchiveSearchDraft} onSearch={searchArchive} onPageChange={changeArchivePage} onRetry={retryArchive} onOpenArticle={(weekKey) => void openArchivedArticle(weekKey)} />
            ) : view === 'archivedArticle' && archivedArticle === null ? (
              archivedArticleError ? <LoadError message={archivedArticleError} onRetry={() => { if (initialRoute?.weekKey) { void openArchivedArticle(initialRoute.weekKey, false) } }} /> : <p role="status" className="py-10 text-muted">Loading the selected teaching…</p>
            ) : view === 'archivedArticle' && archivedArticle !== null ? (
              <div>
                <button type="button" onClick={showArchive} className="reading-nonessential mt-5 inline-flex min-h-[44px] items-center gap-2 rounded-lg pr-3 text-[length:max(0.875rem,14px)] font-semibold text-ink-soft transition hover:text-pomegranate">
                  <ArrowLeft aria-hidden="true" className="size-4" strokeWidth={1.8} />
                  Back to past teachings
                </button>
                <PublishedArticle key={`${archivedArticle.week.weekKey}:${archivedArticle.audio?.version ?? ''}`} article={archivedArticle} progress={progress} client={client} sources={sources} selectedSourceNumber={selectedSourceNumber} onSelectSource={openSourceReader} audioDock={audioDock} scrollAreaRef={scrollAreaRef} onAskTeaching={onAskTeaching} isAskDisabled={isAskDisabled} />
              </div>
            ) : loadError !== null ? (
              <LoadError message={loadError} onRetry={retry} />
            ) : publication === null ? (
              <div className="mt-12 flex min-h-48 items-center justify-center border-y border-line" aria-busy="true">
                <p className="text-sm text-muted" role="status">Loading this week’s Dvar Torah…</p>
              </div>
            ) : publication.dvarTorah === null ? (
              <PendingPublication week={publication.currentWeek} onRetry={retry} />
            ) : (
              <PublishedArticle key={`${publication.dvarTorah.week.weekKey}:${publication.dvarTorah.audio?.version ?? ''}`} article={publication.dvarTorah} progress={progress} client={client} showFallbackNotice={!offlineSavedAt && !publication.isCurrentWeek} sources={sources} selectedSourceNumber={selectedSourceNumber} onSelectSource={openSourceReader} audioDock={audioDock} scrollAreaRef={scrollAreaRef} onAskTeaching={onAskTeaching} isAskDisabled={isAskDisabled} />
            )}
          </div>
        </section>
        <div ref={setAudioDock} className="z-10 shrink-0 border-t border-line bg-parchment/95 px-3 pt-2 pb-[max(0.5rem,env(safe-area-inset-bottom))] empty:hidden sm:px-8 sm:pt-3" />
      </div>
      {article === null || selectedSourceIndex < 0 ? null : <SourceReader messageId={`weekly-dvar-torah-${article.week.weekKey}`} sources={sources} selectedIndex={selectedSourceIndex} showSourceContextByDefault={false} onSelectSourceNumber={setSelectedSourceNumber} onClose={closeSourceReader} onAsk={onAsk} isAskDisabled={isAskDisabled} />}
    </div>
  )
}

interface PublishedArticleProps {
  onAskTeaching?(context: ConversationTeachingContext): void
  isAskDisabled?: boolean
  progress: TeachingReadProgress
  audioDock: HTMLDivElement | null
  scrollAreaRef: RefObject<HTMLElement | null>
  article: WeeklyDvarTorahArticle
  client: DvarTorahClient
  showFallbackNotice?: boolean
  sources: readonly ConversationSource[]
  selectedSourceNumber: number | null
  onSelectSource(sourceNumber: number, trigger: HTMLButtonElement): void
}

function PublishedArticle({ article, progress, client, showFallbackNotice = false, sources, selectedSourceNumber, onSelectSource, audioDock, scrollAreaRef, onAskTeaching, isAskDisabled }: PublishedArticleProps) {
  const readingId = `teaching:${article.week.weekKey}`
  const reading = useReadingTarget(readingId, article.body)
  const [activeWord, setActiveWord] = useState<DvarTorahAudioWord | null>(null)
  const [isPlaying, setIsPlaying] = useState(false)
  const [timings, setTimings] = useState<DvarTorahAudioTimings | null>(null)
  const playerRef = useRef<DvarTorahPlaybackHandle | null>(null)
  const titleWords = useMemo(() => timings?.words.filter((word) => word.section === 'title'), [timings])
  const bodyWords = useMemo(() => timings?.words.filter((word) => word.section === 'body'), [timings])
  const selectWord = useCallback((word: DvarTorahAudioWord) => playerRef.current?.seekToWord(word), [])
  const articleRef = useRef<HTMLElement | null>(null)
  useNarrationFollow(activeWord, articleRef, scrollAreaRef, isPlaying, selectedSourceNumber !== null)
  const title = useMemo(() => normalizeDvarTorahText(article.title), [article.title])
  const body = useMemo(() => normalizeDvarTorahText(article.body), [article.body])
  const paragraphs = useMemo(() => createNarratedParagraphs(body), [body])
  const sourceNumbersById = useMemo(() => new Map(article.sources.map((source, index) => [source.sourceId, index + 1])), [article.sources])
  const audioReadingMinutes = estimateReadingMinutes(article.audio?.durationMs)
  const readingMinutes = useMemo(() => estimateReadingMinutes(article.audio?.durationMs, body), [article.audio?.durationMs, body])
  const cancelAutomaticRead = useAutomaticTeachingRead(article.week.weekKey, readingMinutes, progress)
  const isRead = progress.keys?.has(article.week.weekKey) ?? false
  return (
    <article ref={articleRef} data-reading-target={readingId} data-reading-focused={reading.isFocused} className="teaching-article mt-5 sm:mt-7" aria-label={normalizeDvarTorahText(article.title)}>
      {!showFallbackNotice ? null : (
        <p className="mb-6 rounded-lg border border-brass/40 bg-brass/5 px-4 py-3 text-sm leading-6 text-ink-soft">
          This week’s teaching is still being prepared. Here is the latest available Dvar Torah.
        </p>
      )}

      <header>
        <h1 id="weekly-dvar-torah-title" className="max-w-[60rem] text-balance font-display text-[clamp(2rem,4.5vw,3.35rem)] leading-[1.08] tracking-[-0.035em] text-ink"><NarratedText text={title} activeWord={activeWord?.section === 'title' ? activeWord : null} words={titleWords} onSelectWord={selectWord} /></h1>
        <div className="mt-5"><WeekDetails week={article.week} /></div>
        <div className="mt-6 flex flex-col gap-4">
          {readingMinutes === null ? null : (
            <p aria-label="Estimated reading time" title={audioReadingMinutes === null ? 'Estimated from the teaching text at 200 words per minute.' : `Based on ${formatAudioTime((article.audio?.durationMs ?? 0) / 1000)} of audio at 1× speed, rounded up to the next minute.`} className="inline-flex items-center gap-2 text-[length:max(0.875rem,14px)] text-muted">
              <Clock aria-hidden="true" className="size-4 text-brass" strokeWidth={1.7} />About {readingMinutes} min read<span className="sr-only"> · {audioReadingMinutes === null ? 'Based on text length' : 'Based on audio at 1×'}</span>
            </p>
          )}
          <div role="group" aria-label="Teaching actions" className="teaching-actions readable-menu reading-nonessential">
            {onAskTeaching ? <TeachingAskActions context={{ weekKey: article.week.weekKey, title, selectedText: null }} onAsk={onAskTeaching} disabled={isAskDisabled} /> : null}
            <PrintAction label="Print teaching" getRequest={() => ({ kind: 'teaching', article })} />
            {reading.isLong ? <FocusReadingButton id={readingId} label="Focus teaching" /> : null}
          </div>
        </div>
      </header>
      {article.audio == null ? <p className="mt-3 text-sm text-muted">Audio is not available for this teaching yet.</p> : null}
      {audioDock === null || article.audio == null ? null : createPortal(<DvarTorahReadAloud ref={playerRef} audio={article.audio} weekKey={article.week.weekKey} title={title} body={body} client={client} onWordChange={setActiveWord} onTimingsChange={setTimings} onPlayingChange={setIsPlaying} />, audioDock)}
      {timings === null ? null : <p className="mt-3 text-sm text-muted sm:mt-4">Select a word to listen from that point.<span className="sr-only"> Use the left and right arrow keys to move between words, then Enter to play.</span></p>}
      <div className="reading-content teaching-body mt-5 max-w-[60rem] space-y-6 border-l-2 border-brass/55 pl-3 sm:mt-8 sm:pl-7">
        {paragraphs.map((paragraph) => <p key={paragraph.textOffset} className="whitespace-pre-line text-base leading-8 text-ink-soft sm:text-[1.08rem]"><DvarTorahNarratedText text={paragraph.text} textOffset={paragraph.textOffset} activeWord={activeWord?.section === 'body' && activeWord.textOffset >= paragraph.textOffset && activeWord.textOffset < paragraph.textOffset + paragraph.text.length ? activeWord : null} words={bodyWords} onSelectWord={selectWord} sourceNumbersById={sourceNumbersById} selectedSourceNumber={selectedSourceNumber} onSelectSource={onSelectSource} /></p>)}
      </div>
      {sources.length === 0 ? null : <p className="mt-8 inline-flex max-w-[60rem] items-center gap-2 text-sm leading-6 text-muted"><BookOpenText aria-hidden="true" className="size-4 shrink-0 text-pomegranate" strokeWidth={1.7} />Select a numbered reference to read the supporting excerpt and source details.</p>}
      <footer className="mt-8 max-w-[60rem] border-t border-line pt-5">
        <div role="group" aria-label="Teaching reading progress" className="readable-menu flex flex-wrap items-center justify-between gap-3">
          <p role="status" className="text-sm font-medium text-ink-soft">{progress.keys === null ? 'Reading progress' : isRead ? 'Marked as read' : 'Finished reading?'}</p>
          <TeachingReadButton progress={progress} weekKey={article.week.weekKey} title={title} onToggle={cancelAutomaticRead} />
        </div>
        {progress.error && progress.keys !== null ? <p role="alert" className="mt-3 text-sm text-pomegranate">{progress.error}</p> : null}
        <p className="mt-5 text-xs leading-5 text-muted">
          This is an educational reflection, not binding <i>psak</i>. Read it as an invitation to study, question, and continue the conversation. See our <LegalLink document="terms-of-service" section="educational-use" />.
        </p>
      </footer>
    </article>
  )
}

interface DvarTorahArchiveProps {
  progress: TeachingReadProgress
  readStatus: TeachingReadStatus
  onReadStatusChange(value: TeachingReadStatus): void
  archive: WeeklyDvarTorahArchiveResponse | null
  searchDraft: string
  activeSearch: string
  isLoading: boolean
  loadError: string | null
  articleError: string | null
  loadingArticleKey: string | null
  onSearchDraftChange(value: string): void
  onSearch(value: string): void
  onPageChange(page: number): void
  onRetry(): void
  onOpenArticle(weekKey: string): void
}

function DvarTorahArchive({ archive, progress, readStatus, onReadStatusChange, searchDraft, activeSearch, isLoading, loadError, articleError, loadingArticleKey, onSearchDraftChange, onSearch, onPageChange, onRetry, onOpenArticle }: DvarTorahArchiveProps) {
  const items = archive?.items ?? []

  return (
    <section className="mt-7" aria-labelledby="dvar-torah-archive-title" aria-busy={isLoading}>
      <div className="max-w-[60rem]">
        <h1 id="dvar-torah-archive-title" className="font-display text-[clamp(1.85rem,4vw,2.65rem)] leading-tight tracking-[-0.03em] text-ink">Explore past Dvar Torahs.</h1>
        <p className="mt-2 text-sm leading-6 text-ink-soft">Browse earlier weekly reflections by title, parashah, holiday, date, or topic.</p>
      </div>

      <form className="mt-6 max-w-[60rem]" role="search" onSubmit={(event) => {
        event.preventDefault()
        onSearch(searchDraft)
      }}>
        <label htmlFor="dvar-torah-archive-search" className="text-sm font-semibold text-ink">Search past teachings</label>
        <div className="mt-2 flex gap-2">
          <div className="relative min-w-0 flex-1">
            <Search aria-hidden="true" className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" strokeWidth={1.8} />
            <input id="dvar-torah-archive-search" type="search" maxLength={120} value={searchDraft} onChange={(event) => onSearchDraftChange(event.target.value)} placeholder="Try ‘Nitzavim’ or ‘community’" className="h-12 w-full rounded-xl border border-line-strong bg-paper pl-10 pr-3 text-base text-ink shadow-sm transition placeholder:text-muted/70 hover:border-ink/35 focus:border-pomegranate focus:outline-none focus:ring-2 focus:ring-pomegranate/15 sm:text-sm" />
          </div>
          <button type="submit" disabled={isLoading} className="h-12 shrink-0 rounded-xl bg-pomegranate px-5 text-sm font-semibold text-white transition hover:bg-pomegranate-dark disabled:cursor-wait disabled:opacity-60">Search</button>
        </div>
        {activeSearch.length === 0 ? null : (
          <div className="mt-3 flex flex-wrap items-center gap-2 text-sm text-muted">
            <span>Showing results for “{normalizeDvarTorahText(activeSearch)}”</span>
            <button type="button" onClick={() => {
              onSearchDraftChange('')
              onSearch('')
            }} className="font-semibold text-pomegranate hover:text-pomegranate-dark">Clear search</button>
          </div>
        )}
      </form>

      <div className="mt-4 grid w-full max-w-[46rem] grid-cols-[1.4fr_1fr_1fr] gap-1 rounded-xl border border-line bg-stone/50 p-1 sm:flex sm:w-fit" role="group" aria-label="Filter teachings by reading status">
        {(['all', 'unread', 'read'] as const).map(status => <button key={status} type="button" aria-pressed={readStatus === status} onClick={() => { if (status !== readStatus) { onReadStatusChange(status) } }} className={`min-h-11 rounded-lg px-2 text-sm font-semibold transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate sm:px-4 ${readStatus === status ? 'bg-paper text-pomegranate shadow-sm ring-1 ring-line' : 'text-ink-soft hover:text-pomegranate'}`}>{status === 'all' ? 'All teachings' : status === 'unread' ? 'Unread' : 'Read'}</button>)}
      </div>

      {articleError === null ? null : <p className="mt-5 max-w-[60rem] rounded-lg border border-pomegranate/25 bg-pomegranate/5 px-4 py-3 text-sm text-pomegranate" role="alert">{articleError}</p>}
      {loadError === null ? null : (
        <div className="mt-6 max-w-[60rem] rounded-xl border border-pomegranate/25 bg-pomegranate/5 px-4 py-4" role="alert">
          <p className="text-sm text-pomegranate">{loadError}</p>
          <button type="button" onClick={onRetry} className="mt-3 inline-flex min-h-10 items-center gap-2 text-sm font-semibold text-ink hover:text-pomegranate">
            <RefreshCw aria-hidden="true" className="size-4" strokeWidth={1.8} />
            Try again
          </button>
        </div>
      )}

      {archive === null && isLoading ? (
        <div className="mt-8 flex min-h-40 max-w-[60rem] items-center justify-center rounded-xl border border-line bg-stone/35" role="status">
          <LoaderCircle aria-hidden="true" className="mr-2 size-4 animate-spin text-pomegranate" />
          <span className="text-sm text-muted">Loading the latest 10 weeks…</span>
        </div>
      ) : loadError !== null && archive === null ? null : items.length === 0 ? (
        <div className="mt-8 max-w-[60rem] rounded-xl border border-line bg-stone/35 px-5 py-8 text-center">
          <BookMarked aria-hidden="true" className="mx-auto size-6 text-brass" strokeWidth={1.6} />
          <p className="mt-3 font-display text-xl text-ink">{readStatus === 'all' ? 'No past teachings found.' : `No ${readStatus} teachings found.`}</p>
          <p className="mt-1 text-sm text-muted">{readStatus === 'all' ? 'Try a different title, parashah, date, or topic.' : 'Try a different search or choose All teachings.'}</p>
        </div>
      ) : (
        <div className={`mt-7 max-w-[60rem] transition-opacity ${isLoading ? 'opacity-55' : 'opacity-100'}`}>
          <div className="mb-3 flex items-center justify-between gap-4 text-xs font-semibold uppercase tracking-[0.12em] text-muted">
            <span>{archive?.totalCount ?? 0} {(archive?.totalCount ?? 0) === 1 ? 'teaching' : 'teachings'}</span>
            {isLoading ? <span className="inline-flex items-center gap-1.5 normal-case tracking-normal" role="status"><LoaderCircle aria-hidden="true" className="size-3.5 animate-spin" />Updating…</span> : null}
          </div>
          <ul className="divide-y divide-line border-y border-line">
            {items.map((item) => {
              const title = normalizeDvarTorahText(item.title)
              const parashah = item.week.parashah === null ? null : normalizeDvarTorahText(item.week.parashah)
              const holiday = item.week.holiday === null ? null : normalizeDvarTorahText(item.week.holiday)
              const isOpening = loadingArticleKey === item.week.weekKey
              return (
                <li key={item.week.weekKey}>
                  <button type="button" disabled={loadingArticleKey !== null} onClick={() => onOpenArticle(item.week.weekKey)} aria-label={`Open ${title}`} aria-describedby={progress.keys === null ? undefined : `teaching-progress-${item.week.weekKey}`} className="group grid min-h-32 w-full grid-cols-[1fr_auto] gap-4 px-1 py-5 text-left transition hover:bg-stone/45 disabled:cursor-wait disabled:opacity-65 sm:px-3">
                    <span className="min-w-0">
                      <span className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted">
                        <span className="font-semibold text-ink-soft">{formatShabbatDate(item.week.shabbatDate)}</span>
                        <span>{normalizeDvarTorahText(item.week.hebrewDate)}</span>
                        {progress.keys === null ? null : <span id={`teaching-progress-${item.week.weekKey}`} className={`rounded-full px-2 py-0.5 font-semibold ${progress.keys.has(item.week.weekKey) ? 'bg-pomegranate/10 text-pomegranate' : 'bg-stone-deep text-ink-soft'}`}>{progress.keys.has(item.week.weekKey) ? 'Read' : 'Unread'}</span>}
                      </span>
                      <span className="mt-2 block font-display text-[1.35rem] leading-7 text-ink transition group-hover:text-pomegranate">{title}</span>
                      <span className="mt-2 flex flex-wrap items-center gap-2 text-sm text-ink-soft">
                        <span>{parashah === null || parashah.trim().length === 0 ? 'Shabbat reading' : `Parashat ${parashah}`}</span>
                        {holiday === null || holiday.trim().length === 0 ? null : <span className="inline-flex items-center gap-1 rounded-full border border-brass/40 bg-brass/8 px-2 py-0.5 text-xs font-semibold"><Sparkles aria-hidden="true" className="size-3 text-brass" />{holiday}</span>}
                      </span>
                      {item.tags.length === 0 ? null : (
                        <span className="mt-3 flex flex-wrap gap-1.5">
                          {item.tags.slice(0, 3).map((tag) => <span key={tag} className="rounded-full bg-stone-deep/75 px-2.5 py-1 text-xs text-ink-soft">{normalizeDvarTorahText(tag)}</span>)}
                        </span>
                      )}
                    </span>
                    <span className="mt-9 flex size-9 items-center justify-center rounded-full border border-line text-muted transition group-hover:border-pomegranate/45 group-hover:text-pomegranate">
                      {isOpening ? <LoaderCircle aria-hidden="true" className="size-4 animate-spin" /> : <ChevronRight aria-hidden="true" className="size-4" strokeWidth={1.8} />}
                    </span>
                  </button>
                </li>
              )
            })}
          </ul>

          {(archive?.totalPages ?? 0) <= 1 ? null : (
            <nav className="mt-5 flex items-center justify-between gap-3" aria-label="Past Dvar Torah pages">
              <button type="button" disabled={isLoading || (archive?.page ?? 1) <= 1} onClick={() => onPageChange((archive?.page ?? 1) - 1)} className="inline-flex min-h-11 items-center gap-2 rounded-lg border border-line bg-paper px-3.5 text-sm font-semibold text-ink transition hover:border-pomegranate/45 hover:text-pomegranate disabled:cursor-not-allowed disabled:opacity-45">
                <ChevronLeft aria-hidden="true" className="size-4" />
                Previous
              </button>
              <span className="text-sm text-muted">Page <strong className="text-ink">{archive?.page}</strong> of {archive?.totalPages}</span>
              <button type="button" disabled={isLoading || (archive?.page ?? 1) >= (archive?.totalPages ?? 0)} onClick={() => onPageChange((archive?.page ?? 1) + 1)} className="inline-flex min-h-11 items-center gap-2 rounded-lg border border-line bg-paper px-3.5 text-sm font-semibold text-ink transition hover:border-pomegranate/45 hover:text-pomegranate disabled:cursor-not-allowed disabled:opacity-45">
                Next
                <ChevronRight aria-hidden="true" className="size-4" />
              </button>
            </nav>
          )}
        </div>
      )}
    </section>
  )
}

function PendingPublication({ week, onRetry }: { week: DvarTorahWeek; onRetry(): void }) {
  return (
    <div className="mt-9">
      <div className="flex max-w-[60rem] gap-4">
        <div className="flex size-11 shrink-0 items-center justify-center rounded-full bg-stone-deep text-pomegranate">
          <BookOpenText aria-hidden="true" className="size-5" strokeWidth={1.65} />
        </div>
        <div>
          <h1 className="font-display text-2xl text-ink">This week’s teaching is being prepared.</h1>
          <div className="mt-4"><WeekDetails week={week} /></div>
          <p className="mt-4 text-base leading-6 text-ink-soft">A new reflection on this week’s reading will appear here when it is ready.</p>
          <button type="button" onClick={onRetry} className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg border border-line-strong bg-paper px-4 text-sm font-semibold text-ink transition hover:border-pomegranate/45 hover:text-pomegranate">
            <RefreshCw aria-hidden="true" className="size-4" strokeWidth={1.8} />
            Check again
          </button>
        </div>
      </div>
    </div>
  )
}

function WeekDetails({ week }: { week: DvarTorahWeek }) {
  const parashah = week.parashah === null ? null : normalizeDvarTorahText(week.parashah).trim()
  const holiday = week.holiday === null ? null : normalizeDvarTorahText(week.holiday).trim()
  const readingName = getReadingName(parashah, holiday)

  return (
    <div role="group" aria-label="Teaching details" className="space-y-1 text-[length:max(0.875rem,14px)] text-muted sm:space-y-2 sm:text-[length:max(0.9375rem,14px)]">
      <p className="flex flex-wrap items-center gap-x-3 gap-y-1 font-semibold text-ink-soft">
        <span className="inline-flex items-center gap-2"><BookOpenText aria-hidden="true" className="size-4 text-brass" strokeWidth={1.7} />{readingName}</span>
        {!parashah || !holiday ? null : <span>{holiday}</span>}
        <span className="font-normal text-muted">{week.inIsrael ? 'Israel' : 'Diaspora'}</span>
      </p>
      <p className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <span className="inline-flex items-center gap-2"><CalendarDays aria-hidden="true" className="size-4 shrink-0" strokeWidth={1.7} /><time dateTime={week.shabbatDate}>{formatShabbatDate(week.shabbatDate)}</time></span>
        <span>{normalizeDvarTorahText(week.hebrewDate)}</span>
      </p>
    </div>
  )
}

function LoadError({ message, onRetry }: { message: string; onRetry(): void }) {
  return (
    <div className="mt-9 max-w-[60rem] border-y border-pomegranate/25 bg-pomegranate/5 px-5 py-6" role="alert">
      <p className="text-sm leading-6 text-pomegranate">{message}</p>
      <button type="button" onClick={onRetry} className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg bg-pomegranate px-4 text-sm font-semibold text-white transition hover:bg-pomegranate-dark">
        <RefreshCw aria-hidden="true" className="size-4" strokeWidth={1.8} />
        Try again
      </button>
    </div>
  )
}

function formatShabbatDate(value: string) {
  const date = new Date(`${value}T12:00:00Z`)
  return Number.isNaN(date.getTime()) ? value : ShabbatDateFormatter.format(date)
}

function toConversationSources(sources: readonly WeeklyDvarTorahSource[]): ConversationSource[] {
  return sources.map((source, index) => ({
    number: index + 1,
    title: normalizeDvarTorahText(source.kind === 'News' ? source.publisher : source.title),
    hebrewTitle: '',
    canonicalReference: normalizeDvarTorahText(source.canonicalReference ?? source.title),
    edition: source.kind === 'News' ? formatPublishedDate(source.publishedAtUtc) : normalizeDvarTorahText(source.publisher),
    language: 'English',
    collection: source.kind,
    license: normalizeDvarTorahText(source.license ?? 'Source terms apply'),
    sourceUrl: source.sourceUrl,
    attributionUrl: source.sourceUrl,
    quotations: [normalizeDvarTorahText(source.excerpt)],
    context: createSourceContext(source),
    isExcerpt: true,
    externalLinkLabel: 'Open original source',
  }))
}

function createSourceContext(source: WeeklyDvarTorahSource) {
  const details = [
    `Publisher: ${normalizeDvarTorahText(source.publisher)}`,
    source.publishedAtUtc === null ? null : `Published: ${formatSourceDate(source.publishedAtUtc)}`,
    `Retrieved: ${formatSourceDate(source.retrievedAtUtc)}`,
  ]
  return details.filter((detail) => detail !== null).join('\n')
}

function formatPublishedDate(value: string | null) {
  return value === null ? 'News report' : `Published ${formatSourceDate(value)}`
}

function formatSourceDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return normalizeDvarTorahText(value)
  }

  return SourceDateFormatter.format(date)
}

function getReadingName(parashah: string | null, holiday: string | null) {
  if (parashah !== null && parashah.length > 0) {
    return `Parashat ${parashah}`
  }

  return holiday !== null && holiday.length > 0 ? holiday : 'Shabbat reading'
}
