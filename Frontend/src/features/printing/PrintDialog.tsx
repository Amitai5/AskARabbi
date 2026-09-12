import { useEffect, useId, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Printer, X } from 'lucide-react'
import { normalizeDisplayText } from '../../displayText.ts'
import { selectHolidayAgenda } from '../calendar/calendarAgenda.ts'
import { CalendarRanges, type CalendarRange } from '../calendar/calendarTypes.ts'
import { formatEventRange } from '../calendar/calendarFormatting.ts'
import { DefaultPrintOptions, type PrintRequest } from './printTypes.ts'
import { PrintFrame } from './PrintFrame.tsx'
import './printDialog.css'

export interface PrintDialogProps { request: PrintRequest; returnFocusTo?: HTMLElement; onClose(): void }
const SelectClass = 'mt-2 min-h-11 w-full rounded-lg border border-line-strong bg-parchment px-3 text-base text-ink'

export function PrintDialog({ request, returnFocusTo, onClose }: PrintDialogProps) {
  const [options, setOptions] = useState(DefaultPrintOptions)
  const [days, setDays] = useState<CalendarRange>(request.kind === 'calendar' ? request.calendar.days : 90)
  const [search, setSearch] = useState(request.kind === 'calendar' ? request.calendar.search : '')
  const [selectedIds, setSelectedIds] = useState<ReadonlySet<string>>(() => new Set(request.kind === 'answers' ? request.answers.filter(answer => !request.initialAnswerId || answer.id === request.initialAnswerId).map(answer => answer.id) : request.kind === 'calendar' ? selectHolidayAgenda(request.calendar.events, request.calendar.startDate, request.calendar.days, request.calendar.filters, request.calendar.search).events.map(event => event.id) : []))
  const [ready, setReady] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<'options' | 'preview'>('options')
  const dialogRef = useRef<HTMLDialogElement>(null)
  const closeRef = useRef<HTMLButtonElement>(null)
  const frameRef = useRef<HTMLIFrameElement>(null)
  const id = useId()
  const agenda = request.kind === 'calendar' ? selectHolidayAgenda(request.calendar.events, request.calendar.startDate, days, request.calendar.filters, search) : null
  const items = request.kind === 'answers' ? request.answers.map((answer, index) => ({ id: answer.id, title: answer.question ?? `Answer ${index + 1}`, description: `Answer ${index + 1}` })) : agenda?.events.map(event => ({ id: event.id, title: event.title, description: formatEventRange(event) })) ?? []
  const canPrint = request.kind === 'teaching' || selectedIds.size > 0 || (request.kind === 'calendar' && request.calendar.overview && (options.includeCalendarSummary || options.includeLocalTimes))

  useEffect(() => {
    const dialog = dialogRef.current
    const trigger = returnFocusTo ?? document.activeElement
    dialog?.showModal()
    closeRef.current?.focus()
    return () => { dialog?.close(); if (trigger instanceof HTMLElement && trigger.isConnected) { trigger.focus({ preventScroll: true }) } }
  }, [returnFocusTo])

  function toggleItem(itemId: string) {
    setSelectedIds(current => { const next = new Set(current); if (next.has(itemId)) { next.delete(itemId) } else { next.add(itemId) }; return next })
  }
  function changeCalendarRange(value: CalendarRange, query = search) {
    if (request.kind !== 'calendar') { return }
    setDays(value)
    setSearch(query)
    setSelectedIds(new Set(selectHolidayAgenda(request.calendar.events, request.calendar.startDate, value, request.calendar.filters, query).events.map(event => event.id)))
  }
  function print() {
    const frame = frameRef.current?.contentWindow
    if (!ready || !canPrint || !frame) { return }
    setError(null)
    try { frame.focus(); frame.print() }
    catch { setError('The browser could not open its print dialog. Try again in your browser.') }
  }
  return createPortal(<dialog ref={dialogRef} className="study-print-dialog m-auto h-[min(92dvh,68rem)] max-h-[calc(100dvh-1rem)] w-[min(94rem,calc(100vw-1rem))] max-w-none overflow-hidden rounded-2xl border border-line-strong bg-parchment p-0 text-ink shadow-menu" aria-labelledby={`${id}-title`} aria-describedby={`${id}-description`} onCancel={event => { event.preventDefault(); onClose() }}>
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex shrink-0 items-start justify-between gap-4 border-b border-line bg-paper px-5 py-4 sm:px-6"><div><h2 id={`${id}-title`} className="font-display text-2xl">Print a study copy</h2><p id={`${id}-description`} className="mt-1 text-sm text-muted">Choose what to include, then print or save a PDF.</p></div><button ref={closeRef} type="button" aria-label="Close print preview" onClick={onClose} className="flex size-11 shrink-0 items-center justify-center rounded-lg hover:bg-stone"><X aria-hidden="true" className="size-5" /></button></header>
      <div role="tablist" aria-label="Print setup" className="flex shrink-0 gap-2 border-b border-line px-5 py-2 md:hidden">{(['options', 'preview'] as const).map(value => <button key={value} type="button" role="tab" id={`${id}-${value}-tab`} tabIndex={tab === value ? 0 : -1} aria-selected={tab === value} aria-controls={`${id}-${value}`} onClick={() => setTab(value)} onKeyDown={event => { if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); setTab(value === 'options' ? 'preview' : 'options'); (event.currentTarget.parentElement?.querySelector<HTMLButtonElement>(`button[aria-selected="false"]`))?.focus() } }} className={`min-h-10 flex-1 rounded-lg px-3 text-sm font-semibold ${tab === value ? 'bg-stone text-ink' : 'text-muted'}`}>{value === 'options' ? 'Options' : 'Preview'}</button>)}</div>
      <div className="flex min-h-0 flex-1 flex-col md:flex-row">
        <div id={`${id}-options`} role="tabpanel" aria-labelledby={`${id}-options-tab`} className={`min-h-0 w-full shrink-0 overflow-y-auto overscroll-contain p-5 md:block md:w-[22rem] md:border-r md:border-line md:p-6 ${tab === 'options' ? 'block' : 'hidden'}`}>
          {request.kind === 'answers' ? <p className="mb-4 text-sm leading-6 text-muted">Choose any answers from this conversation. Your unsent draft and account details are never included.</p> : null}
          {request.kind === 'calendar' ? <><label className="block text-sm font-semibold" htmlFor={`${id}-range`}>Calendar range<select id={`${id}-range`} value={agenda?.days ?? days} onChange={event => changeCalendarRange(Number(event.target.value) as CalendarRange)} className={SelectClass}>{CalendarRanges.map(range => <option key={range} value={range} disabled={range < (agenda?.minimumDays ?? 90)}>Next {range} days</option>)}</select></label><p className="mt-2 text-sm text-muted">Uses your calendar’s holiday filters.</p>{search ? <p className="mt-3 text-sm">Search: “{search}” <button type="button" onClick={() => changeCalendarRange(days, '')} className="min-h-10 text-pomegranate underline">Include other holidays</button></p> : null}</> : null}
          {request.kind !== 'teaching' ? <fieldset className="mt-4"><legend className="text-sm font-semibold">{request.kind === 'answers' ? 'Answers to print' : 'Holidays to print'} ({selectedIds.size})</legend><div className="my-2 flex gap-4 text-sm"><button type="button" onClick={() => setSelectedIds(new Set(items.map(item => item.id)))} className="min-h-10 text-pomegranate underline">Select all</button><button type="button" onClick={() => setSelectedIds(new Set())} className="min-h-10 text-muted underline">Clear selection</button></div><div className="max-h-64 space-y-1 overflow-y-auto overscroll-contain rounded-lg border border-line bg-paper p-2">{items.length ? items.map(item => <label key={item.id} className="flex min-h-11 cursor-pointer items-start gap-3 rounded-md p-2 hover:bg-stone"><input type="checkbox" checked={selectedIds.has(item.id)} onChange={() => toggleItem(item.id)} className="mt-1 size-4 shrink-0 accent-pomegranate" /><span className="min-w-0 text-sm"><span dir="auto" className="line-clamp-3 break-words">{normalizeDisplayText(item.title)}</span><span className="mt-1 block text-xs text-muted">{item.description}</span></span></label>) : <p className="p-2 text-sm text-muted">No holidays match this range and your filters.</p>}</div></fieldset> : null}
          <div className="mt-5 grid grid-cols-2 gap-3"><label className="text-sm font-semibold" htmlFor={`${id}-paper`}>Paper size<select id={`${id}-paper`} className={SelectClass} value={options.paper} onChange={event => setOptions(current => ({ ...current, paper: event.target.value as 'letter' | 'a4' }))}><option value="letter">US Letter</option><option value="a4">A4</option></select></label><label className="text-sm font-semibold" htmlFor={`${id}-size`}>Print text<select id={`${id}-size`} className={SelectClass} value={options.textSize} onChange={event => setOptions(current => ({ ...current, textSize: event.target.value as 'standard' | 'large' }))}><option value="standard">Standard</option><option value="large">Large</option></select></label></div>
          <fieldset className="mt-6 space-y-3"><legend className="mb-3 text-sm font-semibold">Include in your copy</legend>
            {request.kind === 'answers' ? <PrintOption label="Original questions" checked={options.includeQuestions} onChange={value => setOptions(current => ({ ...current, includeQuestions: value }))} /> : null}
            {request.kind !== 'calendar' ? <><PrintOption label="Source excerpts" checked={options.includeExcerpts} onChange={value => setOptions(current => ({ ...current, includeExcerpts: value }))} /><p className="text-xs leading-5 text-muted">Citations, source details, and attribution are always included.</p>{request.kind === 'answers' ? <PrintOption label="Surrounding source context" checked={options.includeContext} disabled={!options.includeExcerpts} onChange={value => setOptions(current => ({ ...current, includeContext: value }))} /> : null}</> : null}
            {request.kind === 'calendar' && request.calendar.overview ? <><PrintOption label="Today and this Shabbat" checked={options.includeCalendarSummary} onChange={value => setOptions(current => ({ ...current, includeCalendarSummary: value }))} /><PrintOption label="Local times and location" checked={options.includeLocalTimes} onChange={value => setOptions(current => ({ ...current, includeLocalTimes: value }))} /></> : null}
            <PrintOption label="Space for study notes" checked={options.includeNotes} onChange={value => setOptions(current => ({ ...current, includeNotes: value }))} />
            {request.kind === 'answers' && request.answers.length > 1 ? <PrintOption label="Start each answer on a new page" checked={options.separateAnswers} onChange={value => setOptions(current => ({ ...current, separateAnswers: value }))} /> : null}
          </fieldset>
          <p className="mt-5 text-xs leading-5 text-muted">A small book logo and AskARabbi.ai link appear in the corner of every printed page. The preview shows the content; your browser’s dialog shows final page breaks. For the cleanest copy, turn off the browser’s own headers and footers.</p>
        </div>
        <div id={`${id}-preview`} role="tabpanel" aria-labelledby={`${id}-preview-tab`} className={`min-h-0 min-w-0 flex-1 bg-stone p-2 md:block md:p-4 ${tab === 'preview' ? 'block' : 'hidden'}`}><PrintFrame frameRef={frameRef} request={request} options={options} selectedIds={selectedIds} events={agenda?.events ?? []} days={agenda?.days ?? days} onReady={setReady} onClose={onClose} /></div>
      </div>
      <footer className="flex shrink-0 flex-wrap items-center justify-between gap-3 border-t border-line bg-paper px-5 py-3 sm:px-6"><p className="text-sm text-muted" role="status">{!canPrint ? 'Choose at least one item to print.' : ready ? 'Ready for paper or PDF.' : 'Preparing your preview…'}</p><div className="flex items-center gap-3"><button type="button" onClick={onClose} className="min-h-11 rounded-lg border border-line-strong px-4 text-sm font-semibold hover:bg-stone">Cancel</button><button type="button" disabled={!ready || !canPrint} onClick={print} className="inline-flex min-h-11 items-center gap-2 rounded-lg bg-pomegranate px-4 text-sm font-semibold text-white hover:bg-pomegranate-dark disabled:opacity-50"><Printer aria-hidden="true" className="size-4" />Print / Save PDF</button></div>{error ? <p role="alert" className="w-full text-sm text-pomegranate">{error}</p> : null}</footer>
    </div>
  </dialog>, document.body)
}

function PrintOption({ label, checked, disabled, onChange }: { label: string; checked: boolean; disabled?: boolean; onChange(value: boolean): void }) {
  return <label className={`flex min-h-8 cursor-pointer items-start gap-3 text-sm ${disabled ? 'opacity-50' : ''}`}><input type="checkbox" checked={checked} disabled={disabled} onChange={event => onChange(event.target.checked)} className="mt-0.5 size-4 shrink-0 accent-pomegranate" /><span>{label}</span></label>
}
