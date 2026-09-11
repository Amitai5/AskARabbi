import type { CalendarEvent } from './calendarTypes.ts'
import { formatCivilDate } from './calendarFormatting.ts'

export function HolidayDetails({ event }: { event: CalendarEvent }) {
  return <div className="text-sm leading-7 text-ink-soft">
    <p>{event.explanation}</p>
    {event.occurrences.length > 0 ? <ul aria-label="Holiday dates" className="mt-3 space-y-1">{event.occurrences.map(occurrence => <li key={`${occurrence.title}-${occurrence.date}`}><time dateTime={occurrence.date}>{formatCivilDate(occurrence.date)}</time> — {occurrence.title}</li>)}</ul> : null}
  </div>
}
