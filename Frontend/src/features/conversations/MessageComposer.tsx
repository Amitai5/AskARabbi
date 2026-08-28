import { useRef, type FormEvent, type KeyboardEvent } from 'react'
import { ArrowUp } from 'lucide-react'
import { SourceFilterMenu } from './SourceFilterMenu.tsx'

interface MessageComposerProps {
  draft: string
  selectedSourceKeys: readonly string[]
  conversationLanguage: string
  quotationLanguage: string
  isSending: boolean
  onDraftChange(value: string): void
  onSelectedSourceKeysChange(sourceKeys: string[]): void
  onSubmit(): void
}

export function MessageComposer({ draft, selectedSourceKeys, conversationLanguage, quotationLanguage, isSending, onDraftChange, onSelectedSourceKeysChange, onSubmit }: MessageComposerProps) {
  const formRef = useRef<HTMLFormElement>(null)

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (draft.trim().length > 0 && selectedSourceKeys.length > 0) {
      onSubmit()
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      formRef.current?.requestSubmit()
    }
  }

  return (
    <div className="w-full max-w-[58rem]">
      <form ref={formRef} onSubmit={handleSubmit} className="rounded-2xl border border-line-strong bg-paper p-3 shadow-[0_10px_30px_rgb(16_35_63_/_0.06)] transition focus-within:border-pomegranate focus-within:ring-3 focus-within:ring-pomegranate/10">
        <label htmlFor="message" className="sr-only">Message AskRabbi</label>
        <textarea
          id="message"
          value={draft}
          onChange={(event) => onDraftChange(event.target.value)}
          onKeyDown={handleKeyDown}
          rows={3}
          maxLength={4000}
          placeholder="Ask about Jewish texts, traditions, or practice…"
          className="max-h-52 min-h-20 w-full resize-none bg-transparent px-2 pt-1 text-[0.98rem] leading-6 text-ink outline-none placeholder:text-muted/80"
        />
        <div className="flex flex-wrap items-center justify-between gap-2 pt-2">
          <div className="flex min-w-0 items-center gap-2">
            <SourceFilterMenu selectedSourceKeys={selectedSourceKeys} onChange={onSelectedSourceKeysChange} />
            <span className="hidden truncate text-xs text-muted sm:inline">{conversationLanguage} · quotes in {quotationLanguage}</span>
          </div>
          <button type="submit" disabled={isSending || draft.trim().length === 0 || selectedSourceKeys.length === 0} className="flex size-10 items-center justify-center rounded-full bg-pomegranate text-white transition hover:bg-pomegranate-dark disabled:cursor-not-allowed disabled:bg-stone-deep disabled:text-muted" aria-label="Send message">
            <ArrowUp aria-hidden="true" className="size-5" strokeWidth={1.9} />
          </button>
        </div>
        {selectedSourceKeys.length === 0 ? <p className="px-2 pt-2 text-xs font-medium text-pomegranate" role="alert">Select at least one source before sending.</p> : null}
      </form>
      <p className="mt-3 text-center text-xs leading-5 text-muted">
        AskRabbi can make mistakes. Check the cited sources.
      </p>
    </div>
  )
}
