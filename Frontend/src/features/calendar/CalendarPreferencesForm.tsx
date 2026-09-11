import { useEffect, useRef, useState, type FormEvent } from 'react'
import { MapPin, Save, X } from 'lucide-react'
import type { CalendarLocation, CalendarPreferences } from './calendarTypes.ts'

const ControlClass = 'mt-2 min-h-11 w-full rounded-lg border border-line-strong bg-paper px-3 py-2 text-base text-ink'

interface Props {
  preferences: CalendarPreferences
  cities: CalendarLocation[]
  onSave(preferences: CalendarPreferences): Promise<void>
  onClose(): void
}

export function CalendarPreferencesForm({ preferences, cities, onSave, onClose }: Props) {
  const [draft, setDraft] = useState(preferences)
  const [mode, setMode] = useState(preferences.location?.kind ?? 'city')
  const [query, setQuery] = useState('')
  const [zip, setZip] = useState(preferences.location?.kind === 'zip' ? preferences.location.id : '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const heading = useRef<HTMLHeadingElement>(null)
  useEffect(() => { heading.current?.focus() }, [])
  const matching = cities.filter((city) => city.label.toLowerCase().includes(query.toLowerCase()))
  const visibleCities = draft.location?.kind === 'city' && !matching.some((city) => city.id === draft.location?.id)
    ? [...matching, draft.location] : matching

  async function submit(event: FormEvent) {
    event.preventDefault()
    const location = mode === 'zip' ? { kind: 'zip' as const, id: zip, label: zip, timeZone: 'UTC', defaultCandleLightingMinutes: 18 } : draft.location?.kind === 'city' ? draft.location : null
    setSaving(true)
    setError(null)
    try {
      await onSave({ ...draft, location })
      onClose()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Calendar preferences could not be saved. Please try again.')
    } finally {
      setSaving(false)
    }
  }

  return <section aria-labelledby="calendar-preferences-title" className="my-7 rounded-xl border border-line bg-stone/50 p-5 sm:p-7">
    <div className="flex items-center justify-between gap-4">
      <h2 id="calendar-preferences-title" ref={heading} tabIndex={-1} className="font-display text-2xl text-ink outline-none">Location & preferences</h2>
      <button type="button" onClick={onClose} disabled={saving} className="flex size-11 items-center justify-center rounded-lg hover:bg-stone-deep" aria-label="Close calendar preferences"><X className="size-5" /></button>
    </div>
    <p className="mt-2 text-sm leading-6 text-muted">Choose where you are now. Your birth location is not used. Only calendar parameters are sent to Hebcal.</p>
    <form onSubmit={(event) => void submit(event)} className="mt-5 space-y-6">
      <fieldset disabled={saving} className="grid gap-5 sm:grid-cols-2">
        <legend className="sr-only">Current location and timing</legend>
        <label className="text-sm font-semibold">Location type<select className={ControlClass} value={mode} onChange={(event) => { setMode(event.target.value as 'city' | 'zip'); setDraft({ ...draft, location: null, candleLightingMinutes: null }) }}><option value="city">Supported city</option><option value="zip">U.S. ZIP code</option></select></label>
        {mode === 'city' ? <div>
          <label className="text-sm font-semibold">Search supported cities<input type="search" className={ControlClass} value={query} onChange={(event) => setQuery(event.target.value)} placeholder="City or country" /></label>
          <label className="mt-3 block text-sm font-semibold">City<select className={ControlClass} value={draft.location?.kind === 'city' ? draft.location.id : ''} onChange={(event) => setDraft({ ...draft, location: cities.find((city) => city.id === event.target.value) ?? null, candleLightingMinutes: null })}><option value="">No location (dates only)</option>{visibleCities.map((city) => <option key={city.id} value={city.id}>{city.label}</option>)}</select></label>
          {matching.length === 0 ? <p className="mt-2 text-sm text-muted">No matching city. U.S. locations can also use a ZIP code.</p> : null}
        </div> : <label className="text-sm font-semibold">U.S. ZIP code<input className={ControlClass} type="text" inputMode="numeric" autoComplete="postal-code" required pattern="[0-9]{5}" maxLength={5} value={zip} onChange={(event) => setZip(event.target.value)} placeholder="90210" /><span className="mt-2 block text-sm font-normal text-muted">Timezone is resolved when you save.</span></label>}
        <label className="text-sm font-semibold">Holiday and reading schedule<select className={ControlClass} value={draft.inIsrael ? 'israel' : 'diaspora'} onChange={(event) => setDraft({ ...draft, inIsrael: event.target.value === 'israel' })}><option value="diaspora">Diaspora</option><option value="israel">Israel</option></select></label>
        <label className="flex items-center gap-3 self-center text-sm"><input type="checkbox" className="size-5 accent-pomegranate" checked={draft.showLocalTimes} onChange={(event) => setDraft({ ...draft, showLocalTimes: event.target.checked })} />Show local candle-lighting and Havdalah times</label>
        <label className="text-sm font-semibold">Candle-lighting minutes before sunset<input type="number" min={1} max={90} className={ControlClass} placeholder={`Location default (${draft.location?.defaultCandleLightingMinutes ?? 18})`} value={draft.candleLightingMinutes ?? ''} onChange={(event) => setDraft({ ...draft, candleLightingMinutes: event.target.value === '' ? null : Number(event.target.value) })} /><span className="mt-2 block text-sm font-normal leading-6 text-muted">Leave blank for the location default: usually 18 minutes; Jerusalem 40, Haifa 30. Later festival lighting follows the festival transition.</span></label>
        <div><label className="text-sm font-semibold">Havdalah calculation<select className={ControlClass} value={draft.havdalah === 'nightfall' ? 'nightfall' : String(draft.havdalahMinutes)} onChange={(event) => setDraft({ ...draft, havdalah: event.target.value === 'nightfall' ? 'nightfall' : 'fixed', havdalahMinutes: event.target.value === 'nightfall' ? 42 : Number(event.target.value) })}><option value="nightfall">Nightfall — sun 8.5° below horizon</option><option value="42">42 minutes after sunset</option><option value="50">50 minutes after sunset</option><option value="72">72 minutes after sunset</option></select></label><p className="mt-2 text-sm leading-6 text-muted">Choose your community’s convention. Calculations do not use elevation.</p></div>
      </fieldset>
      {draft.location?.kind === 'city' ? <p className="flex items-center gap-2 text-sm text-muted"><MapPin className="size-4" />{draft.location.timeZone}</p> : null}
      {error ? <p role="alert" className="text-sm text-pomegranate">{error}</p> : null}
      <div className="flex justify-end gap-3"><button type="button" onClick={onClose} disabled={saving} className="min-h-11 rounded-lg px-4 text-sm hover:bg-stone-deep">Cancel</button><button type="submit" disabled={saving} className="flex min-h-11 items-center gap-2 rounded-lg bg-pomegranate px-5 text-sm font-semibold text-white disabled:opacity-60"><Save className="size-4" />{saving ? 'Saving…' : 'Save calendar preferences'}</button></div>
    </form>
  </section>
}
