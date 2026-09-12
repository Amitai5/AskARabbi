import { createRef } from 'react'
import { act, fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ConversationQuestionNavigation } from './ConversationQuestionNavigation.tsx'
import { questionElementId } from './questionNavigation.ts'
import { UserMessage } from './UserMessage.tsx'
import type { ConversationMessage } from './conversationData.ts'

function createMessages(count: number): ConversationMessage[] {
  return Array.from({ length: count }, (_, index) => [
    { id: `q${index}`, role: 'User' as const, content: index === 0 ? 'What does “שלום” mean?' : `My question ${index + 1}?`, createdAtUtc: '2026-09-11T12:00:00Z' },
    { id: `a${index}`, role: 'Assistant' as const, content: `Answer ${index + 1} is not a generated summary.`, createdAtUtc: '2026-09-11T12:00:00Z' },
  ]).flat()
}

function setup(count = 4) {
  const scrollRef = createRef<HTMLElement>()
  const onNavigate = vi.fn()
  const messages = createMessages(count)
  const view = render(<><section ref={scrollRef} aria-label="Conversation">{messages.filter(message => message.role === 'User').map(message => <UserMessage key={message.id} message={message} />)}</section><ConversationQuestionNavigation messages={messages} scrollRef={scrollRef} onNavigate={onNavigate} /></>)
  const container = screen.getByRole('region', { name: 'Conversation' })
  Object.defineProperties(container, { scrollTop: { configurable: true, writable: true, value: 500 }, scrollHeight: { configurable: true, value: 2400 }, clientHeight: { configurable: true, value: 400 } })
  vi.spyOn(container, 'getBoundingClientRect').mockReturnValue({ top: 50 } as DOMRect)
  messages.filter(message => message.role === 'User').forEach((message, index) => {
    vi.spyOn(document.getElementById(questionElementId(message.id))!, 'getBoundingClientRect').mockImplementation(() => ({ top: 50 + index * 400 - container.scrollTop }) as DOMRect)
  })
  return { ...view, container, onNavigate, scrollRef, messages, user: userEvent.setup() }
}

describe('Long conversation question rail', () => {
  beforeEach(() => { Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() }) })
  afterEach(() => { vi.restoreAllMocks(); vi.useRealTimers() })

  it('stays absent below four questions', () => {
    setup(3)
    expect(screen.queryByRole('navigation', { name: 'Questions in this conversation' })).not.toBeInTheDocument()
  })

  it('uses actual question text, hides on phones through the breakpoint, and scopes scrolling to the conversation', async () => {
    const { user, container, onNavigate } = setup()
    const navigation = screen.getByRole('navigation', { name: 'Questions in this conversation' })
    expect(navigation).toHaveClass('hidden', 'md:flex')
    expect(within(navigation).queryByText(/generated summary/)).not.toBeInTheDocument()
    const question = within(navigation).getByRole('button', { name: 'Question 3: My question 3?' })
    await user.hover(question)
    expect(within(navigation).getByText('My question 3?')).toBeVisible()
    await user.click(question)
    expect(onNavigate).toHaveBeenLastCalledWith(false)
    expect(document.getElementById(questionElementId('q2'))).toHaveFocus()
    expect(container.scrollTo).toHaveBeenCalledWith({ top: 780, behavior: 'auto' })
    await user.click(within(navigation).getByRole('button', { name: 'Latest answer' }))
    expect(onNavigate).toHaveBeenLastCalledWith(true)
    expect(container.scrollTo).toHaveBeenLastCalledWith({ top: 2400, behavior: 'auto' })
  })

  it('opens a keyboard/touch question list and restores focus on Escape or closes outside', async () => {
    const { user } = setup(40)
    const browse = screen.getByRole('button', { name: 'Browse questions' })
    await user.click(browse)
    expect(browse).toHaveAttribute('aria-expanded', 'true')
    const list = document.getElementById(browse.getAttribute('aria-controls')!)!
    expect(within(list).getAllByRole('listitem')).toHaveLength(40)
    expect(list).toContainElement(document.activeElement as HTMLElement)
    await user.keyboard('{Escape}')
    expect(browse).toHaveFocus()
    expect(browse).toHaveAttribute('aria-expanded', 'false')
    await user.click(browse)
    fireEvent.pointerDown(document.body)
    expect(browse).toHaveAttribute('aria-expanded', 'false')
    await user.click(browse)
    await user.click(screen.getByRole('button', { name: 'My question 40?' }))
    expect(document.getElementById(questionElementId('q39'))).toHaveFocus()
  })

  it('tracks manual scroll positions and updates the latest marker', () => {
    vi.useFakeTimers()
    const { container } = setup()
    act(() => { vi.advanceTimersByTime(30) })
    expect(screen.getByRole('button', { name: 'Question 2: My question 2?' })).toHaveAttribute('aria-current', 'location')
    container.scrollTop = 2000
    fireEvent.scroll(container)
    act(() => { vi.advanceTimersByTime(30) })
    expect(screen.getByRole('button', { name: 'Question 4: My question 4?' })).toHaveAttribute('aria-current', 'location')
  })
})
