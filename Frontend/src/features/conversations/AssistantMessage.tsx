import { memo, useMemo } from 'react'
import { AnswerCopyButton } from './AnswerCopyButton.tsx'
import { normalizeDisplayText } from '../../displayText.ts'
import type { ConversationMessage, ConversationSource } from './conversationData.ts'
import { useReadingTarget } from '../reading/focusedReadingContext.ts'
import { PrintAction } from '../printing/PrintAction.tsx'
import { collectPrintAnswers, type PrintRequest } from '../printing/printTypes.ts'

interface AssistantMessageProps {
  autoFocusEligible?: boolean
  message: ConversationMessage
  selectedSourceNumber: number | null
  onSelectSource(messageId: string, sourceNumber: number, trigger: HTMLButtonElement): void
  getPrintRequest?(messageId: string): PrintRequest
}

const EmptySources: readonly ConversationSource[] = []

export const AssistantMessage = memo(function AssistantMessage({ message, selectedSourceNumber, onSelectSource, getPrintRequest, autoFocusEligible = false }: AssistantMessageProps) {
  const sources = message.sources ?? EmptySources
  const sourceNumbers = useMemo(() => new Set(sources.map((source) => source.number)), [sources])
  const normalizedContent = useMemo(() => normalizeDisplayText(message.content), [message.content])
  const readingId = `answer:${message.id}`
  const reading = useReadingTarget(readingId, normalizedContent, autoFocusEligible)

  return (
    <div className="conversation-message group relative border-l-2 border-pomegranate pb-10 pl-5 sm:pb-0" data-message-role="assistant" data-reading-target={readingId} data-reading-focused={reading.isFocused}>
      <div className="mb-3"><p className="font-display text-xl text-ink">AskRabbi</p></div>
      <div className="reading-content space-y-4 text-base leading-7 text-ink sm:text-lg">
        {normalizedContent.trim().split(/\n\s*\n/).map((paragraph, index) => (
          <p key={`${message.id}-paragraph-${index}`} dir="auto" className="sm:last:min-h-9 sm:last:pr-32">{renderParagraph(paragraph, sourceNumbers, message.id, selectedSourceNumber, onSelectSource)}</p>
        ))}
      </div>
      <div className="absolute bottom-0 right-0 flex gap-2 p-0.5" role="group" aria-label="Answer actions">
        <PrintAction iconOnly compact className="answer-print-button" label="Print answer" getRequest={() => getPrintRequest?.(message.id) ?? { kind: 'answers', title: 'Conversation study copy', answers: collectPrintAnswers([message]), initialAnswerId: message.id }} />
        <AnswerCopyButton key={`${message.id}-text`} message={message} mode="text" />
        <AnswerCopyButton key={`${message.id}-sources`} message={message} mode="sources" />
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
      <button key={`${messageId}-citation-${sourceNumber}-${index}`} type="button" dir="ltr" aria-label={`View source ${sourceNumber}`} aria-expanded={isSelected} onClick={(event) => onSelectSource(messageId, sourceNumber, event.currentTarget)} className={`relative mx-0.5 inline-flex rounded-sm px-0.5 font-semibold text-pomegranate underline decoration-pomegranate/35 underline-offset-4 transition hover:bg-pomegranate/8 hover:decoration-pomegranate ${isSelected ? 'bg-pomegranate/10 ring-1 ring-pomegranate/45' : ''}`}>
        {part}
      </button>
    )
  })
}
