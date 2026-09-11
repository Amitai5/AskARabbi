import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'
import { ApiError } from './api/apiClient.ts'
import type { DvarTorahClient } from './features/dvarTorah/dvarTorahClient.ts'
import type { UsageSummary } from './features/settings/settingsTypes.ts'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'

const Exhausted: UsageSummary = {
  periodStartUtc: '2026-09-01T00:00:00Z', periodEndUtc: '2026-10-01T00:00:00Z',
  tokensUsed: 10_000_000, tokenLimit: 10_000_000, tokensRemaining: 0, usedPercent: 100, isLimitReached: true,
}
const Available: UsageSummary = { ...Exhausted, tokensUsed: 2_500_000, tokensRemaining: 7_500_000, usedPercent: 25, isLimitReached: false }
const dvarTorahClient: DvarTorahClient = {
  getCurrent: () => Promise.resolve({ currentWeek: { weekKey: 'diaspora:2026-09-05', shabbatDate: '2026-09-05', hebrewDate: '23 Elul, 5786', parashah: 'Nitzavim', holiday: null, inIsrael: false }, dvarTorah: null, isCurrentWeek: true }),
  getArchive: () => Promise.resolve({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 }),
  getArchived: () => Promise.reject(new Error('No archived articles.')),
  getAudioUrl: () => '', getAudioTimings: () => Promise.resolve(null),
}

