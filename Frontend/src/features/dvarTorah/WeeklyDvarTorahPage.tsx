import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ArrowLeft, BookMarked, BookOpenText, CalendarDays, ChevronLeft, ChevronRight, LoaderCircle, RefreshCw, Search, Sparkles } from 'lucide-react'
import { SourceReader } from '../conversations/SourceReader.tsx'
import type { ConversationSource } from '../conversations/conversationData.ts'
import type { DvarTorahClient } from './dvarTorahClient.ts'
import { DvarTorahReadAloud } from './DvarTorahReadAloud.tsx'
import { normalizeDvarTorahText } from './dvarTorahText.ts'
import type { DvarTorahWeek, WeeklyDvarTorahArchiveResponse, WeeklyDvarTorahArticle, WeeklyDvarTorahResponse, WeeklyDvarTorahSource } from './dvarTorahTypes.ts'

interface WeeklyDvarTorahPageProps {
  client: DvarTorahClient
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

export function WeeklyDvarTorahPage({ client }: WeeklyDvarTorahPageProps) {
  const [publication, setPublication] = useState<WeeklyDvarTorahResponse | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)
  const [view, setView] = useState<WeeklyLearningView>('current')
  const [archive, setArchive] = useState<WeeklyDvarTorahArchiveResponse | null>(null)
  const [archiveSearchDraft, setArchiveSearchDraft] = useState('')
  const [archiveSearch, setArchiveSearch] = useState('')
  const [archivePage, setArchivePage] = useState(1)
  const [archiveRefreshKey, setArchiveRefreshKey] = useState(0)
  const [isArchiveLoading, setIsArchiveLoading] = useState(true)
  const [archiveError, setArchiveError] = useState<string | null>(null)
  const [archivedArticle, setArchivedArticle] = useState<WeeklyDvarTorahArticle | null>(null)
  const [archivedArticleLoadingKey, setArchivedArticleLoadingKey] = useState<string | null>(null)
  const [archivedArticleError, setArchivedArticleError] = useState<string | null>(null)
  const [selectedSourceNumber, setSelectedSourceNumber] = useState<number | null>(null)
  const sourceReaderTriggerRef = useRef<HTMLButtonElement | null>(null)
  const scrollAreaRef = useRef<HTMLElement | null>(null)
  const archivedArticleRequestIdRef = useRef(0)

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
    let isCurrent = true

    void client.getArchive({ page: archivePage, pageSize: ArchivePageSize, search: archiveSearch || undefined })
      .then((value) => {
        if (isCurrent) {
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
  }, [archivePage, archiveRefreshKey, archiveSearch, client])

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
    archivedArticleRequestIdRef.current += 1
    setView('current')
    setArchivedArticle(null)
    setArchivedArticleLoadingKey(null)
    setArchivedArticleError(null)
    setSelectedSourceNumber(null)
    scrollToTop()
  }

  function showArchive() {
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
    setIsArchiveLoading(true)
    setArchiveError(null)
    setArchiveSearch(normalizedSearch)
    setArchivePage(1)
    if (archivePage === 1 && normalizedSearch === archiveSearch) {
      setArchiveRefreshKey((current) => current + 1)
    }
  }

  function changeArchivePage(page: number) {
    setIsArchiveLoading(true)
    setArchiveError(null)
    setArchivePage(page)
  }

  function retryArchive() {
    setIsArchiveLoading(true)
    setArchiveError(null)
    setArchiveRefreshKey((current) => current + 1)
  }

