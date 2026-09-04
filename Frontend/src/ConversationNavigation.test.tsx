import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'
import type { ConversationClient } from './features/conversations/conversationClient.ts'
import type { DvarTorahClient } from './features/dvarTorah/dvarTorahClient.ts'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'

const Question = 'Explain the weekly Torah reading'
const dvarTorahClient: DvarTorahClient = {
  getCurrent: () => Promise.resolve({
    currentWeek: { weekKey: 'diaspora:2026-09-05', shabbatDate: '2026-09-05', hebrewDate: '23 Elul, 5786', parashah: 'Nitzavim', holiday: null, inIsrael: false },
    dvarTorah: null,
    isCurrentWeek: true,
  }),
  getArchive: () => Promise.resolve({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 }),
  getArchived: () => Promise.reject(new Error('No archived articles in this fixture.')),
  getAudioUrl: () => '',
  getAudioTimings: () => Promise.resolve(null),
}

describe('Background conversation navigation', () => {
  beforeEach(() => {
    Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() })
    window.sessionStorage.clear()
    window.history.replaceState({}, '', '/')
  })

  it.each([
    ['Dvar Torah', 'A teaching for the week.'],
    ['Personalization', 'Make AskRabbi yours.'],
    ['Settings', 'Account and usage.'],
  ])('finishes a new conversation while visiting %s without navigating away', async (destination, heading) => {
    const { user, releaseCreate, conversationClient } = await renderPendingApp()
    await startNewConversation(user)

    expect(screen.getByRole('button', { name: Question, current: 'page' })).toBeEnabled()
    expect(screen.getByRole('status', { name: `Generating answer for ${Question}` })).toBeVisible()
    expect(screen.getByRole('button', { name: `Conversation actions for ${Question}, item 1` })).toBeDisabled()
    if (destination === 'Dvar Torah') {
      await user.click(screen.getByRole('button', { name: 'This week’s Dvar Torah' }))
    } else {
      await user.click(screen.getByRole('button', { name: 'Open profile menu' }))
      await user.click(screen.getByRole('menuitem', { name: destination }))
    }
    expect(await screen.findByRole('heading', { name: heading })).toBeVisible()
    expect(screen.queryByLabelText('Message AskRabbi')).not.toBeInTheDocument()

    await act(async () => releaseCreate(null))

    await waitFor(() => expect(screen.queryByRole('status', { name: `Generating answer for ${Question}` })).not.toBeInTheDocument())
    expect(screen.getByRole('heading', { name: heading })).toBeVisible()
    expect(screen.getAllByRole('button', { name: Question })).toHaveLength(1)
    await user.click(screen.getByRole('button', { name: Question }))
    expect(await screen.findByText(/this local demo represents a validated grounded response/)).toBeVisible()
    expect(within(screen.getByRole('region', { name: 'Current conversation' })).getByText(Question)).toBeVisible()
    expect(conversationClient.createWithMessage).toHaveBeenCalledTimes(1)
    expect(conversationClient.get).not.toHaveBeenCalledWith(expect.stringContaining('pending:'))
  })

  it('returns to the pending conversation without refetching a partial response', async () => {
    const { user, releaseCreate, conversationClient } = await renderPendingApp()
    await startNewConversation(user)
    await user.click(screen.getByRole('button', { name: 'Shabbat and automation' }))

    expect(await screen.findByRole('button', { name: 'Shabbat and automation', current: 'page' })).toBeVisible()
    expect(screen.queryByTestId('answer-progress-dots')).not.toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Current conversation' })).queryByText(Question)).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: Question }))
    expect(screen.getByTestId('answer-progress-dots')).toBeVisible()
    expect(within(screen.getByRole('article')).getByText(Question)).toBeVisible()
    await user.type(screen.getByLabelText('Message AskRabbi'), 'My next question')
    await user.click(screen.getByRole('button', { name: 'This week’s Dvar Torah' }))
    await user.click(screen.getByRole('button', { name: Question }))
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('My next question')

    await act(async () => releaseCreate(null))
    expect(await screen.findByText(/this local demo represents a validated grounded response/)).toBeVisible()
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('My next question')
    expect(conversationClient.get).not.toHaveBeenCalledWith(expect.stringContaining('pending:'))
  })

  it('keeps simultaneous replies, progress, and drafts in their own conversations', async () => {
    const { user, releaseCreate, releaseAppend } = await renderPendingApp()
    const followUp = 'Explain this source further'
    await user.type(screen.getByLabelText('Message AskRabbi'), followUp)
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    await startNewConversation(user)

    await act(async () => releaseAppend(null))

    await waitFor(() => expect(screen.queryByRole('status', { name: 'Generating answer for Chicken and dairy' })).not.toBeInTheDocument())
    expect(screen.getByRole('button', { name: Question, current: 'page' })).toBeVisible()
    expect(screen.getByTestId('answer-progress-dots')).toBeVisible()
    expect(screen.queryByText(/local demo follow-up remains grounded/)).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Chicken and dairy' }))
    expect(await screen.findByText(/local demo follow-up remains grounded/)).toBeVisible()
    expect(screen.queryByTestId('answer-progress-dots')).not.toBeInTheDocument()
    await user.type(screen.getByLabelText('Message AskRabbi'), 'An unsent follow-up')

    await act(async () => releaseCreate(null))

    await waitFor(() => expect(screen.queryByRole('status', { name: `Generating answer for ${Question}` })).not.toBeInTheDocument())
    expect(screen.getByRole('button', { name: 'Chicken and dairy', current: 'page' })).toBeVisible()
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('An unsent follow-up')
    await user.click(screen.getByRole('button', { name: Question }))
    expect(await screen.findByText(/this local demo represents a validated grounded response/)).toBeVisible()
    expect(screen.queryByText(followUp)).not.toBeInTheDocument()
  })

  it('scrolls back to the pending message when returning from the profile', async () => {
    const { user, releaseCreate } = await renderPendingApp()
    await startNewConversation(user)
    await user.click(screen.getByRole('button', { name: 'Open profile menu' }))
    await user.click(screen.getByRole('menuitem', { name: 'Personalization' }))
    const scrollTo = vi.mocked(HTMLElement.prototype.scrollTo)
    scrollTo.mockClear()

    await user.click(screen.getByRole('button', { name: 'Back to conversation' }))

    expect(screen.getByTestId('answer-progress-dots')).toBeVisible()
    expect(scrollTo).toHaveBeenCalledWith({ top: 0, behavior: 'auto' })
    await act(async () => releaseCreate(null))
    expect(await screen.findByText(/this local demo represents a validated grounded response/)).toBeVisible()
  })

  it('keeps a background failure and its retry draft out of another conversation', async () => {
    const { user, releaseCreate, conversationClient, clients } = await renderPendingApp()
    await startNewConversation(user)
    await user.click(screen.getByRole('button', { name: 'Shabbat and automation' }))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'An unrelated draft')

    await act(async () => releaseCreate('The request could not be completed.'))

    await waitFor(() => expect(screen.queryByRole('status', { name: `Generating answer for ${Question}` })).not.toBeInTheDocument())
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('An unrelated draft')
    await user.click(screen.getByRole('button', { name: Question }))
    expect(screen.getByRole('alert')).toHaveTextContent('The request could not be completed.')
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue(Question)

    conversationClient.createWithMessage.mockImplementation(clients.conversationClient.createWithMessage)
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    expect(await screen.findByText(/this local demo represents a validated grounded response/)).toBeVisible()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: Question })).toHaveLength(1)
    expect(conversationClient.appendMessage).not.toHaveBeenCalled()
  })
})

