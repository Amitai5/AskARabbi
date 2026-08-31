import { BookOpenText } from 'lucide-react'

interface AnswerProgressProps {
  sourceDescription: string
}

export function AnswerProgress({ sourceDescription }: AnswerProgressProps) {
  return (
    <div className="border-l-2 border-brass pl-5">
      <p className="mb-2 font-display text-xl text-ink">AskRabbi</p>
      <div className="flex items-start gap-3 text-ink-soft" role="status" aria-live="polite">
        <span className="mt-1 inline-flex size-7 shrink-0 items-center justify-center rounded-full bg-brass/15 motion-safe:animate-pulse">
          <BookOpenText aria-hidden="true" className="size-4 text-brass-dark" strokeWidth={1.8} />
        </span>
        <div>
          <p className="leading-7">Finding relevant passages in {sourceDescription.toLowerCase()}, checking the quotations, and preparing a grounded answer…</p>
          <span className="mt-2 inline-flex gap-1" aria-hidden="true" data-testid="answer-progress-dots">
            <span className="size-1.5 rounded-full bg-pomegranate motion-safe:animate-bounce" style={{ animationDelay: '-0.3s' }} />
            <span className="size-1.5 rounded-full bg-pomegranate motion-safe:animate-bounce" style={{ animationDelay: '-0.15s' }} />
            <span className="size-1.5 rounded-full bg-pomegranate motion-safe:animate-bounce" />
          </span>
        </div>
      </div>
    </div>
  )
}
