import { useCallback, useEffect, useRef, useState } from 'react'
import { ArrowLeft, ArrowRight, BookOpenText, CalendarDays, ChevronDown, ExternalLink, MapPin, RefreshCw, Settings2 } from 'lucide-react'
import type { CalendarClient } from './calendarClient.ts'
import { CalendarCategories, type CalendarAvailability, type CalendarCategorySetting, type CalendarEvent, type CalendarOverview, type CalendarPreferences, type CalendarPreferencesResponse, type CalendarRange } from './calendarTypes.ts'
import { formatBeginning, formatCivilDate, formatEventRange } from './calendarFormatting.ts'
import { useOnlineStatus } from '../pwa/useOnlineStatus.ts'
import './calendar.css'

interface Props { client: CalendarClient; onOpenDvarTorah(): void; onBackToConversation?(): void; onOpenPersonalization?(): void }
const ButtonClass = 'inline-flex min-h-11 items-center justify-center gap-2 rounded-lg border border-line-strong bg-paper px-4 text-sm font-semibold text-ink transition hover:bg-stone disabled:opacity-50'

export function CalendarPage({ client, onOpenDvarTorah, onBackToConversation, onOpenPersonalization }: Props) {
  const [days, setDays] = useState<CalendarRange>(90)
  const [data, setData] = useState<CalendarOverview | null>(null)
  const [settings, setSettings] = useState<CalendarPreferencesResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [revision, setRevision] = useState(0)
  const [saving, setSaving] = useState(false)
  const heading = useRef<HTMLHeadingElement>(null)
  const isOnline = useOnlineStatus()
  const refresh = useCallback(() => setRevision((value) => value + 1), [])
  useEffect(() => { heading.current?.focus() }, [])

  useEffect(() => {
    if (!isOnline) { return }
    const controller = new AbortController()
    void client.getOverview(days, controller.signal)
      .then((overview) => {
        if (controller.signal.aborted) { return }
        setData(overview)
        setSettings({ cities: [], preferences: overview.preferences })
        setError(null)
      }).catch((cause: unknown) => {
        if (!controller.signal.aborted) { setError(cause instanceof Error ? cause.message : 'The calendar could not be loaded.') }
      }).finally(() => { if (!controller.signal.aborted) { setLoading(false) } })
    return () => controller.abort()
  }, [client, days, revision, isOnline])

  useEffect(() => {
    if (!data || !isOnline || error || saving) { return }
    const boundary = Date.parse(data.nextRefreshAtUtc)
    if (!Number.isFinite(boundary)) { return }
    const timer = window.setTimeout(refresh, Math.min(2_147_483_647, Math.max(1000, boundary - Date.now() + 100)))
    function onFocus() { if (Date.now() >= boundary) { refresh() } }
    window.addEventListener('focus', onFocus)
    return () => { window.clearTimeout(timer); window.removeEventListener('focus', onFocus) }
  }, [data, error, isOnline, saving, refresh])

  async function savePreferences(preferences: CalendarPreferences) {
    if (!navigator.onLine) { throw new Error('Reconnect to save calendar preferences.') }
    const saved = await client.updatePreferences(preferences)
    setSettings((current) => current ? { ...current, preferences: saved } : current)
    setData(null)
    setLoading(true)
    refresh()
  }

  async function changeCategory(key: CalendarCategorySetting, value: boolean) {
    if (!settings || saving) { return }
    const previous = settings.preferences
    const updated = { ...previous, [key]: value }
    setSettings({ ...settings, preferences: updated })
    setSaving(true)
    try { await savePreferences(updated); setError(null) }
    catch (cause) {
      setSettings((current) => current ? { ...current, preferences: previous } : current)
      setError(cause instanceof Error ? cause.message : 'Event filters could not be saved.')
    }
    finally { setSaving(false) }
  }

  function changeRange(value: CalendarRange) {
    if (value === days) { return }
    setDays(value); setData(null); setLoading(true)
  }
  function retry() { setLoading(true); refresh() }

  return <section className="calendar-page min-h-0 flex-1 overflow-y-auto overscroll-contain px-5 py-7 text-ink sm:px-8 sm:py-10 xl:px-10" aria-labelledby="calendar-title">
    <div className="mx-auto max-w-[82rem]">
      {onBackToConversation ? <button type="button" onClick={onBackToConversation} className="mb-4 inline-flex min-h-10 items-center gap-2 text-sm text-ink-soft hover:text-pomegranate"><ArrowLeft className="size-4" />Back to conversation</button> : null}
      <header className="flex flex-wrap items-start justify-between gap-5 border-b border-line pb-6">
        <div><h1 id="calendar-title" ref={heading} tabIndex={-1} className="font-display text-[clamp(2.5rem,4vw,3.6rem)] leading-[1.08] tracking-[-0.035em] outline-none">Jewish Calendar</h1><p className="mt-3 text-base leading-7 text-ink-soft">Dates, holidays, and the rhythm of the Jewish year.</p></div>
        {onOpenPersonalization ? <button type="button" onClick={onOpenPersonalization} className={ButtonClass}><Settings2 aria-hidden="true" className="size-4" />Edit location in Personalization</button> : null}
      </header>
      {!isOnline ? <p role="status" className="my-6 rounded-lg bg-stone p-4 text-sm">You’re offline. Reconnect for current calendar dates and local times. Saved Dvar Torah remains available from Learning & Tools.</p> : null}
      {error ? <div role="alert" className="my-6 flex flex-wrap items-center justify-between gap-3 rounded-lg bg-pomegranate/5 p-4 text-sm text-pomegranate"><p>{error}</p><button type="button" onClick={retry} disabled={!isOnline} className={ButtonClass}><RefreshCw className="size-4" />Retry calendar</button></div> : null}
      {loading && !data && isOnline ? <p role="status" className="py-10 text-muted">Loading calendar…</p> : null}
      {data ? <>
        <div className="grid items-start gap-7 border-b border-line py-7 xl:grid-cols-2">
          <section aria-labelledby="calendar-today"><h2 id="calendar-today" className="text-sm font-semibold">Today</h2><p className="mt-3 font-display text-3xl leading-tight">{formatCivilDate(data.today.gregorianDate, { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' })}</p>
            <div className="mt-3 flex flex-wrap gap-x-8 gap-y-2 font-display text-2xl"><span>{data.today.hebrewDate}</span><bdi dir="rtl" lang="he">{data.today.hebrewScript}</bdi></div>
            <p className="mt-5 flex items-start gap-2 text-sm text-ink-soft"><MapPin className="size-5 shrink-0" aria-hidden="true" /><span>{data.preferences.location?.label ?? 'No location selected'} · {data.preferences.inIsrael ? 'Israel' : 'Diaspora'}</span></p>
            <p className="mt-2 text-sm leading-6 text-muted">{data.today.isDaytimeOnly ? `Daytime Hebrew date (${data.today.timeZone}). ${data.preferences.location ? 'Sunset data is unavailable.' : 'Select a location for sunset-aware dates.'}` : `${data.today.isAfterSunset ? 'After local sunset — the Hebrew date has advanced.' : 'Hebrew date follows local sunset.'} ${data.today.timeZone}`}</p>
            {data.today.solarData?.isStale ? <AvailabilityNotice value={data.today.solarData} onRetry={retry} disabled={!isOnline} /> : null}
          </section>
          <details className="group/shabbat min-w-0 rounded-xl border border-line" aria-labelledby="calendar-shabbat">
            <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-4 rounded-xl p-5 [&::-webkit-details-marker]:hidden">
              <div className="min-w-0"><h2 id="calendar-shabbat" className="font-display text-2xl">This Shabbat</h2><p className="mt-2 text-sm text-ink-soft">{formatCivilDate(data.shabbat.shabbatDate)}</p><p className="mt-1 text-xs text-muted">Reading and local times</p></div>
              <ChevronDown className="size-5 shrink-0 text-muted transition group-open/shabbat:rotate-180 motion-reduce:transition-none" aria-hidden="true" />
            </summary>
            <div className="px-5 pb-5">
              <section className="border-t border-line pt-4"><p className="text-sm leading-6 text-ink-soft">{data.shabbat.parashah ? `Parashat ${data.shabbat.parashah}` : `Festival reading · ${data.shabbat.holiday ?? 'No regular weekly portion'}`}</p><p className="mt-1 text-xs text-muted">{data.shabbat.inIsrael ? 'Israel' : 'Diaspora'} reading cycle</p><button type="button" onClick={onOpenDvarTorah} className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg border border-pomegranate px-4 text-sm font-semibold text-pomegranate hover:bg-pomegranate/5"><BookOpenText className="size-4" />Read Dvar Torah</button><p className="mt-2 text-xs leading-5 text-muted">Browse our published teachings; the publication’s own date and cycle apply.</p></section>
              <section className="mt-6 border-t border-line pt-6"><h3 className="flex items-center gap-2 font-display text-2xl"><MapPin className="size-5" />Local times</h3><AvailabilityNotice value={data.timing} onRetry={data.preferences.location && data.preferences.showLocalTimes ? retry : undefined} disabled={!isOnline} />
                {data.localTimes.length ? <ol className="mt-3 space-y-4">{data.localTimes.map((time) => <li key={`${time.title}-${time.at}`} className="text-sm"><div className="flex items-baseline justify-between gap-2"><span className="font-semibold">{time.title}</span><time dateTime={time.at} className="whitespace-nowrap font-semibold">{new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit', timeZone: time.timeZone }).format(new Date(time.at))}</time></div><p className="mt-1 text-xs leading-5 text-muted">{formatCivilDate(time.date)} · {time.location}<br />{time.timeZone}{time.context ? ` · ${time.context}` : ''}</p></li>)}</ol> : onOpenPersonalization && !data.preferences.location ? <button type="button" onClick={onOpenPersonalization} className={`${ButtonClass} mt-4 w-full`}>Set location in Personalization</button> : null}
                {data.preferences.location ? <p className="mt-4 text-xs leading-5 text-muted">{data.timingConvention}</p> : null}
              </section>
              <p className="mt-6 border-t border-line pt-5 text-xs leading-6 text-muted">Calendar data by <a href="https://www.hebcal.com/home/developer-apis" target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-pomegranate underline">Hebcal<ExternalLink className="size-3" /></a> · <a href="https://creativecommons.org/licenses/by/4.0/" target="_blank" rel="noreferrer" className="underline">CC BY 4.0</a></p>
            </div>
          </details>
          {data.highlight ? <section aria-labelledby="calendar-highlight" className="rounded-xl bg-stone/80 p-5 sm:p-6 xl:col-span-2"><p className="flex items-center gap-2 text-sm text-ink-soft"><CalendarDays className="size-5 text-pomegranate" />{data.highlight.isOngoing ? 'Happening now' : 'Next holiday'}</p><h2 id="calendar-highlight" className="mt-2 font-display text-3xl">{data.highlight.title}</h2><p className="mt-3 text-base">{formatEventRange(data.highlight)}</p><p className="mt-1 text-sm text-muted">{formatBeginning(data.highlight)}</p><p className="mt-3 text-sm leading-6 text-ink-soft">{data.highlight.explanation}</p><button type="button" onClick={() => { const target = document.getElementById(`event-${data.highlight?.id}`); if (target instanceof HTMLDetailsElement) { target.open = true; target.scrollIntoView({ block: 'center', behavior: 'auto' }); target.querySelector('summary')?.focus() } }} className="mt-3 inline-flex min-h-11 items-center gap-2 text-sm font-semibold text-pomegranate">View dates & meaning<ArrowRight className="size-4" /></button></section> : <div className="text-sm text-muted xl:col-span-2">{data.holidays.isAvailable ? 'No upcoming events match your filters.' : 'Holiday information is temporarily unavailable.'}</div>}
        </div>
        <div className="mt-7">
          <section aria-labelledby="calendar-agenda" className="min-w-0">
            <div className="flex flex-wrap items-center justify-between gap-4"><h2 id="calendar-agenda" className="font-display text-3xl">Upcoming holidays</h2><div className="inline-flex max-w-full flex-wrap rounded-lg border border-line bg-stone p-1" role="group" aria-label="Agenda range">{([90, 30, 365] as const).map((value) => <button key={value} type="button" aria-pressed={days === value} onClick={() => changeRange(value)} className={`min-h-10 rounded-md px-3 text-sm ${days === value ? 'bg-paper font-semibold shadow-sm' : 'text-ink-soft hover:bg-paper/60'}`}>{value === 90 ? 'Next 90 days' : `${value} days`}</button>)}</div></div>
            {settings ? <fieldset disabled={saving || !isOnline} className="my-5 text-sm"><legend className="sr-only">Event categories</legend><div className="flex flex-wrap gap-x-5 gap-y-3">{CalendarCategories.slice(4).map(([key, label]) => <CategoryCheckbox key={key} label={label} checked={settings.preferences[key]} onChange={(value) => void changeCategory(key, value)} />)}</div><details className="mt-3"><summary className="min-h-8 cursor-pointer text-muted">More event filters</summary><div className="mt-2 flex flex-wrap gap-x-5 gap-y-3">{CalendarCategories.slice(0, 4).map(([key, label]) => <CategoryCheckbox key={key} label={label} checked={settings.preferences[key]} onChange={(value) => void changeCategory(key, value)} />)}</div></details></fieldset> : null}
            <AvailabilityNotice value={data.holidays} onRetry={retry} disabled={!isOnline} />
            <div>{data.events.map((event) => <AgendaEntry key={event.id} event={event} />)}</div>
            {data.events.length === 0 && data.holidays.isAvailable ? <p className="py-8 text-sm text-muted">No events in this range with the selected filters. Try a longer range or enable more categories.</p> : null}
          </section>
        </div>
      </> : null}
    </div>
  </section>
}

function CategoryCheckbox({ label, checked, onChange }: { label: string; checked: boolean; onChange(value: boolean): void }) {
  return <label className="flex min-h-8 items-center gap-2 text-ink-soft"><input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} className="size-5 accent-pomegranate" />{label}</label>
}

function AvailabilityNotice({ value, onRetry, disabled }: { value: CalendarAvailability; onRetry?: () => void; disabled: boolean }) {
  if (!value.message && !value.isStale) { return null }
  return <div role="status" className="my-3 text-sm leading-6 text-muted"><p>{value.message}</p>{value.isStale && value.fetchedAtUtc ? <p>Last updated {new Date(value.fetchedAtUtc).toLocaleString('en-US')}.</p> : null}{onRetry ? <button type="button" disabled={disabled} onClick={onRetry} className="mt-1 min-h-10 font-semibold text-pomegranate underline">Refresh data</button> : null}</div>
}

function AgendaEntry({ event }: { event: CalendarEvent }) {
  return <details id={`event-${event.id}`} className="group border-t border-line py-1">
    <summary className="flex cursor-pointer list-none items-center gap-4 py-4 [&::-webkit-details-marker]:hidden"><div aria-hidden="true" className="w-14 shrink-0 self-start pt-1 text-center"><p className="text-xs uppercase tracking-widest text-muted">{formatCivilDate(event.startDate, { month: 'short' })}</p><p className="font-display text-3xl">{formatCivilDate(event.startDate, { day: 'numeric' })}</p><p className="text-xs text-muted">{formatCivilDate(event.startDate, { year: 'numeric' })}</p></div><div className="min-w-0 flex-1"><h3 className="font-display text-2xl leading-tight">{event.title}{event.isOngoing ? <span className="ml-2 align-middle font-sans text-xs font-normal text-pomegranate">Happening now</span> : null}</h3><p className="mt-1 text-sm text-ink-soft">{formatEventRange(event)}</p><p className="mt-1 text-xs leading-5 text-muted">{formatBeginning(event)}</p></div><ChevronDown className="size-4 shrink-0 transition group-open:rotate-180" aria-hidden="true" /></summary>
    <div className="pb-5 pl-[4.5rem] text-sm leading-7 text-ink-soft"><p>{event.explanation}</p><ul className="my-3 space-y-1">{event.occurrences.map((occurrence) => <li key={`${occurrence.title}-${occurrence.date}`}><time dateTime={occurrence.date}>{formatCivilDate(occurrence.date)}</time> — {occurrence.title}</li>)}</ul></div>
  </details>
}
