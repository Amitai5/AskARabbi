import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { TeachingAskActions } from './TeachingAskActions.tsx'
import type { ConversationTeachingContext } from '../conversations/conversationData.ts'

const Context = { weekKey: 'diaspora:2026-08-29', title: 'Choosing responsibility', selectedText: null }
function Example({ onAsk, disabled = false }: { onAsk(context: ConversationTeachingContext): void; disabled?: boolean }) {
  return <><TeachingAskActions context={Context} onAsk={onAsk} disabled={disabled} /><p data-testid="teaching">Choose life and care for others.</p></>
}
function select(element: HTMLElement) {
  const range = document.createRange()
  range.selectNodeContents(element)
  window.getSelection()?.removeAllRanges()
  window.getSelection()?.addRange(range)
  fireEvent(document, new Event('selectionchange'))
}
afterEach(() => { cleanup(); window.getSelection()?.removeAllRanges(); vi.restoreAllMocks() })

describe('Teaching ask actions', () => {
  it('offers a whole-teaching action without requiring a selection', async () => {
    const ask = vi.fn()
    render(<Example onAsk={ask} />)
    await userEvent.click(screen.getByRole('button', { name: 'Ask about this teaching' }))
    expect(ask).toHaveBeenCalledExactlyOnceWith(Context)
  })

  it('leaves selected text available for copying without a passage action', () => {
    const ask = vi.fn()
    render(<Example onAsk={ask} />)
    select(screen.getByTestId('teaching'))
    fireEvent.pointerUp(document)
    expect(fireEvent.keyDown(document, { key: 'Tab' })).toBe(true)

    expect(window.getSelection()?.toString()).toBe('Choose life and care for others.')
    expect(screen.queryByRole('group', { name: 'Selected teaching passage' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ask about this passage' })).not.toBeInTheDocument()
    expect(screen.getAllByRole('button')).toHaveLength(1)
    expect(ask).not.toHaveBeenCalled()
  })

  it('always asks about the whole teaching even when text is selected', async () => {
    const ask = vi.fn()
    render(<Example onAsk={ask} />)
    select(screen.getByTestId('teaching'))
    await userEvent.click(screen.getByRole('button', { name: 'Ask about this teaching' }))

    expect(ask).toHaveBeenCalledExactlyOnceWith(Context)
  })

  it('supports keyboard activation of the whole-teaching action', async () => {
    const ask = vi.fn()
    render(<Example onAsk={ask} />)
    const button = screen.getByRole('button', { name: 'Ask about this teaching' })

    await userEvent.tab()
    expect(button).toHaveFocus()
    await userEvent.keyboard('{Enter}')

    expect(ask).toHaveBeenCalledExactlyOnceWith(Context)
  })

  it('does not ask when chat is disabled', async () => {
    const ask = vi.fn()
    render(<Example onAsk={ask} disabled />)
    const button = screen.getByRole('button', { name: 'Ask about this teaching' })
    expect(button).toBeDisabled()
    await userEvent.click(button)
    expect(ask).not.toHaveBeenCalled()
  })
})
