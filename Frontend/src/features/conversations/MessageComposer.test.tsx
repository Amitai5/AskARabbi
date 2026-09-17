import { useState } from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { MessageComposer } from './MessageComposer.tsx'
import { AllSourceKeys } from './sourceOptions.ts'

function renderComposer(options: { enterSendsMessage?: boolean; isSending?: boolean; isChatDisabled?: boolean; selectedSourceKeys?: string[]; draft?: string } = {}) {
  const onSubmit = vi.fn()
  function Composer() {
    const [draft, setDraft] = useState(options.draft ?? 'A question')
    return <MessageComposer selectedSourceKeys={[...AllSourceKeys]} conversationLanguage="English" quotationLanguage="English" isSending={false} {...options} draft={draft} onDraftChange={setDraft} onSelectedSourceKeysChange={vi.fn()} onSubmit={onSubmit} />
  }
  render(<Composer />)
  return { onSubmit, user: userEvent.setup(), input: screen.getByLabelText('Message AskRabbi') }
}

describe('Composer keyboard preferences', () => {
  it('groups the microphone immediately before Send and keeps keyboard guidance screen-reader only', () => {
    renderComposer()
    const microphone = screen.getByRole('button', { name: 'Record question' })
    const send = screen.getByRole('button', { name: 'Send message' })

    expect(microphone.parentElement?.nextElementSibling).toBe(send)
    expect(microphone.textContent).toBe('')
    expect(microphone).toHaveAttribute('type', 'button')
    expect(document.getElementById('message-keyboard-help')).toHaveAttribute('class', 'sr-only')
  })

  it('adds a new line with Enter by default and preserves an unfinished question', async () => {
    const { user, input, onSubmit } = renderComposer()
    await user.click(input)
    await user.keyboard('{End}{Enter}More detail')
    expect(input).toHaveValue('A question\nMore detail')
    expect(onSubmit).not.toHaveBeenCalled()
    expect(input).toHaveAccessibleDescription('Enter for a new line · Ctrl/Cmd+Enter to send')
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    expect(onSubmit).toHaveBeenCalledOnce()
  })

  it('supports opt-in Enter to send and Shift+Enter to add a new line', async () => {
    const { user, input, onSubmit } = renderComposer({ enterSendsMessage: true })
    await user.click(input)
    await user.keyboard('{End}{Shift>}{Enter}{/Shift}More detail')
    expect(input).toHaveValue('A question\nMore detail')
    expect(onSubmit).not.toHaveBeenCalled()
    await user.keyboard('{Enter}')
    expect(onSubmit).toHaveBeenCalledOnce()
  })

  it.each([[false, 'ctrlKey'], [false, 'metaKey'], [true, 'ctrlKey'], [true, 'metaKey']] as const)('sends with %s preference and %s+Enter', (enterSendsMessage, modifier) => {
    const { input, onSubmit } = renderComposer({ enterSendsMessage })
    fireEvent.keyDown(input, { key: 'Enter', [modifier]: true })
    expect(onSubmit).toHaveBeenCalledOnce()
  })

  it.each([{ isComposing: true }, { keyCode: 229 }, { repeat: true }, { altKey: true }])('does not submit IME, repeated, or alternative key events: %j', flags => {
    const { input, onSubmit } = renderComposer({ enterSendsMessage: true })
    fireEvent.keyDown(input, { key: 'Enter', ctrlKey: true, ...flags })
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it.each([{ draft: '  ' }, { isSending: true }, { isChatDisabled: true }, { selectedSourceKeys: [] }])('does not bypass send guards: %j', options => {
    const { input, onSubmit } = renderComposer({ enterSendsMessage: true, ...options })
    fireEvent.keyDown(input, { key: 'Enter', ctrlKey: true })
    expect(onSubmit).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Send message' })).toBeDisabled()
  })
})
