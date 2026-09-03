import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AssistantMessage } from './AssistantMessage.tsx'
import type { ConversationMessage } from './conversationData.ts'

const Message: ConversationMessage = {
  id: 'assistant-message',
  role: 'Assistant',
  content: 'A grounded answer that should be copied.',
  createdAtUtc: '2026-09-01T12:00:00Z',
}

describe('AssistantMessage', () => {
  afterEach(() => vi.restoreAllMocks())

  it('copies from an icon-only action at the bottom of the response', async () => {
    const user = userEvent.setup()
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValueOnce(undefined)
    render(<AssistantMessage message={Message} selectedSourceNumber={null} onSelectSource={vi.fn()} />)

    const copyButton = screen.getByRole('button', { name: 'Copy answer' })
    expect(copyButton).toHaveClass('opacity-0', 'group-hover:opacity-100', 'group-focus-within:opacity-100')
    expect(copyButton).not.toHaveTextContent('Copy')
    expect(copyButton.querySelector('svg')).toBeInTheDocument()
    expect(copyButton.parentElement).toHaveClass('mt-3', 'justify-end', 'pb-2', 'pr-2')
    expect(screen.getByText(Message.content).compareDocumentPosition(copyButton) & Node.DOCUMENT_POSITION_FOLLOWING).not.toBe(0)

    await user.click(copyButton)

    expect(writeText).toHaveBeenCalledWith(Message.content)
    expect(screen.getByRole('button', { name: 'Answer copied' })).toHaveClass('opacity-100')
    expect(screen.getByRole('status')).toHaveTextContent('Answer copied to clipboard.')
  })

  it('shows retry feedback when clipboard access fails', async () => {
    const user = userEvent.setup()
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValueOnce(new Error('Clipboard permission denied.'))
    render(<AssistantMessage message={Message} selectedSourceNumber={null} onSelectSource={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Copy answer' }))

    expect(writeText).toHaveBeenCalledWith(Message.content)
    expect(screen.getByRole('button', { name: 'Copy failed. Try again' })).toBeVisible()
    expect(screen.getByRole('status')).toHaveTextContent('The answer could not be copied. Try again.')
  })

  it('repairs malformed typography when displaying and copying an answer', async () => {
    const user = userEvent.setup()
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValueOnce(undefined)
    const malformedMessage = {
      ...Message,
      content: 'Joseph\u0019s identity\u0014and his family\u0092s move are discussed. [1]',
    }
    render(<AssistantMessage message={malformedMessage} selectedSourceNumber={null} onSelectSource={vi.fn()} />)

    expect(screen.getByText('Joseph’s identity—and his family’s move are discussed. [1]')).toBeVisible()
    expect(document.body).not.toHaveTextContent('\u0019')
    expect(document.body).not.toHaveTextContent('\u0092')

    await user.click(screen.getByRole('button', { name: 'Copy answer' }))

    expect(writeText).toHaveBeenCalledWith('Joseph’s identity—and his family’s move are discussed. [1]')
  })
})
