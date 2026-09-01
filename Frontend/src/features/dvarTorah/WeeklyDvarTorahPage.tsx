import { useEffect, useState } from 'react'
import { ArrowLeft, BookOpenText, CalendarDays, RefreshCw } from 'lucide-react'
import type { DvarTorahClient } from './dvarTorahClient.ts'
import type { DvarTorahWeek, WeeklyDvarTorahResponse } from './dvarTorahTypes.ts'

interface WeeklyDvarTorahPageProps {
  client: DvarTorahClient
  onBack(): void
}

const ShabbatDateFormatter = new Intl.DateTimeFormat(undefined, {
  day: 'numeric',
  month: 'long',
  timeZone: 'UTC',
  weekday: 'long',
  year: 'numeric',
})

export function WeeklyDvarTorahPage({ client, onBack }: WeeklyDvarTorahPageProps) {
  const [publication, setPublication] = useState<WeeklyDvarTorahResponse | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)

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

  function retry() {
    setPublication(null)
    setLoadError(null)
    setRefreshKey((current) => current + 1)
  }

  return (
    <section className="min-h-0 flex-1 overflow-y-auto px-4 sm:px-8" aria-labelledby="weekly-dvar-torah-title">
      <div className="enter-softly mx-auto w-full max-w-[54rem] pb-16 pt-7 sm:pt-9">
        <button type="button" onClick={onBack} className="inline-flex min-h-11 items-center gap-2 rounded-lg pr-3 text-sm font-semibold text-ink-soft transition hover:text-pomegranate">
          <ArrowLeft aria-hidden="true" className="size-4" strokeWidth={1.8} />
          Back to conversation
        </button>

        <div className="mt-3 max-w-[46rem]">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-pomegranate">Weekly Dvar Torah</p>
          <h1 id="weekly-dvar-torah-title" className="mt-2 font-display text-[clamp(2.15rem,4vw,3.1rem)] leading-[1.04] tracking-[-0.04em] text-ink">
            A teaching for the week.
          </h1>
          <p className="mt-3 max-w-[43rem] text-sm leading-6 text-ink-soft sm:text-base">
            A new reflection follows the upcoming Shabbat reading and appears here when it is ready.
          </p>
        </div>

        {loadError !== null ? (
          <LoadError message={loadError} onRetry={retry} />
        ) : publication === null ? (
          <div className="mt-12 flex min-h-48 items-center justify-center border-y border-line" aria-busy="true">
            <p className="text-sm text-muted" role="status">Loading this week’s Dvar Torah…</p>
          </div>
        ) : publication.dvarTorah === null ? (
          <PendingPublication week={publication.currentWeek} onRetry={retry} />
        ) : (
          <PublishedArticle publication={publication} />
        )}
      </div>
    </section>
  )
}

function PublishedArticle({ publication }: { publication: WeeklyDvarTorahResponse }) {
  const article = publication.dvarTorah
  if (article === null) {
    return null
  }

  const paragraphs = article.body.split(/\r?\n\s*\r?\n/).filter((paragraph) => paragraph.trim().length > 0)
  return (
    <article className="mt-9 border-t border-line pt-7" aria-label={article.title}>
      {!publication.isCurrentWeek ? (
        <p className="mb-6 rounded-lg border border-brass/40 bg-brass/5 px-4 py-3 text-sm leading-6 text-ink-soft">
          This week’s teaching is still being prepared. Here is the latest available Dvar Torah.
        </p>
      ) : null}

      <WeekDetails week={article.week} />
      <h2 className="mt-5 max-w-[47rem] font-display text-[clamp(2rem,4.5vw,3.35rem)] leading-[1.08] tracking-[-0.035em] text-ink">{article.title}</h2>
      <div className="mt-8 max-w-[46rem] space-y-6 border-l-2 border-brass/55 pl-5 sm:pl-7">
        {paragraphs.map((paragraph, index) => <p key={index} className="whitespace-pre-line text-base leading-8 text-ink-soft sm:text-[1.08rem]">{paragraph}</p>)}
      </div>
      <p className="mt-10 max-w-[46rem] border-t border-line pt-5 text-xs leading-5 text-muted">
        This is an educational reflection, not binding <i>psak</i>. Read it as an invitation to study, question, and continue the conversation.
      </p>
    </article>
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
  return (
    <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-muted">
      <span className="inline-flex items-center gap-2 font-semibold text-ink-soft">
        <CalendarDays aria-hidden="true" className="size-4 text-brass" strokeWidth={1.7} />
        {formatShabbatDate(week.shabbatDate)}
      </span>
      <span>{week.hebrewDate}</span>
      <span>{getReadingName(week)}</span>
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

function getReadingName(week: DvarTorahWeek) {
  if (week.parashah !== null) {
    return `Parashat ${week.parashah}`
  }
  return week.holiday ?? 'Shabbat reading'
}
