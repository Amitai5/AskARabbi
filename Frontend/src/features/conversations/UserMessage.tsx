import { memo } from 'react'
import { normalizeDisplayText } from '../../displayText.ts'
import type { ConversationMessage } from './conversationData.ts'

interface UserMessageProps {
  message: ConversationMessage
}

export const UserMessage = memo(function UserMessage({ message }: UserMessageProps) {
  return (
    <div className="conversation-message flex justify-end" data-message-role="user">
      <div className="max-w-[88%] rounded-2xl rounded-br-md border border-line-strong/80 bg-stone px-4 py-3 shadow-sm sm:max-w-[78%] sm:px-5 sm:py-4">
        <p className="mb-1.5 text-right text-xs font-semibold uppercase tracking-[0.14em] text-pomegranate">You</p>
        <p className="whitespace-pre-wrap text-base leading-7 text-ink sm:text-lg">{normalizeDisplayText(message.content)}</p>
      </div>
    </div>
  )
})
