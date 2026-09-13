import { useRef } from 'react'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { TeachingAskActions } from './TeachingAskActions.tsx'
import type { ConversationTeachingContext } from '../conversations/conversationData.ts'

const Context = { weekKey: 'diaspora:2026-08-29', title: 'Choosing responsibility', selectedText: null }
function Example({ onAsk, disabled = false, text = 'Choose life and care for others.' }: { onAsk(context: ConversationTeachingContext): void; disabled?: boolean; text?: string }) {
  const bodyRef = useRef<HTMLDivElement>(null)
  return <><p>Outside the teaching</p><TeachingAskActions context={Context} bodyRef={bodyRef} onAsk={onAsk} disabled={disabled} /><div ref={bodyRef} data-testid="teaching">{text}</div></>
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

  it('keeps the selected text until the action is clicked and includes the teaching key', async () => {
    const ask = vi.fn()
    render(<Example onAsk={ask} />)
    select(screen.getByTestId('teaching'))
    const button = await screen.findByRole('button', { name: 'Ask about this passage' })
    expect(ask).not.toHaveBeenCalled()
    await userEvent.click(button)
    expect(ask).toHaveBeenCalledExactlyOnceWith({ ...Context, selectedText: 'Choose life and care for others.' })
    expect(window.getSelection()?.isCollapsed).toBe(true)
  })

  it('rejects selections outside the teaching and dismisses on Escape', async () => {
    render(<Example onAsk={vi.fn()} />)
    select(screen.getByTestId('teaching'))
    await screen.findByRole('button', { name: 'Ask about this passage' })
    fireEvent.keyDown(document, { key: 'Escape' })
    expect(screen.queryByRole('button', { name: 'Ask about this passage' })).not.toBeInTheDocument()
    select(screen.getByText('Outside the teaching'))
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Ask about this passage' })).not.toBeInTheDocument())
  })

  it('lets a keyboard user reach the selection action with Tab and activate it with Enter', async () => {
    const ask = vi.fn()
    render(<Example onAsk={ask} />)
    select(screen.getByTestId('teaching'))
    const button = await screen.findByRole('button', { name: 'Ask about this passage' })

    await userEvent.tab()
    expect(button).toHaveFocus()
    await userEvent.keyboard('{Enter}')

    expect(ask).toHaveBeenCalledExactlyOnceWith({ ...Context, selectedText: 'Choose life and care for others.' })
  })

  it('does not submit disabled or overlong selections', async () => {
    const ask = vi.fn()
    const { rerender } = render(<Example onAsk={ask} disabled />)
    expect(screen.getByRole('button', { name: 'Ask about this teaching' })).toBeDisabled()
    select(screen.getByTestId('teaching'))
    expect(await screen.findByRole('button', { name: 'Ask about this passage' })).toBeDisabled()
    rerender(<Example onAsk={ask} text={'a'.repeat(4001)} />)
    select(screen.getByTestId('teaching'))
    await screen.findByText('Select a shorter passage, or ask about the whole teaching.')
    expect(screen.getByRole('button', { name: 'Ask about this passage' })).toBeDisabled()
    expect(ask).not.toHaveBeenCalled()
  })
})
