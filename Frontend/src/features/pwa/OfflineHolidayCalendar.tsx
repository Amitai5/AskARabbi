import { useEffect, useState } from 'react'
import { HolidayAgenda } from '../calendar/HolidayAgenda.tsx'
import { AllCalendarFilters, type CalendarRange } from '../calendar/calendarTypes.ts'
import { formatCivilDate } from '../calendar/calendarFormatting.ts'
import { OfflineLibraryChanged, readOfflineLibrary, type SavedHolidayCalendar } from './offlineLibrary.ts'

export function OfflineHolidayCalendar() {
  const [saved, setSaved] = useState<SavedHolidayCalendar | null>(null)
  const [loading, setLoading] = useState(true)
  const [days, setDays] = useState<CalendarRange>(90)
  const [filters, setFilters] = useState({ ...AllCalendarFilters })
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    const refresh = () => setRevision(value => value + 1)
    window.addEventListener(OfflineLibraryChanged, refresh)
    window.addEventListener('focus', refresh)
    return () => { window.removeEventListener(OfflineLibraryChanged, refresh); window.removeEventListener('focus', refresh) }
  }, [])
  useEffect(() => {
    let active = true
    void readOfflineLibrary().then(library => { if (active) { setSaved(library.holidays?.version === 1 ? library.holidays : null) } })
      .catch(() => { if (active) { setSaved(null) } })
      .finally(() => { if (active) { setLoading(false) } })
    return () => { active = false }
  }, [revision])

  const now = new Date()
  const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
  return <div>
    <p role="status" className="mb-6 rounded-lg bg-stone p-4 text-sm leading-6">{loading ? 'Opening saved holidays…' : saved ? `Saved holidays · ${saved.inIsrael ? 'Israel' : 'Diaspora'} · Available through ${formatCivilDate(saved.endDate)}. Reconnect for current calendar dates and local times. The list uses this device’s date.` : 'No holidays saved on this device yet. Sign in on a good connection to save the next 360 days. Reconnect for current calendar dates and local times.'}</p>
    {saved ? <>
      {today > saved.endDate || today < saved.startDate ? <p role="status" className="mb-4 text-sm text-pomegranate">This saved schedule does not cover today. Reconnect on a good connection to update it.</p> : null}
      <HolidayAgenda days={days} onRange={setDays} filters={filters} onFilters={setFilters} events={saved.events} startDate={today} />
      <p className="mt-5 text-xs leading-6 text-muted">Saved {new Date(saved.savedAt).toLocaleDateString()}. Local candle-lighting and sunset times require a connection. Holiday data: <a href="https://www.hebcal.com/home/developer-apis" target="_blank" rel="noreferrer" className="text-pomegranate underline">Hebcal</a> · <a href="https://creativecommons.org/licenses/by/4.0/" target="_blank" rel="noreferrer" className="underline">CC BY 4.0</a> (links need a connection).</p>
    </> : null}
  </div>
}