async function startNewConversation(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'New conversation' }))
  await user.type(screen.getByLabelText('Message AskRabbi'), Question)
  await user.click(screen.getByRole('button', { name: 'Send message' }))
}

async function renderPendingApp() {
  const clients = createDemoApplicationClients()
  let releaseCreate!: (error: string | null) => void
  let releaseAppend!: (error: string | null) => void
  const createGate = new Promise<string | null>((resolve) => { releaseCreate = resolve })
  const appendGate = new Promise<string | null>((resolve) => { releaseAppend = resolve })
  const conversationClient = {
    ...clients.conversationClient,
    get: vi.fn(clients.conversationClient.get),
    createWithMessage: vi.fn(async (...args: Parameters<ConversationClient['createWithMessage']>) => {
      const error = await createGate
      if (error !== null) {
        throw new Error(error)
      }
      return clients.conversationClient.createWithMessage(...args)
    }),
    appendMessage: vi.fn(async (...args: Parameters<ConversationClient['appendMessage']>) => {
      const error = await appendGate
      if (error !== null) {
        throw new Error(error)
      }
      return clients.conversationClient.appendMessage(...args)
    }),
  }
  const user = userEvent.setup()
  render(<App authClient={clients.authClient} conversationClient={conversationClient} conversationSettingsClient={clients.conversationSettingsClient} dvarTorahClient={dvarTorahClient} />)
  await user.click(await screen.findByRole('button', { name: 'Continue with Google' }))
  await waitFor(() => expect(screen.getByRole('button', { name: 'New conversation' })).toBeEnabled())
  return { user, clients, conversationClient, releaseCreate, releaseAppend }
}
