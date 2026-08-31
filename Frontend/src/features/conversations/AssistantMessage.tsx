import { memo, useMemo } from 'react'
import type { ConversationMessage, ConversationSource } from './conversationData.ts'

interface AssistantMessageProps {
  message: ConversationMessage
  selectedSourceNumber: number | null
  onSelectSource(messageId: string, sourceNumber: number, trigger: HTMLButtonElement): void
}

const EmptySources: readonly ConversationSource[] = []

export const AssistantMessage = memo(function AssistantMessage({ message, selectedSourceNumber, onSelectSource }: AssistantMessageProps) {
  const sources = message.sources ?? EmptySources
  const sourceNumbers = useMemo(() => new Set(sources.map((source) => source.number)), [sources])

  return (
    <div className="conversation-message border-l-2 border-pomegranate pl-5">
      <p className="mb-3 font-display text-xl text-ink">AskRabbi</p>
      <div className="space-y-4 text-base leading-7 text-ink sm:text-lg">
        {message.content.split(/\n\n+/).map((paragraph, index) => (
          <p key={`${message.id}-paragraph-${index}`}>{renderParagraph(paragraph, sourceNumbers, message.id, selectedSourceNumber, onSelectSource)}</p>
        ))}
      </div>
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
