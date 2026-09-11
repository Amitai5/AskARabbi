import { useId, useRef, useState, type ReactNode } from 'react'
import { ChevronDown, SlidersHorizontal } from 'lucide-react'
import { AllCalendarFilters, CalendarCategories, type CalendarCategorySetting, type CalendarEvent, type CalendarFilters, type CalendarRange } from './calendarTypes.ts'
import { formatBeginning, formatCivilDate, formatEventRange } from './calendarFormatting.ts'

interface Props {
  days: CalendarRange
  onRange(days: CalendarRange): void
  events: CalendarEvent[]
  filters: CalendarFilters
  onFilters(filters: CalendarFilters): void
  disabled?: boolean
  notice?: ReactNode
  isAvailable?: boolean
}

export function HolidayAgenda({ days, onRange, events, filters, onFilters, disabled = false, notice, isAvailable = true }: Props) {
  const [filtersOpen, setFiltersOpen] = useState(false)
  const filterId = useId()
  const toggle = useRef<HTMLButtonElement>(null)
  const hiddenCount = CalendarCategories.filter(([key]) => !filters[key]).length
  function change(key: CalendarCategorySetting, checked: boolean) { onFilters({ ...filters, [key]: checked }) }

  return <section aria-label="Upcoming holidays" className="min-w-0">
    <div className="flex items-center justify-between gap-4">
      <h2 className="font-display text-3xl">Upcoming holidays</h2>
      <button ref={toggle} type="button" aria-label="Filter holidays" aria-expanded={filtersOpen} aria-controls={filterId} title="Filter holidays" onClick={() => setFiltersOpen(value => !value)} className={`relative inline-flex size-11 shrink-0 items-center justify-center rounded-lg border transition hover:bg-stone focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate ${filtersOpen || hiddenCount ? 'border-pomegranate bg-pomegranate/5 text-pomegranate' : 'border-line-strong text-ink-soft'}`}>
        <SlidersHorizontal className="size-5" aria-hidden="true" />
        {hiddenCount > 0 ? <span className="absolute -right-1 -top-1 flex size-4 items-center justify-center rounded-full bg-pomegranate text-[10px] font-semibold text-white" aria-label={`${hiddenCount} categories hidden`}>{hiddenCount}</span> : null}
      </button>
    </div>
    <div className="mt-4 inline-flex max-w-full rounded-lg border border-line bg-stone p-1" role="group" aria-label="Agenda range">
      {([90, 180, 360] as const).map(value => <button key={value} type="button" aria-pressed={days === value} onClick={() => onRange(value)} className={`min-h-11 rounded-md px-3 text-sm sm:px-4 ${days === value ? 'bg-paper font-semibold shadow-sm' : 'text-ink-soft hover:bg-paper/60'}`}>{value === 90 ? 'Next 90 days' : `${value} days`}</button>)}
    </div>
    <div id={filterId} hidden={!filtersOpen} onKeyDown={event => { if (event.key === 'Escape') { setFiltersOpen(false); toggle.current?.focus() } }} className="mt-4 rounded-xl border border-line bg-stone/40 p-4 sm:p-5">
      <fieldset disabled={disabled}>
        <legend className="font-semibold">Event filters</legend>
        <p className="mt-1 text-sm text-muted">Choose which holidays and observances to show.</p>
        <div className="mt-3 grid gap-x-6 gap-y-1 sm:grid-cols-2 xl:grid-cols-3">{CalendarCategories.map(([key, label]) => <label key={key} className="flex min-h-11 cursor-pointer items-center gap-3 text-sm text-ink-soft"><input type="checkbox" checked={filters[key]} onChange={event => change(key, event.target.checked)} className="size-5 accent-pomegranate" />{label}</label>)}</div>
        <button type="button" disabled={hiddenCount === 0} onClick={() => onFilters({ ...AllCalendarFilters })} className="mt-2 min-h-11 text-sm font-semibold text-pomegranate underline underline-offset-4 disabled:text-muted disabled:no-underline">Show everything</button>
      </fieldset>
    </div>
    {notice}
    <div className="mt-5">{events.map(event => <AgendaEntry key={event.id} event={event} />)}</div>
    {events.length === 0 && isAvailable ? <p className="py-8 text-sm text-muted">No events in this range with the selected filters. Try a longer range or enable more categories.</p> : null}
  </section>
}

function AgendaEntry({ event }: { event: CalendarEvent }) {
  return <details id={`event-${event.id}`} className="group border-t border-line py-1">
    <summary className="flex cursor-pointer list-none items-center gap-4 py-4 [&::-webkit-details-marker]:hidden"><div aria-hidden="true" className="w-14 shrink-0 self-start pt-1 text-center"><p className="text-xs uppercase tracking-widest text-muted">{formatCivilDate(event.startDate, { month: 'short' })}</p><p className="font-display text-3xl">{formatCivilDate(event.startDate, { day: 'numeric' })}</p><p className="text-xs text-muted">{formatCivilDate(event.startDate, { year: 'numeric' })}</p></div><div className="min-w-0 flex-1"><h3 className="font-display text-2xl leading-tight">{event.title}{event.isOngoing ? <span className="ml-2 align-middle font-sans text-xs font-normal text-pomegranate">Happening now</span> : null}</h3><p className="mt-1 text-sm text-ink-soft">{formatEventRange(event)}</p><p className="mt-1 text-xs leading-5 text-muted">{formatBeginning(event)}</p></div><ChevronDown className="size-4 shrink-0 transition group-open:rotate-180" aria-hidden="true" /></summary>
    <div className="pb-5 pl-[4.5rem] text-sm leading-7 text-ink-soft"><p>{event.explanation}</p><ul className="my-3 space-y-1">{event.occurrences.map(occurrence => <li key={`${occurrence.title}-${occurrence.date}`}><time dateTime={occurrence.date}>{formatCivilDate(occurrence.date)}</time> — {occurrence.title}</li>)}</ul></div>
  </details>
}
