import { MessageCircle } from 'lucide-react'
import type { ConversationTeachingContext } from '../conversations/conversationData.ts'

interface Props {
  context: ConversationTeachingContext
  disabled?: boolean
  onAsk(context: ConversationTeachingContext): void
}

export function TeachingAskActions({ context, disabled = false, onAsk }: Props) {
  function ask() {
    if (disabled) { return }
    onAsk({ ...context, selectedText: null })
  }

  return (
    <button type="button" disabled={disabled} onClick={ask} aria-label="Ask about this teaching" title={disabled ? 'Chat is unavailable right now' : 'Start a new conversation with the whole teaching as context'} className="inline-flex min-h-11 items-center gap-2 rounded-lg border border-line-strong bg-paper px-3 text-sm font-semibold text-pomegranate hover:bg-stone disabled:opacity-50">
      <MessageCircle aria-hidden="true" className="size-4" /><span className="sm:hidden">Ask</span><span className="hidden sm:inline">Ask about this teaching</span>
    </button>
  )
}
