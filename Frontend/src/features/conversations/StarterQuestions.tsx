import { ArrowUpRight } from 'lucide-react'

const Questions = [
  'Why do we light candles before Shabbat?',
  'Why is chicken with milk not kosher?',
  'What does the Shema mean?',
] as const

export function StarterQuestions({ disabled, onChoose }: { disabled: boolean; onChoose(question: string): void }) {
  return <div className="mt-7 grid w-full max-w-[43rem] gap-2 text-left sm:grid-cols-3" role="group" aria-label="Questions to explore">
    {Questions.map(question => <button key={question} type="button" disabled={disabled} onClick={() => onChoose(question)} className="group flex min-h-14 items-center justify-between gap-3 rounded-xl border border-line bg-paper/65 px-4 py-3 text-left text-sm leading-6 text-ink-soft transition hover:border-pomegranate/45 hover:bg-paper hover:text-pomegranate focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate disabled:opacity-50">
      <span>{question}</span><ArrowUpRight aria-hidden="true" className="size-4 shrink-0 text-brass" />
    </button>)}
  </div>
}
