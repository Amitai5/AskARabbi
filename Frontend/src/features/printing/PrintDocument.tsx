import { BookOpen } from 'lucide-react'
import { normalizeDisplayText } from '../../displayText.ts'
import { addCivilDays } from '../calendar/calendarAgenda.ts'
import { formatBeginning, formatCivilDate, formatEventRange } from '../calendar/calendarFormatting.ts'
import type { CalendarEvent, CalendarRange } from '../calendar/calendarTypes.ts'
import { PrintText } from './PrintText.tsx'
import { safePrintUrl, teachingPrintSources, type PrintOptions, type PrintRequest, type PrintSource } from './printTypes.ts'

interface Props { request: PrintRequest; options: PrintOptions; selectedIds: ReadonlySet<string>; events: readonly CalendarEvent[]; days: CalendarRange }

export function PrintDocument({ request, options, selectedIds, events, days }: Props) {
  const title = request.kind === 'answers' ? request.title : request.kind === 'teaching' ? request.article.title : 'Jewish Calendar'
  const kind = request.kind === 'answers' ? 'Conversation study copy' : request.kind === 'teaching' ? 'Weekly Dvar Torah' : 'Dates & observances'
  const teachingSources = request.kind === 'teaching' ? teachingPrintSources(request.article) : []
  return <main className={`print-document print-size-${options.textSize} print-paper-${options.paper}`}>
    <table className="print-layout" role="presentation"><tbody><tr><td>
    <header className="print-masthead"><PrintBrand /><span>{kind}</span></header>
    <header className="print-title"><p className="print-eyebrow">For study, reflection & conversation</p><h1 dir="auto">{normalizeDisplayText(title)}</h1>
      {request.kind === 'teaching' ? <p className="print-subtitle">{formatCivilDate(request.article.week.shabbatDate, { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' })}<br />{normalizeDisplayText(request.article.week.hebrewDate)} · {request.article.week.inIsrael ? 'Israel' : 'Diaspora'}{request.article.week.parashah ? ` · Parashat ${normalizeDisplayText(request.article.week.parashah)}` : ''}{request.article.week.holiday ? ` · ${normalizeDisplayText(request.article.week.holiday)}` : ''}</p> : null}
      {request.kind === 'answers' ? <p className="print-subtitle">{selectedIds.size} selected {selectedIds.size === 1 ? 'answer' : 'answers'} · Questions and source references remain in conversation order.</p> : null}
      {request.kind === 'calendar' ? <p className="print-subtitle">{formatCivilDate(request.calendar.startDate)} - {formatCivilDate(addCivilDays(request.calendar.startDate, days - 1))}<br />{request.calendar.inIsrael ? 'Israel' : 'Diaspora'} schedule · {events.filter(event => selectedIds.has(event.id)).length} selected holidays</p> : null}
    </header>
    {request.kind === 'answers' ? request.answers.filter(answer => selectedIds.has(answer.id)).map((answer, index) => <section key={answer.id} className={`print-answer ${options.separateAnswers && index > 0 ? 'print-new-page' : ''}`}>
      <h2 className="print-section-heading">{String(index + 1).padStart(2, '0')} / Answer</h2>
      {options.includeQuestions && answer.question ? <div className="print-question"><p className="print-eyebrow">Question</p><PrintText text={answer.question} /></div> : null}
      <div className="print-prose"><PrintText text={answer.content} sources={answer.sources} prefix={`answer-${index + 1}`} /></div>
      <PrintedSources sources={answer.sources} options={options} prefix={`answer-${index + 1}`} />
      {options.includeNotes ? <StudyNotes /> : null}
    </section>) : null}
    {request.kind === 'teaching' ? <>
      <div className="print-prose"><PrintText text={request.article.body} sources={teachingSources} prefix="teaching" includeReferences={options.includeSourceReferences} /></div>
      {request.article.centralTeaching ? <aside className="print-takeaway"><h2>For reflection</h2><PrintText text={request.article.centralTeaching} sources={teachingSources} prefix="teaching" includeReferences={options.includeSourceReferences} /></aside> : null}
      {options.includeSourceReferences ? <PrintedSources sources={teachingSources} options={options} prefix="teaching" /> : null}
      {options.includeNotes ? <StudyNotes /> : null}
    </> : null}
    {request.kind === 'calendar' ? <PrintedCalendar request={request} options={options} events={events.filter(event => selectedIds.has(event.id))} /> : null}
    <p className="print-disclaimer">{request.kind === 'calendar' ? 'Check the location, time zone, and date before using local times. Community customs may differ.' : request.kind === 'teaching' && !options.includeSourceReferences ? 'Source references are available with this teaching on AskARabbi.ai. This is an educational reflection, not personal halakhic guidance.' : 'AskRabbi offers source-based Jewish learning, not personal halakhic rulings. Check the cited sources; for practical guidance, consult a qualified rabbi.'} Terms: <a href="https://askarabbi.ai/terms-of-service" target="_blank" rel="noopener noreferrer">askarabbi.ai/terms-of-service</a>. Privacy: <a href="https://askarabbi.ai/privacy-policy" target="_blank" rel="noopener noreferrer">askarabbi.ai/privacy-policy</a>.</p>
    </td></tr></tbody><tfoot><tr><td><footer className="print-watermark"><a href="https://askarabbi.ai" target="_blank" rel="noopener noreferrer" aria-label="AskARabbi.ai"><BookSymbol /><span>AskARabbi.ai</span></a></footer></td></tr></tfoot></table>
  </main>
}

function BookSymbol() { return <span className="print-book" aria-hidden="true"><BookOpen viewBox="0 0 24 26" strokeWidth={1.65}><path d="M12 20v5" stroke="#a92d43" strokeWidth={0.8} /></BookOpen></span> }
function PrintBrand() { return <a className="print-brand" href="https://askarabbi.ai" target="_blank" rel="noopener noreferrer"><BookSymbol /><span>AskRabbi</span></a> }
function StudyNotes() { return <section className="print-notes"><h2>Notes for study & discussion</h2><div /><div /><div /></section> }

function PrintedSources({ sources, options, prefix }: { sources: readonly PrintSource[]; options: PrintOptions; prefix: string }) {
  if (!sources.length) { return null }
  return <section className="print-sources"><h2>Sources{options.includeExcerpts ? ' & excerpts' : ''}</h2><ol>{sources.map((source, index) => {
    const url = safePrintUrl(source.url)
    const attribution = safePrintUrl(source.attributionUrl)
    const excerpts = [...new Set(source.excerpts.map(normalizeDisplayText).filter(value => value.trim()))]
    return <li key={`${source.number}-${index}`} id={`${prefix}-source-${source.number}`} className="print-source">
      <h3><span className="print-source-number">[{source.number}]</span> {normalizeDisplayText(source.title)}{source.reference && source.reference !== source.title ? ` — ${normalizeDisplayText(source.reference)}` : ''}</h3>
      {source.hebrewTitle ? <p dir="rtl" lang="he" className="print-hebrew">{normalizeDisplayText(source.hebrewTitle)}</p> : null}
      {source.details ? <p className="print-source-details">{normalizeDisplayText(source.details)}</p> : null}
      {options.includeExcerpts ? (excerpts.length ? excerpts : source.context ? [normalizeDisplayText(source.context)] : []).map((excerpt, i) => <blockquote key={i}><PrintText text={excerpt} /></blockquote>) : null}
      {options.includeExcerpts && options.includeContext && source.context && excerpts.length > 0 && !excerpts.includes(normalizeDisplayText(source.context)) ? <div className="print-source-context"><h4>Surrounding context</h4><PrintText text={source.context} /></div> : null}
      <p className="print-source-credit">{normalizeDisplayText(source.license)}{url ? <><br /><a href={url} target="_blank" rel="noopener noreferrer">{url}</a></> : null}{attribution && attribution !== url ? <><br />Attribution: <a href={attribution} target="_blank" rel="noopener noreferrer">{attribution}</a></> : null}</p>
    </li>
  })}</ol></section>
}

function PrintedCalendar({ request, options, events }: { request: Extract<PrintRequest, { kind: 'calendar' }>; options: PrintOptions; events: readonly CalendarEvent[] }) {
  const { overview, savedNote } = request.calendar
  return <>
    {savedNote ? <p className="print-notice">{savedNote}</p> : null}
    {options.includeCalendarSummary && overview ? <section className="print-calendar-summary">
      <div><h2>Today</h2><p>{formatCivilDate(overview.today.gregorianDate)}<br /><strong>{normalizeDisplayText(overview.today.hebrewDate)}</strong><br /><bdi lang="he" dir="rtl">{overview.today.hebrewScript}</bdi></p><p className="print-small">{overview.today.isDaytimeOnly ? 'Daytime date; sunset-aware date unavailable.' : overview.today.isAfterSunset ? 'After local sunset; Hebrew date has advanced.' : 'Hebrew date follows local sunset.'}</p></div>
      <div><h2>This Shabbat</h2><p>{formatCivilDate(overview.shabbat.shabbatDate)}<br />{normalizeDisplayText(overview.shabbat.hebrewDate)}<br /><strong>{overview.shabbat.parashah ? `Parashat ${normalizeDisplayText(overview.shabbat.parashah)}` : normalizeDisplayText(overview.shabbat.holiday ?? 'Holiday reading')}</strong></p></div>
    </section> : null}
    {options.includeLocalTimes && overview ? <section className="print-local-times"><h2>Local times</h2><p className="print-small">{overview.preferences.location?.label ?? 'No location selected'} · {overview.today.timeZone}</p>
      {overview.timing.message || !overview.timing.isAvailable || overview.timing.isStale ? <p className="print-notice">{overview.timing.message ?? (!overview.timing.isAvailable ? 'Local times are unavailable. Do not infer times from this copy.' : 'These are cached times. Check for updates before use.')}{overview.timing.isStale && overview.timing.fetchedAtUtc ? ` Last updated ${new Date(overview.timing.fetchedAtUtc).toISOString().slice(0, 10)}.` : ''}</p> : null}
      {overview.localTimes.length ? <table><thead><tr><th>Date</th><th>Observance</th><th>Local time</th></tr></thead><tbody>{overview.localTimes.map((time, index) => <tr key={index}><td>{formatCivilDate(time.date, { month: 'short', day: 'numeric', year: 'numeric' })}</td><td>{time.title}{time.context ? <span className="print-table-note">{time.context}</span> : null}<span className="print-table-note">{time.location} · {time.timeZone}</span></td><td><time dateTime={time.at}>{new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit', timeZone: time.timeZone }).format(new Date(time.at))}</time></td></tr>)}</tbody></table> : <p>No local times are available in this snapshot.</p>}
      <p className="print-small">{overview.timingConvention}</p>
    </section> : null}
    <section className="print-holidays"><h2>Holidays & observances</h2>
      {overview && (overview.holidays.message || overview.holidays.isStale || !overview.holidays.isAvailable) ? <p className="print-notice">{overview.holidays.message ?? (overview.holidays.isStale ? 'Holiday information is cached; reconnect to check for updates.' : 'Holiday information is unavailable.')}</p> : null}
      {events.length ? events.map(event => <section className="print-holiday" key={event.id}><h3>{normalizeDisplayText(event.title)}</h3><p className="print-holiday-date">{formatEventRange(event)}<br /><span>{formatBeginning(event)}</span></p><PrintText text={event.explanation} />{event.occurrences.length > 1 ? <ul className="print-holiday-occurrences">{event.occurrences.map((occurrence, index) => <li key={index}>{formatCivilDate(occurrence.date)} — {normalizeDisplayText(occurrence.title)}</li>)}</ul> : null}</section>) : <p>No holidays selected for this copy.</p>}
    </section>
    <p className="print-source-credit">Calendar data: <a href="https://www.hebcal.com/home/developer-apis" target="_blank" rel="noopener noreferrer">Hebcal</a> · <a href="https://creativecommons.org/licenses/by/4.0/" target="_blank" rel="noopener noreferrer">CC BY 4.0</a><br />https://www.hebcal.com/home/developer-apis{overview ? ` · Snapshot: ${overview.generatedAtUtc}` : ''}</p>
    {options.includeNotes ? <StudyNotes /> : null}
  </>
}
