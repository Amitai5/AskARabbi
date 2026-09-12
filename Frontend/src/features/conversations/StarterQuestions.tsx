import { ArrowUpRight } from 'lucide-react'
import type { CalendarClient } from '../calendar/calendarClient.ts'
import { useOfflineLearningLibrary } from '../pwa/offlineLearningContext.ts'

export function StarterQuestions({ client, disabled, onChoose }: { client: CalendarClient; disabled: boolean; onChoose(question: string): void }) {
  // Reuse the existing background preload; suggestions must not trigger extra downloads.
  const library = useOfflineLearningLibrary()
  const calendar = client.getCachedOverview?.(360)
  const now = new Date()
  const today = calendar?.today.gregorianDate ?? `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
  const holiday = (calendar?.events ?? library?.holidays?.events ?? []).find(event => event.endDate >= today && event.category === 'major')
  const questions = [
    holiday ? `What is ${holiday.title} about, and how is it observed?` : 'What is this week’s Torah portion about?',
    'Why do we light candles before Shabbat?',
    'Why is chicken with milk not kosher?',
    'What does the Shema mean?',
    'What does Judaism teach about helping someone in need?',
    'How can I find my bar or bat mitzvah Torah portion?',
  ]
  return <div className="mt-7 grid w-full max-w-[43rem] gap-2 text-left sm:grid-cols-2" role="group" aria-label="Questions to explore">
    {questions.map(question => <button key={question} type="button" disabled={disabled} onClick={() => onChoose(question)} className="group flex min-h-14 items-center justify-between gap-3 rounded-xl border border-line bg-paper/65 px-4 py-3 text-left text-sm leading-6 text-ink-soft transition hover:border-pomegranate/45 hover:bg-paper hover:text-pomegranate focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate disabled:opacity-50">
      <span>{question}</span><ArrowUpRight aria-hidden="true" className="size-4 shrink-0 text-brass" />
    </button>)}
  </div>
}
