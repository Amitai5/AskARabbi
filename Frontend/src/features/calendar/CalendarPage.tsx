import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ArrowLeft, BookOpenText, CalendarDays, ChevronDown, ExternalLink, MapPin, RefreshCw, Settings2 } from 'lucide-react'
import type { CalendarClient } from './calendarClient.ts'
import { AllCalendarFilters, type CalendarAvailability, type CalendarFilters, type CalendarOverview, type CalendarPreferencesResponse, type CalendarRange } from './calendarTypes.ts'
import { formatBeginning, formatCivilDate, formatEventRange } from './calendarFormatting.ts'
import { useOnlineStatus } from '../pwa/useOnlineStatus.ts'
import { HolidayAgenda } from './HolidayAgenda.tsx'
import { HolidayDetails } from './HolidayDetails.tsx'
import { selectCalendarEvents } from './calendarAgenda.ts'
import { OfflineHolidayCalendar } from '../pwa/OfflineHolidayCalendar.tsx'
import { PrintAction } from '../printing/PrintAction.tsx'
import './calendar.css'

interface Props { client: CalendarClient; onOpenDvarTorah(): void; onBackToConversation?(): void; onOpenPersonalization?(): void; initialDays?: CalendarRange; initialSearch?: string; onNavigate?(days: CalendarRange, search: string): void; onAsk?(question: string): void; isAskDisabled?: boolean }
const ButtonClass = 'inline-flex min-h-11 items-center justify-center gap-2 rounded-lg border border-line-strong bg-paper px-4 text-sm font-semibold text-ink transition hover:bg-stone disabled:opacity-50'

