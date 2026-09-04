import { Check, Copy } from 'lucide-react'
import { memo, useEffect, useMemo, useState } from 'react'
import { normalizeDisplayText } from '../../displayText.ts'
import type { ConversationMessage, ConversationSource } from './conversationData.ts'

interface AssistantMessageProps {
  message: ConversationMessage
  selectedSourceNumber: number | null
  onSelectSource(messageId: string, sourceNumber: number, trigger: HTMLButtonElement): void
}

const EmptySources: readonly ConversationSource[] = []
type CopyStatus = 'idle' | 'copied' | 'failed'

export const AssistantMessage = memo(function AssistantMessage({ message, selectedSourceNumber, onSelectSource }: AssistantMessageProps) {
  const sources = message.sources ?? EmptySources
  const sourceNumbers = useMemo(() => new Set(sources.map((source) => source.number)), [sources])
  const normalizedContent = useMemo(() => normalizeDisplayText(message.content), [message.content])
  const [copyStatus, setCopyStatus] = useState<CopyStatus>('idle')

  useEffect(() => {
    if (copyStatus === 'idle') {
      return
    }

    const resetTimeout = window.setTimeout(() => setCopyStatus('idle'), 2_000)
    return () => window.clearTimeout(resetTimeout)
  }, [copyStatus])

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(normalizedContent)
      setCopyStatus('copied')
    } catch {
      setCopyStatus('failed')
    }
  }

  const copyLabel = copyStatus === 'copied' ? 'Answer copied' : copyStatus === 'failed' ? 'Copy failed. Try again' : 'Copy answer'
  const copyVisibilityClass = copyStatus === 'idle'
    ? 'opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 max-sm:opacity-100'
    : 'opacity-100'

  return (
    <div className="conversation-message group relative border-l-2 border-pomegranate pl-5" data-message-role="assistant">
      <p className="mb-3 font-display text-xl text-ink">AskRabbi</p>
      <div className="space-y-4 text-base leading-7 text-ink sm:text-lg">
        {normalizedContent.trim().split(/\n\s*\n/).map((paragraph, index) => (
          <p key={`${message.id}-paragraph-${index}`} className="last:min-h-9 last:pr-12">{renderParagraph(paragraph, sourceNumbers, message.id, selectedSourceNumber, onSelectSource)}</p>
        ))}
      </div>
      <div className="absolute bottom-0 right-0 flex p-0.5">
        <button type="button" aria-label={copyLabel} title={copyLabel} onClick={() => void handleCopy()} className={`answer-copy-button inline-flex size-8 items-center justify-center rounded-md border border-line bg-paper text-muted shadow-sm transition hover:border-line-strong hover:bg-stone hover:text-ink focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-pomegranate/55 motion-reduce:transition-none ${copyVisibilityClass}`}>
          {copyStatus === 'copied' ? <Check aria-hidden="true" className="size-4 text-pomegranate" strokeWidth={1.8} /> : <Copy aria-hidden="true" className="size-4" strokeWidth={1.8} />}
        </button>
      </div>
      <span className="sr-only" role="status" aria-live="polite">{copyStatus === 'copied' ? 'Answer copied to clipboard.' : copyStatus === 'failed' ? 'The answer could not be copied. Try again.' : ''}</span>
    </div>
  )
})

function renderParagraph(paragraph: string, sourceNumbers: ReadonlySet<number>, messageId: string, selectedSourceNumber: number | null, onSelectSource: AssistantMessageProps['onSelectSource']) {
  return paragraph.split(/(\[\d+\])/g).map((part, index) => {
    const match = /^\[(\d+)\]$/.exec(part)
    const sourceNumber = match === null ? Number.NaN : Number(match[1])
    if (!sourceNumbers.has(sourceNumber)) {
      return part
    }

    const isSelected = sourceNumber === selectedSourceNumber
    return (
      <button key={`${messageId}-citation-${sourceNumber}-${index}`} type="button" aria-label={`View source ${sourceNumber}`} aria-expanded={isSelected} onClick={(event) => onSelectSource(messageId, sourceNumber, event.currentTarget)} className={`relative mx-0.5 inline-flex rounded-sm px-0.5 font-semibold text-pomegranate underline decoration-pomegranate/35 underline-offset-4 transition hover:bg-pomegranate/8 hover:decoration-pomegranate ${isSelected ? 'bg-pomegranate/10 ring-1 ring-pomegranate/45' : ''}`}>
        {part}
      </button>
    )
  })
}