  async function openArchivedArticle(weekKey: string) {
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
      <section ref={scrollAreaRef} className="min-h-0 min-w-0 flex-1 overflow-y-auto px-4 sm:px-8" aria-labelledby="weekly-dvar-torah-title">
        <div className="enter-softly mx-auto w-full max-w-[54rem] pb-16 pt-7 sm:pt-9">
          <div className="max-w-[46rem]">
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-pomegranate">Weekly Dvar Torah</p>
            <h1 id="weekly-dvar-torah-title" className="mt-2 font-display text-[clamp(2.15rem,4vw,3.1rem)] leading-[1.04] tracking-[-0.04em] text-ink">
              A teaching for the week.
            </h1>
            <p className="mt-3 max-w-[43rem] text-sm leading-6 text-ink-soft sm:text-base">
              A new reflection follows the upcoming Shabbat reading and appears here when it is ready.
            </p>
          </div>

          <nav className="mt-7 flex w-fit rounded-xl border border-line bg-stone/55 p-1" aria-label="Weekly learning">
            <button type="button" aria-pressed={view === 'current'} onClick={showCurrentTeaching} className={`inline-flex min-h-10 items-center gap-2 rounded-lg px-3.5 text-sm font-semibold transition ${view === 'current' ? 'bg-paper text-ink shadow-sm' : 'text-ink-soft hover:text-pomegranate'}`}>
              <BookOpenText aria-hidden="true" className="size-4" strokeWidth={1.7} />
              This week
            </button>
            <button type="button" aria-pressed={view !== 'current'} onClick={showArchive} className={`inline-flex min-h-10 items-center gap-2 rounded-lg px-3.5 text-sm font-semibold transition ${view !== 'current' ? 'bg-paper text-ink shadow-sm' : 'text-ink-soft hover:text-pomegranate'}`}>
              <BookMarked aria-hidden="true" className="size-4" strokeWidth={1.7} />
              Past teachings
            </button>
          </nav>

          {view === 'archive' ? (
            <DvarTorahArchive archive={archive} searchDraft={archiveSearchDraft} activeSearch={archiveSearch} isLoading={isArchiveLoading} loadError={archiveError} articleError={archivedArticleError} loadingArticleKey={archivedArticleLoadingKey} onSearchDraftChange={setArchiveSearchDraft} onSearch={searchArchive} onPageChange={changeArchivePage} onRetry={retryArchive} onOpenArticle={(weekKey) => void openArchivedArticle(weekKey)} />
          ) : view === 'archivedArticle' && archivedArticle !== null ? (
            <div>
              <button type="button" onClick={showArchive} className="mt-8 inline-flex min-h-11 items-center gap-2 rounded-lg pr-3 text-sm font-semibold text-ink-soft transition hover:text-pomegranate">
                <ArrowLeft aria-hidden="true" className="size-4" strokeWidth={1.8} />
                Back to past teachings
              </button>
              <PublishedArticle article={archivedArticle} sources={sources} selectedSourceNumber={selectedSourceNumber} onSelectSource={openSourceReader} />
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
            <PublishedArticle article={publication.dvarTorah} showFallbackNotice={!publication.isCurrentWeek} sources={sources} selectedSourceNumber={selectedSourceNumber} onSelectSource={openSourceReader} />
          )}
        </div>
      </section>
      {article === null || selectedSourceIndex < 0 ? null : <SourceReader messageId={`weekly-dvar-torah-${article.week.weekKey}`} sources={sources} selectedIndex={selectedSourceIndex} showSourceContextByDefault={false} onSelectSourceNumber={setSelectedSourceNumber} onClose={closeSourceReader} />}
    </div>
  )
}

interface PublishedArticleProps {
  article: WeeklyDvarTorahArticle
  showFallbackNotice?: boolean
  sources: readonly ConversationSource[]
  selectedSourceNumber: number | null
  onSelectSource(sourceNumber: number, trigger: HTMLButtonElement): void
}

function PublishedArticle({ article, showFallbackNotice = false, sources, selectedSourceNumber, onSelectSource }: PublishedArticleProps) {
  const paragraphs = normalizeDvarTorahText(article.body).split(/\r?\n\s*\r?\n/).filter((paragraph) => paragraph.trim().length > 0)
  const sourceNumbersById = new Map(article.sources.map((source, index) => [source.sourceId, index + 1]))
  return (
    <article className="mt-9 border-t border-line pt-7" aria-label={normalizeDvarTorahText(article.title)}>
      {!showFallbackNotice ? null : (
        <p className="mb-6 rounded-lg border border-brass/40 bg-brass/5 px-4 py-3 text-sm leading-6 text-ink-soft">
          This week’s teaching is still being prepared. Here is the latest available Dvar Torah.
        </p>
      )}

      <WeekDetails week={article.week} />
      <h2 className="mt-5 max-w-[47rem] font-display text-[clamp(2rem,4.5vw,3.35rem)] leading-[1.08] tracking-[-0.035em] text-ink">{normalizeDvarTorahText(article.title)}</h2>
      <DvarTorahReadAloud key={article.week.weekKey} title={article.title} body={article.body} />
      <div className="mt-8 max-w-[46rem] space-y-6 border-l-2 border-brass/55 pl-5 sm:pl-7">
        {paragraphs.map((paragraph, index) => <p key={index} className="whitespace-pre-line text-base leading-8 text-ink-soft sm:text-[1.08rem]">{renderParagraph(paragraph, sourceNumbersById, selectedSourceNumber, onSelectSource)}</p>)}
      </div>
      {sources.length === 0 ? null : <p className="mt-8 inline-flex max-w-[46rem] items-center gap-2 text-sm leading-6 text-muted"><BookOpenText aria-hidden="true" className="size-4 shrink-0 text-pomegranate" strokeWidth={1.7} />Select a numbered reference to read the supporting excerpt and source details.</p>}
      <p className="mt-10 max-w-[46rem] border-t border-line pt-5 text-xs leading-5 text-muted">
        This is an educational reflection, not binding <i>psak</i>. Read it as an invitation to study, question, and continue the conversation.
      </p>
    </article>
  )
}

