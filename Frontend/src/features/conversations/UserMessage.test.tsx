import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { UserMessage } from './UserMessage.tsx'
import type { ConversationMessage } from './conversationData.ts'

const Message: ConversationMessage = {
  id: 'user-message',
  role: 'User',
  content: 'How should I understand this teaching?',
  createdAtUtc: '2026-09-01T12:00:00Z',
}

describe('UserMessage', () => {
  it('presents the user turn as a right-aligned chat bubble', () => {
    render(<UserMessage message={Message} />)

    const message = screen.getByText(Message.content).closest('[data-message-role="user"]')
    const bubble = screen.getByText(Message.content).parentElement

    expect(message).toHaveClass('flex', 'justify-end')
    expect(bubble).toHaveClass('rounded-2xl', 'bg-stone')
    expect(screen.getByText('You')).toBeVisible()
  })
})
