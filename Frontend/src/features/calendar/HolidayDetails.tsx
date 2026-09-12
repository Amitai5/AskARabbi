import type { CalendarEvent } from './calendarTypes.ts'
import { formatCivilDate } from './calendarFormatting.ts'
import { MessageCircle } from 'lucide-react'
import { holidayQuestion } from '../conversations/learningQuestions.ts'

export function HolidayDetails({ event, onAsk, isAskDisabled }: { event: CalendarEvent; onAsk?(question: string): void; isAskDisabled?: boolean }) {
  return <div className="text-sm leading-7 text-ink-soft">
    <p>{event.explanation}</p>
    {event.occurrences.length > 0 ? <ul aria-label="Holiday dates" className="mt-3 space-y-1">{event.occurrences.map(occurrence => <li key={`${occurrence.title}-${occurrence.date}`}><time dateTime={occurrence.date}>{formatCivilDate(occurrence.date)}</time> — {occurrence.title}</li>)}</ul> : null}
    {onAsk ? <button type="button" aria-label={`Ask about this: ${event.title}`} disabled={isAskDisabled} onClick={() => onAsk(holidayQuestion(event))} className="mt-3 inline-flex min-h-[44px] items-center gap-2 rounded-lg border border-line-strong bg-paper px-4 font-semibold text-pomegranate hover:bg-stone disabled:opacity-50"><MessageCircle aria-hidden="true" className="size-4" />Ask about this</button> : null}
  </div>
}