interface DvarTorahArchiveProps {
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

function DvarTorahArchive({ archive, searchDraft, activeSearch, isLoading, loadError, articleError, loadingArticleKey, onSearchDraftChange, onSearch, onPageChange, onRetry, onOpenArticle }: DvarTorahArchiveProps) {
  const items = archive?.items ?? []

  return (
    <section className="mt-9 border-t border-line pt-7" aria-labelledby="dvar-torah-archive-title" aria-busy={isLoading}>
      <div className="max-w-[46rem]">
        <p className="text-xs font-semibold uppercase tracking-[0.15em] text-pomegranate">Archive</p>
        <h2 id="dvar-torah-archive-title" className="mt-2 font-display text-[clamp(1.85rem,4vw,2.65rem)] leading-tight tracking-[-0.03em] text-ink">Explore past Dvar Torahs.</h2>
        <p className="mt-2 text-sm leading-6 text-ink-soft">Browse earlier weekly reflections by title, parashah, holiday, date, or topic.</p>
      </div>

      <form className="mt-6 max-w-[46rem]" role="search" onSubmit={(event) => {
        event.preventDefault()
        onSearch(searchDraft)
      }}>
        <label htmlFor="dvar-torah-archive-search" className="text-sm font-semibold text-ink">Search past teachings</label>
        <div className="mt-2 flex gap-2">
          <div className="relative min-w-0 flex-1">
            <Search aria-hidden="true" className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-muted" strokeWidth={1.8} />
            <input id="dvar-torah-archive-search" type="search" maxLength={120} value={searchDraft} onChange={(event) => onSearchDraftChange(event.target.value)} placeholder="Try ‘Nitzavim’ or ‘community’" className="h-12 w-full rounded-xl border border-line-strong bg-paper pl-10 pr-3 text-sm text-ink shadow-sm transition placeholder:text-muted/70 hover:border-ink/35 focus:border-pomegranate focus:outline-none focus:ring-2 focus:ring-pomegranate/15" />
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

      {articleError === null ? null : <p className="mt-5 max-w-[46rem] rounded-lg border border-pomegranate/25 bg-pomegranate/5 px-4 py-3 text-sm text-pomegranate" role="alert">{articleError}</p>}
      {loadError === null ? null : (
        <div className="mt-6 max-w-[46rem] rounded-xl border border-pomegranate/25 bg-pomegranate/5 px-4 py-4" role="alert">
          <p className="text-sm text-pomegranate">{loadError}</p>
          <button type="button" onClick={onRetry} className="mt-3 inline-flex min-h-10 items-center gap-2 text-sm font-semibold text-ink hover:text-pomegranate">
            <RefreshCw aria-hidden="true" className="size-4" strokeWidth={1.8} />
            Try again
          </button>
        </div>
      )}

      {archive === null && isLoading ? (
        <div className="mt-8 flex min-h-40 max-w-[46rem] items-center justify-center rounded-xl border border-line bg-stone/35" role="status">
          <LoaderCircle aria-hidden="true" className="mr-2 size-4 animate-spin text-pomegranate" />
          <span className="text-sm text-muted">Loading the latest 10 weeks…</span>
        </div>
      ) : loadError !== null && archive === null ? null : items.length === 0 ? (
        <div className="mt-8 max-w-[46rem] rounded-xl border border-line bg-stone/35 px-5 py-8 text-center">
          <BookMarked aria-hidden="true" className="mx-auto size-6 text-brass" strokeWidth={1.6} />
          <p className="mt-3 font-display text-xl text-ink">No past teachings found.</p>
          <p className="mt-1 text-sm text-muted">Try a different title, parashah, date, or topic.</p>
        </div>
      ) : (
        <div className={`mt-7 max-w-[46rem] transition-opacity ${isLoading ? 'opacity-55' : 'opacity-100'}`}>
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
                  <button type="button" disabled={loadingArticleKey !== null} onClick={() => onOpenArticle(item.week.weekKey)} aria-label={`Open ${title}`} className="group grid min-h-32 w-full grid-cols-[1fr_auto] gap-4 px-1 py-5 text-left transition hover:bg-stone/45 disabled:cursor-wait disabled:opacity-65 sm:px-3">
                    <span className="min-w-0">
                      <span className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted">
                        <span className="font-semibold text-ink-soft">{formatShabbatDate(item.week.shabbatDate)}</span>
                        <span>{normalizeDvarTorahText(item.week.hebrewDate)}</span>
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
    <div className="mt-9 border-y border-line py-9">
      <WeekDetails week={week} />
      <div className="mt-7 flex max-w-[46rem] gap-4">
        <div className="flex size-11 shrink-0 items-center justify-center rounded-full bg-stone-deep text-pomegranate">
          <BookOpenText aria-hidden="true" className="size-5" strokeWidth={1.65} />
        </div>
        <div>
          <h2 className="font-display text-2xl text-ink">This week’s teaching is being prepared.</h2>
          <p className="mt-2 text-sm leading-6 text-ink-soft">The calendar and publication path are ready. The reflection will appear here after the weekly generator publishes it.</p>
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
    <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-muted">
      <span className="inline-flex items-center gap-2 font-semibold text-ink-soft">
        <CalendarDays aria-hidden="true" className="size-4 text-brass" strokeWidth={1.7} />
        {formatShabbatDate(week.shabbatDate)}
      </span>
      <span>{normalizeDvarTorahText(week.hebrewDate)}</span>
      <span>{readingName}</span>
      {holiday === null || holiday.length === 0 ? null : <span className="inline-flex items-center gap-1.5 rounded-full border border-brass/45 bg-brass/8 px-2.5 py-1 font-semibold text-ink-soft"><Sparkles aria-hidden="true" className="size-3.5 text-brass" strokeWidth={1.8} />{holiday}</span>}
    </div>
  )
}

function LoadError({ message, onRetry }: { message: string; onRetry(): void }) {
  return (
    <div className="mt-9 max-w-[46rem] border-y border-pomegranate/25 bg-pomegranate/5 px-5 py-6" role="alert">
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

function renderParagraph(paragraph: string, sourceNumbersById: ReadonlyMap<string, number>, selectedSourceNumber: number | null, onSelectSource: PublishedArticleProps['onSelectSource']) {
  return paragraph.split(/(\[[A-Za-z][A-Za-z0-9_-]*\])/g).map((part, index) => {
    const match = /^\[([A-Za-z][A-Za-z0-9_-]*)\]$/.exec(part)
    if (match === null) {
      return part
    }

    const sourceNumber = sourceNumbersById.get(match[1])
    if (sourceNumber === undefined) {
      return part
    }

    const isSelected = sourceNumber === selectedSourceNumber
    return (
      <button key={`${match[1]}-${index}`} type="button" aria-label={`View source ${sourceNumber}`} aria-expanded={isSelected} onClick={(event) => onSelectSource(sourceNumber, event.currentTarget)} className={`relative mx-0.5 inline-flex rounded-sm px-0.5 font-semibold text-pomegranate underline decoration-pomegranate/35 underline-offset-4 transition hover:bg-pomegranate/8 hover:decoration-pomegranate ${isSelected ? 'bg-pomegranate/10 ring-1 ring-pomegranate/45' : ''}`}>
        [{sourceNumber}]
      </button>
    )
  })
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

  return holiday !== null && holiday.length > 0 ? 'Holiday reading' : 'Shabbat reading'
}
