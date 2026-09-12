import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'
import type { ConversationDetails } from './features/conversations/conversationData.ts'
import type { ConversationTurn } from './features/conversations/conversationClient.ts'
import { questionElementId } from './features/conversations/questionNavigation.ts'
import { createDefaultUserSettings } from './features/settings/settingsTypes.ts'

const Conversation: ConversationDetails = {
  id: 'long-chat', title: 'A longer conversation', enabledSourceKeys: ['collection:Torah'], createdAtUtc: '2026-09-11T12:00:00Z', updatedAtUtc: '2026-09-11T12:00:00Z',
  messages: Array.from({ length: 4 }, (_, index) => [
    { id: `q-${index}`, role: 'User' as const, content: `Original question ${index + 1}?`, createdAtUtc: '2026-09-11T12:00:00Z' },
    { id: `a-${index}`, role: 'Assistant' as const, content: `This is the original answer ${index + 1}.`, sources: [], createdAtUtc: '2026-09-11T12:00:00Z' },
  ]).flat(),
}

describe('Account keyboard preference and long conversation navigation', () => {
  beforeEach(() => {
    window.localStorage.clear()
    window.sessionStorage.clear()
    window.history.replaceState({}, '', '/conversations/long-chat')
    Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() })
  })

  async function mountApp() {
    const clients = createDemoApplicationClients()
    const conversationClient = { ...clients.conversationClient, list: vi.fn().mockResolvedValue([Conversation]), get: vi.fn().mockResolvedValue(Conversation), appendMessage: vi.fn(clients.conversationClient.appendMessage) }
    const save = vi.spyOn(clients.conversationSettingsClient, 'updatePreferences')
    const view = render(<App {...clients} conversationClient={conversationClient} />)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Continue with Google' }))
    await screen.findByText('This is the original answer 4.')
    await waitFor(() => expect(screen.getByRole('button', { name: 'New conversation' })).toBeEnabled())
    return { user, clients, conversationClient, save, ...view }
  }

  it('searches for the Enter setting, saves it to the account, and applies it after reloading', async () => {
    const { user, clients, save, conversationClient, unmount } = await mountApp()
    await user.click(screen.getByRole('button', { name: 'Open profile menu' }))
    await user.click(screen.getByRole('menuitem', { name: 'Settings & Personalization' }))
    await user.type(screen.getByRole('searchbox', { name: 'Search settings' }), 'keyboard')
    await user.click(screen.getByRole('button', { name: 'Choose how Enter works, Reading' }))
    expect(window.location.pathname + window.location.hash).toBe('/settings/reading#enter-key')
    expect(document.getElementById('setting-enter-key')).toHaveClass('settings-highlight')
    const select = screen.getByRole('combobox', { name: 'Choose how Enter works' })
    expect(select).toHaveValue('newline')
    await user.selectOptions(select, 'send')
    await user.click(screen.getByRole('button', { name: 'Save settings' }))
    await waitFor(() => expect(save).toHaveBeenCalledWith({ ...createDefaultUserSettings(), enterSendsMessage: true }))
    await user.click(screen.getByRole('button', { name: 'Back' }))
    expect(screen.getByRole('button', { name: 'Send message' })).toHaveAttribute('aria-keyshortcuts', 'Enter Control+Enter Meta+Enter')
    unmount()

    render(<App {...clients} conversationClient={conversationClient} />)
    await user.click(await screen.findByRole('button', { name: 'Continue with Google' }))
    await screen.findByText('This is the original answer 4.')
    expect(screen.getByRole('button', { name: 'Send message' })).toHaveAttribute('aria-keyshortcuts', 'Enter Control+Enter Meta+Enter')
  })

  it('retains earlier-question navigation and an unsent draft when a pending answer completes', async () => {
    const { user, conversationClient } = await mountApp()
    let complete!: (turn: ConversationTurn) => void
    conversationClient.appendMessage.mockImplementation(() => new Promise(resolve => { complete = resolve }))
    const input = screen.getByLabelText('Message AskRabbi')
    await user.type(input, 'A follow-up question')
    fireEvent.keyDown(input, { key: 'Enter', ctrlKey: true })
    await waitFor(() => expect(conversationClient.appendMessage).toHaveBeenCalledOnce())
    await user.type(input, 'An unfinished next question')
    const container = screen.getByRole('region', { name: 'Current conversation' })
    const scroll = vi.spyOn(container, 'scrollTo')
    await user.click(screen.getByRole('button', { name: 'Question 1: Original question 1?' }))
    expect(document.getElementById(questionElementId('q-0'))).toHaveFocus()
    scroll.mockClear()
    const [, messageId, content] = conversationClient.appendMessage.mock.calls[0]
    await act(async () => complete({ status: 'answered', message: null, conversation: { ...Conversation, messages: [...Conversation.messages, { id: messageId, role: 'User', content, createdAtUtc: Conversation.createdAtUtc }, { id: 'new-answer', role: 'Assistant', content: 'The new answer is here.', sources: [], createdAtUtc: Conversation.createdAtUtc }] } }))
    await screen.findByText('The new answer is here.')
    expect(scroll).not.toHaveBeenCalled()
    expect(input).toHaveValue('An unfinished next question')
    await user.click(screen.getByRole('button', { name: 'Latest answer' }))
    expect(scroll).toHaveBeenCalledWith({ top: container.scrollHeight, behavior: 'auto' })
    expect(input).toHaveValue('An unfinished next question')
  })
})