export function CalendarPage({ client, onOpenDvarTorah, onBackToConversation, onOpenPersonalization, initialDays = 90, initialSearch = '', onNavigate, onAsk, isAskDisabled }: Props) {
  const [days, setDays] = useState<CalendarRange>(initialDays)
  const [search, setSearch] = useState(initialSearch)
  const [overview, setData] = useState<CalendarOverview | null>(() => client.getCachedOverview?.(360) ?? null)
  const [settings, setSettings] = useState<CalendarPreferencesResponse | null>(() => { const cached = client.getCachedOverview?.(360); return cached ? { cities: [], preferences: cached.preferences } : null })
  const data = useMemo(() => {
    if (!overview) { return null }
    const events = selectCalendarEvents(overview.events, overview.today.gregorianDate, 360, settings?.preferences ?? AllCalendarFilters)
    return { ...overview, events, highlight: events.find(event => event.isOngoing) ?? events[0] ?? null }
  }, [overview, settings])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [revision, setRevision] = useState(0)
  const [saving, setSaving] = useState(false)
  const heading = useRef<HTMLHeadingElement>(null)
  const isOnline = useOnlineStatus()
  const refresh = useCallback(() => setRevision((value) => value + 1), [])
  useEffect(() => { heading.current?.focus({ preventScroll: true }) }, [])

  useEffect(() => {
    if (!isOnline) { return }
    const controller = new AbortController()
    void client.getOverview(360, controller.signal)
      .then((overview) => {
        if (controller.signal.aborted) { return }
        setData(overview)
        setSettings({ cities: [], preferences: overview.preferences })
        setError(null)
      }).catch((cause: unknown) => {
        if (!controller.signal.aborted) { setError(cause instanceof Error ? cause.message : 'The calendar could not be loaded.') }
      }).finally(() => { if (!controller.signal.aborted) { setLoading(false) } })
    return () => controller.abort()
  }, [client, revision, isOnline])

  useEffect(() => {
    if (!data || !isOnline || error || saving) { return }
    const boundary = Date.parse(data.nextRefreshAtUtc)
    if (!Number.isFinite(boundary)) { return }
    const timer = window.setTimeout(refresh, Math.min(2_147_483_647, Math.max(1000, boundary - Date.now() + 100)))
    function onFocus() { if (Date.now() >= boundary) { refresh() } }
    window.addEventListener('focus', onFocus)
    return () => { window.clearTimeout(timer); window.removeEventListener('focus', onFocus) }
  }, [data, error, isOnline, saving, refresh])

  async function changeFilters(filters: CalendarFilters) {
    if (!settings || saving) { return }
    const previous = settings.preferences
    const updated = { ...previous, ...filters }
    setSettings({ ...settings, preferences: updated })
    setSaving(true)
    try {
      if (!navigator.onLine) { throw new Error('Reconnect to save calendar preferences.') }
      const saved = await client.updatePreferences(updated)
      setSettings(current => current ? { ...current, preferences: saved } : current)
      setData(current => current ? { ...current, preferences: saved } : current)
      setError(null)
    }
    catch (cause) {
      setSettings((current) => current ? { ...current, preferences: previous } : current)
      setError(cause instanceof Error ? cause.message : 'Event filters could not be saved.')
    }
    finally { setSaving(false) }
  }

  function retry() { client.invalidate?.(); setLoading(true); refresh() }

  return <section className="calendar-page min-h-0 flex-1 overflow-y-auto overscroll-contain px-5 py-7 text-ink sm:px-8 sm:py-10 xl:px-10" aria-labelledby="calendar-title">
    <div className="mx-auto max-w-[82rem]">
      {onBackToConversation ? <button type="button" onClick={onBackToConversation} className="mb-4 inline-flex min-h-10 items-center gap-2 text-sm text-ink-soft hover:text-pomegranate"><ArrowLeft className="size-4" />Back to conversation</button> : null}
      <header className="flex flex-wrap items-start justify-between gap-5 border-b border-line pb-6">
        <div><h1 id="calendar-title" ref={heading} tabIndex={-1} className="font-display text-[clamp(2.5rem,4vw,3.6rem)] leading-[1.08] tracking-[-0.035em] outline-none">Jewish Calendar</h1><p className="mt-3 text-base leading-7 text-ink-soft">Dates, holidays, and the rhythm of the Jewish year.</p></div>
        <div className="flex flex-wrap items-center gap-3">{data && isOnline ? <PrintAction label="Print calendar" getRequest={() => ({ kind: 'calendar', calendar: { startDate: data.today.gregorianDate, days, events: data.events, filters: settings?.preferences ?? AllCalendarFilters, search, inIsrael: data.preferences.inIsrael, overview: data } })} /> : null}{onOpenPersonalization ? <button type="button" onClick={onOpenPersonalization} className={ButtonClass}><Settings2 aria-hidden="true" className="size-4" />Edit location in Personalization</button> : null}</div>
      </header>
      {!isOnline ? <div className="mt-7"><OfflineHolidayCalendar /></div> : null}
      {error ? <div role="alert" className="my-6 flex flex-wrap items-center justify-between gap-3 rounded-lg bg-pomegranate/5 p-4 text-sm text-pomegranate"><p>{error}</p><button type="button" onClick={retry} disabled={!isOnline} className={ButtonClass}><RefreshCw className="size-4" />Retry calendar</button></div> : null}
      {loading && !data && isOnline ? <p role="status" className="py-10 text-muted">Loading calendar…</p> : null}
      {data && isOnline ? <>
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
          {data.highlight ? <section aria-labelledby="calendar-highlight" className="rounded-xl bg-stone/80 p-5 sm:p-6 xl:col-span-2"><p className="flex items-center gap-2 text-sm text-ink-soft"><CalendarDays className="size-5 text-pomegranate" />{data.highlight.isOngoing ? 'Happening now' : 'Next holiday'}</p><h2 id="calendar-highlight" className="mt-2 font-display text-3xl">{data.highlight.title}</h2><p className="mt-3 text-base">{formatEventRange(data.highlight)}</p><p className="mt-1 text-sm text-muted">{formatBeginning(data.highlight)}</p><div className="mt-3"><HolidayDetails event={data.highlight} onAsk={onAsk} isAskDisabled={isAskDisabled} /></div></section> : <div className="text-sm text-muted xl:col-span-2">{data.holidays.isAvailable ? 'No upcoming events match your filters.' : 'Holiday information is temporarily unavailable.'}</div>}
        </div>
        <div className="mt-7">
          <HolidayAgenda days={days} onRange={value => { setDays(value); onNavigate?.(value, search) }} initialSearch={initialSearch} onSearch={value => { setSearch(value); onNavigate?.(days, value) }} onAsk={onAsk} isAskDisabled={isAskDisabled} events={data.events} startDate={data.today.gregorianDate} filters={settings?.preferences ?? AllCalendarFilters} onFilters={filters => void changeFilters(filters)} disabled={saving || !settings} isAvailable={data.holidays.isAvailable} notice={<AvailabilityNotice value={data.holidays} onRetry={retry} disabled={!isOnline} />} />
        </div>
      </> : null}
    </div>
  </section>
}

function AvailabilityNotice({ value, onRetry, disabled }: { value: CalendarAvailability; onRetry?: () => void; disabled: boolean }) {
  if (!value.message && !value.isStale) { return null }
  return <div role="status" className="my-3 text-sm leading-6 text-muted"><p>{value.message}</p>{value.isStale && value.fetchedAtUtc ? <p>Last updated {new Date(value.fetchedAtUtc).toLocaleString('en-US')}.</p> : null}{onRetry ? <button type="button" disabled={disabled} onClick={onRetry} className="mt-1 min-h-10 font-semibold text-pomegranate underline">Refresh data</button> : null}</div>
}
