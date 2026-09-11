import { useId, useRef, useState, type ReactNode } from 'react'
import { ChevronDown, Search, SlidersHorizontal, X } from 'lucide-react'
import { AllCalendarFilters, CalendarCategories, CalendarRanges, type CalendarCategorySetting, type CalendarEvent, type CalendarFilters, type CalendarRange } from './calendarTypes.ts'
import { formatBeginning, formatCivilDate, formatEventRange } from './calendarFormatting.ts'
import { selectHolidayAgenda } from './calendarAgenda.ts'
import { HolidayDetails } from './HolidayDetails.tsx'

interface Props {
  days: CalendarRange
  onRange(days: CalendarRange): void
  events: CalendarEvent[]
  startDate: string
  filters: CalendarFilters
  onFilters(filters: CalendarFilters): void
  disabled?: boolean
  notice?: ReactNode
  isAvailable?: boolean
}

export function HolidayAgenda({ days, onRange, events, startDate, filters, onFilters, disabled = false, notice, isAvailable = true }: Props) {
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [query, setQuery] = useState('')
  const filterId = useId()
  const searchId = useId()
  const searchInput = useRef<HTMLInputElement>(null)
  const toggle = useRef<HTMLButtonElement>(null)
  const selection = selectHolidayAgenda(events, startDate, days, filters, query)
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
    <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <div className="relative min-w-0 flex-1 sm:max-w-md">
        <label htmlFor={searchId} className="sr-only">Search upcoming holidays</label>
        <Search aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted" />
        <input ref={searchInput} id={searchId} type="search" value={query} maxLength={150} onChange={event => setQuery(event.target.value)} onKeyDown={event => { if (event.key === 'Escape' && query) { event.preventDefault(); event.stopPropagation(); setQuery('') } }} placeholder="Search holidays…" aria-describedby={`${searchId}-scope`} aria-controls={`${searchId}-results`} className="min-h-12 w-full rounded-lg border border-line-strong bg-paper py-2 pl-10 pr-11 text-base text-ink placeholder:text-muted focus:border-pomegranate focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate [&::-webkit-search-cancel-button]:appearance-none" />
        {query ? <button type="button" aria-label="Clear holiday search" onClick={() => { setQuery(''); searchInput.current?.focus() }} className="absolute inset-y-0 right-0 flex w-11 items-center justify-center rounded-r-lg text-muted hover:text-pomegranate"><X aria-hidden="true" className="size-4" /></button> : null}
      </div>
      <div className="inline-flex w-fit max-w-full shrink-0 rounded-lg border border-line bg-stone p-1" role="group" aria-label="Agenda range">
        {CalendarRanges.map(value => <button key={value} type="button" aria-pressed={selection.days === value} disabled={selection.isSearching && value < selection.minimumDays} onClick={() => onRange(value)} className={`min-h-11 rounded-md px-3 text-sm disabled:cursor-not-allowed disabled:opacity-45 sm:px-4 ${selection.days === value ? 'bg-paper font-semibold shadow-sm' : 'text-ink-soft hover:bg-paper/60'}`}>{value === 90 ? 'Next 90 days' : `${value} days`}</button>)}
      </div>
    </div>
    <p id={`${searchId}-scope`} className="mt-2 text-xs leading-6 text-muted">Search looks ahead 360 days and expands the range to include all matches. Clear search to return to your chosen range.</p>
    <div id={filterId} hidden={!filtersOpen} onKeyDown={event => { if (event.key === 'Escape') { setFiltersOpen(false); toggle.current?.focus() } }} className="mt-4 rounded-xl border border-line bg-stone/40 p-4 sm:p-5">
      <fieldset disabled={disabled}>
        <legend className="font-semibold">Event filters</legend>
        <p className="mt-1 text-sm text-muted">Choose which holidays and observances to show.</p>
        <div className="mt-3 grid gap-x-6 gap-y-1 sm:grid-cols-2 xl:grid-cols-3">{CalendarCategories.map(([key, label]) => <label key={key} className="flex min-h-11 cursor-pointer items-center gap-3 text-sm text-ink-soft"><input type="checkbox" checked={filters[key]} onChange={event => change(key, event.target.checked)} className="size-5 accent-pomegranate" />{label}</label>)}</div>
        <button type="button" disabled={hiddenCount === 0} onClick={() => onFilters({ ...AllCalendarFilters })} className="mt-2 min-h-11 text-sm font-semibold text-pomegranate underline underline-offset-4 disabled:text-muted disabled:no-underline">Show everything</button>
      </fieldset>
    </div>
    {notice}
    {selection.isSearching && isAvailable ? <p role="status" className="mt-4 text-sm text-muted">{selection.events.length === 0 ? 'No matching holidays in the next 360 days with the selected filters. Try another name or enable more categories.' : `${selection.events.length} ${selection.events.length === 1 ? 'holiday' : 'holidays'} found in the next 360 days.${selection.days > days ? ` Showing ${selection.days} days to include all matches.` : ''}`}</p> : null}
    <div id={`${searchId}-results`} className="mt-5">{selection.events.map(event => <AgendaEntry key={event.id} event={event} />)}</div>
    {selection.events.length === 0 && !selection.isSearching && isAvailable ? <p className="py-8 text-sm text-muted">No events in this range with the selected filters. Try a longer range or enable more categories.</p> : null}
  </section>
}

function AgendaEntry({ event }: { event: CalendarEvent }) {
  return <details id={`event-${event.id}`} className="group border-t border-line py-1">
    <summary className="flex cursor-pointer list-none items-center gap-4 py-4 [&::-webkit-details-marker]:hidden"><div aria-hidden="true" className="w-14 shrink-0 self-start pt-1 text-center"><p className="text-xs uppercase tracking-widest text-muted">{formatCivilDate(event.startDate, { month: 'short' })}</p><p className="font-display text-3xl">{formatCivilDate(event.startDate, { day: 'numeric' })}</p><p className="text-xs text-muted">{formatCivilDate(event.startDate, { year: 'numeric' })}</p></div><div className="min-w-0 flex-1"><h3 className="font-display text-2xl leading-tight">{event.title}{event.isOngoing ? <span className="ml-2 align-middle font-sans text-xs font-normal text-pomegranate">Happening now</span> : null}</h3><p className="mt-1 text-sm text-ink-soft">{formatEventRange(event)}</p><p className="mt-1 text-xs leading-5 text-muted">{formatBeginning(event)}</p></div><ChevronDown className="size-4 shrink-0 transition group-open:rotate-180" aria-hidden="true" /></summary>
    <div className="pb-5 pl-[4.5rem]"><HolidayDetails event={event} /></div>
  </details>
}