describe('Monthly token allowance', () => {
  afterEach(() => vi.restoreAllMocks())
  beforeEach(() => {
    Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() })
    window.sessionStorage.clear()
    window.history.replaceState({}, '', '/')
  })

  it('disables old and new chats at 100% while keeping history and Dvar Torah accessible', async () => {
    const clients = createDemoApplicationClients()
    const old = await clients.conversationClient.createWithMessage('saved', 'A saved question', ['collection:Torah'])
    const create = vi.spyOn(clients.conversationClient, 'createWithMessage')
    const append = vi.spyOn(clients.conversationClient, 'appendMessage')
    clients.conversationSettingsClient.getUsage = () => Promise.resolve(Exhausted)
    const user = await signIn(clients)

    expect(await screen.findByText('Monthly chat limit reached')).toBeVisible()
    await user.click(screen.getByRole('button', { name: old.conversation.title }))
    expect(await screen.findByText(/this local demo represents a validated grounded response/)).toBeVisible()
    expect(screen.getByLabelText('Message AskRabbi')).toHaveAttribute('readonly')
    fireEvent.keyDown(screen.getByLabelText('Message AskRabbi'), { key: 'Enter' })
    await user.click(screen.getByRole('button', { name: 'New conversation' }))
    expect(screen.getByRole('button', { name: 'Send message' })).toBeDisabled()
    expect(create).not.toHaveBeenCalled()
    expect(append).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Read this week’s Dvar Torah' }))
    expect(await screen.findByRole('heading', { name: 'A teaching for the week.' })).toBeVisible()
  })

  it('uses the completed turn allowance immediately and still displays its answer', async () => {
    const clients = createDemoApplicationClients()
    clients.conversationSettingsClient.getUsage = () => Promise.resolve(Available)
    const original = clients.conversationClient.appendMessage
    clients.conversationClient.appendMessage = async (...args) => ({ ...await original(...args), usage: Exhausted })
    const user = await signIn(clients)
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Explain further')
    await user.click(screen.getByRole('button', { name: 'Send message' }))

    expect(await screen.findByText('Monthly chat limit reached')).toBeVisible()
    expect(screen.getByText(/local demo follow-up remains grounded/)).toBeVisible()
    expect(screen.getByRole('button', { name: 'Send message' })).toBeDisabled()
  })

  it('handles an exhausted response from another tab without leaving an unsaved sidebar chat', async () => {
    const clients = createDemoApplicationClients()
    clients.conversationSettingsClient.getUsage = () => Promise.resolve(Available)
    clients.conversationClient.createWithMessage = vi.fn(() => Promise.reject(new ApiError(429, { code: 'usage_limit_reached', detail: 'Monthly limit reached.', usage: Exhausted })))
    const user = await signIn(clients)
    await user.click(screen.getByRole('button', { name: 'New conversation' }))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Keep my unsent question')
    await user.click(screen.getByRole('button', { name: 'Send message' }))

    expect(await screen.findByText('Monthly chat limit reached')).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Keep my unsent question' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('Keep my unsent question')
    fireEvent.keyDown(screen.getByLabelText('Message AskRabbi'), { key: 'Enter' })
    expect(clients.conversationClient.createWithMessage).toHaveBeenCalledTimes(1)
  })

  it('shows only the remaining monthly percentage and reset in settings, never in available chats', async () => {
    const clients = createDemoApplicationClients()
    clients.conversationSettingsClient.getUsage = () => Promise.resolve(Available)
    const user = await signIn(clients)
    expect(screen.queryByText(/monthly|25%|75%|tokens/i)).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Open profile menu' }))
    await user.click(screen.getByRole('menuitem', { name: 'Settings' }))

    const progress = await screen.findByRole('progressbar', { name: 'Monthly chat allowance remaining' })
    expect(progress).toHaveAttribute('aria-valuenow', '75')
    expect(screen.getByText('75% left')).toBeVisible()
    expect(screen.getByText(/^Resets /)).toBeVisible()
    expect(screen.queryByText(/2,500,000|10,000,000|Includes reasoning|tokens/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/answers remaining/)).not.toBeInTheDocument()
  })

  it('does not let a stale usage request overwrite an exhausted turn response', async () => {
    const clients = createDemoApplicationClients()
    const stale = deferred<UsageSummary>()
    clients.conversationSettingsClient.getUsage = vi.fn().mockResolvedValueOnce(Available).mockReturnValue(stale.promise)
    const original = clients.conversationClient.appendMessage
    clients.conversationClient.appendMessage = async (...args) => ({ ...await original(...args), usage: Exhausted })
    const user = await signIn(clients)
    fireEvent(window, new Event('focus'))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Explain further')
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    expect(await screen.findByText('Monthly chat limit reached')).toBeVisible()

    await act(async () => stale.resolve(Available))

    expect(screen.getByRole('button', { name: 'Send message' })).toBeDisabled()
    expect(screen.getByText('Monthly chat limit reached')).toBeVisible()
  })

  it('pauses old and new chats offline without losing or automatically sending a draft', async () => {
    const clients = createDemoApplicationClients()
    clients.conversationSettingsClient.getUsage = vi.fn().mockResolvedValue(Available)
    const append = vi.spyOn(clients.conversationClient, 'appendMessage')
    const create = vi.spyOn(clients.conversationClient, 'createWithMessage')
    const user = await signIn(clients)
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Keep this draft')

    const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    fireEvent(window, new Event('offline'))
    fireEvent(window, new Event('focus'))

    expect(screen.getByText('You’re offline. Chats are paused.')).toBeVisible()
    expect(screen.getByLabelText('Message AskRabbi')).toHaveAttribute('readonly')
    expect(screen.getByRole('button', { name: 'Send message' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'New conversation' })).toBeDisabled()
    expect(screen.getByRole('link', { name: 'Read or listen to your saved Dvar Torah' })).toHaveAttribute('href', '/offline.html')
    fireEvent.keyDown(screen.getByLabelText('Message AskRabbi'), { key: 'Enter' })
    expect(append).not.toHaveBeenCalled()
    expect(create).not.toHaveBeenCalled()
    expect(clients.conversationSettingsClient.getUsage).toHaveBeenCalledTimes(1)

    online.mockReturnValue(true)
    fireEvent(window, new Event('online'))
    await waitFor(() => expect(clients.conversationSettingsClient.getUsage).toHaveBeenCalledTimes(2))
    expect(screen.getByRole('button', { name: 'Send message' })).toBeEnabled()
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('Keep this draft')
    expect(append).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    await waitFor(() => expect(append).toHaveBeenCalledTimes(1))
  })
})

async function signIn(clients: ReturnType<typeof createDemoApplicationClients>) {
  const user = userEvent.setup()
  render(<App {...clients} dvarTorahClient={dvarTorahClient} />)
  await user.click(await screen.findByRole('button', { name: 'Continue with Google' }))
  await waitFor(() => expect(screen.getByRole('button', { name: 'New conversation' })).toBeEnabled())
  await waitFor(() => expect(screen.queryByText('Preparing chat…')).not.toBeInTheDocument())
  return user
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((done) => { resolve = done })
  return { promise, resolve }
}
