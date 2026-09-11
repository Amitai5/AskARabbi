import { useEffect, useState } from 'react'
import type { CalendarLocation } from '../calendar/calendarTypes.ts'
import type { ConversationSettingsClient } from './conversationSettingsClient.ts'
import type { PersonalizationLocation, PersonalizationProfile } from './personalizationTypes.ts'
import type { PersonalizationErrors } from './personalizationValidation.ts'

interface Props {
  profile: PersonalizationProfile
  errors: PersonalizationErrors
  client: ConversationSettingsClient
  onChange(field: 'birthLocation' | 'currentLocation', value: PersonalizationLocation): void
}

const ControlClass = 'mt-2 min-h-12 w-full min-w-0 rounded-lg border border-line-strong bg-paper px-3.5 py-2 text-base text-ink shadow-sm focus:border-pomegranate focus:outline-none focus:ring-2 focus:ring-pomegranate/15 sm:text-lg'

export function PersonalizationLocationFields({ profile, errors, client, onChange }: Props) {
  const [cities, setCities] = useState<CalendarLocation[]>([])
  const [loadError, setLoadError] = useState(false)
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    void client.getLocations(controller.signal).then((value) => {
      if (!controller.signal.aborted) { setCities(value); setLoadError(false) }
    }).catch(() => { if (!controller.signal.aborted) { setLoadError(true) } })
    return () => controller.abort()
  }, [client, revision])

  return <div className="space-y-7">
    <LocationField title="Current location" prefix="current-location" value={profile.currentLocation} error={errors.currentLocation} cities={cities} onChange={(value) => onChange('currentLocation', value)} description="Used for today’s date, local times, and the Israel or Diaspora reading schedule in the calendar and chat." />
    <div className="border-t border-line pt-6">
      <LocationField title="Birthplace" prefix="birth-location" value={profile.birthLocation} error={errors.birthLocation} cities={cities} onChange={(value) => onChange('birthLocation', value)} description="Used with your birth date and time for Hebrew birthdays and bar or bat mitzvah calculations. It stays separate if you move." />
      <button type="button" disabled={!profile.currentLocation?.id} onClick={() => { if (profile.currentLocation) { onChange('birthLocation', { ...profile.currentLocation }) } }} className="mt-3 min-h-11 rounded-lg px-2 font-semibold text-pomegranate underline disabled:opacity-50">Use current location as birthplace</button>
    </div>
    {loadError ? <p role="status" className="text-sm text-pomegranate">The city list could not be loaded. You can still enter a U.S. ZIP code. <button type="button" onClick={() => setRevision((value) => value + 1)} className="min-h-11 font-semibold underline">Retry city list</button></p> : null}
    <p className="border-l-2 border-brass bg-stone/55 px-4 py-3 text-sm leading-6 text-ink-soft sm:text-base">Time zones are resolved when you save. Only ZIP codes or city identifiers are sent to Hebcal for location lookup; your name, birth date, and conversations are not sent.</p>
  </div>
}

interface FieldProps {
  title: string
  prefix: string
  value: PersonalizationLocation | null | undefined
  error?: string
  description: string
  cities: CalendarLocation[]
  onChange(value: PersonalizationLocation): void
}

function LocationField({ title, prefix, value, error, description, cities, onChange }: FieldProps) {
  const kind = value?.kind ?? 'zip'
  const [query, setQuery] = useState('')
  const matching = cities.filter((city) => city.label.toLowerCase().includes(query.toLowerCase()) || city.id === value?.id)
  return <fieldset className="min-w-0" aria-describedby={`${prefix}-description`}>
    <legend className="font-semibold text-ink">{title}</legend>
    <p id={`${prefix}-description`} className="mt-1 text-sm leading-6 text-muted sm:text-base">{description}</p>
    <div className="mt-4 grid min-w-0 gap-4 sm:grid-cols-2">
      <label className="min-w-0 font-medium">Location type<select aria-label={`${title} location type`} className={ControlClass} value={kind} onChange={(event) => onChange({ kind: event.target.value as 'city' | 'zip', id: '' })}><option value="zip">U.S. ZIP code</option><option value="city">Supported city</option></select></label>
      {kind === 'zip' ? <label className="min-w-0 font-medium">U.S. ZIP code<input aria-label={`${title} U.S. ZIP code`} className={ControlClass} type="text" inputMode="numeric" autoComplete={prefix === 'current-location' ? 'postal-code' : 'off'} required maxLength={5} pattern="[0-9]{5}" placeholder="91302" value={value?.id ?? ''} onChange={(event) => onChange({ kind: 'zip', id: event.target.value })} aria-invalid={Boolean(error)} aria-describedby={error ? `${prefix}-error` : undefined} /></label>
        : <div className="min-w-0"><label className="font-medium">Search cities<input aria-label={`${title} search cities`} type="search" className={ControlClass} placeholder="City or country" value={query} onChange={(event) => setQuery(event.target.value)} /></label><label className="mt-3 block font-medium">City<select aria-label={`${title} city`} required className={ControlClass} value={value?.id ?? ''} onChange={(event) => onChange(cities.find((city) => city.id === event.target.value) ?? { kind: 'city', id: '' })} aria-invalid={Boolean(error)} aria-describedby={error ? `${prefix}-error` : undefined}><option value="">Choose a city</option>{value?.id && !cities.some((city) => city.id === value.id) ? <option value={value.id}>{value.label ?? value.id}</option> : null}{matching.map((city) => <option key={city.id} value={city.id}>{city.label}</option>)}</select></label></div>}
    </div>
    {value?.timeZone ? <p className="mt-2 break-words text-sm leading-6 text-muted">{value.label} · {value.timeZone}</p> : null}
    {error ? <p id={`${prefix}-error`} role="alert" className="mt-2 font-medium text-pomegranate">{error}</p> : null}
  </fieldset>
}
