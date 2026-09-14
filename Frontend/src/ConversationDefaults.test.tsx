import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'
import type { ConversationClient } from './features/conversations/conversationClient.ts'
import type { ConversationDetails } from './features/conversations/conversationData.ts'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'

describe('Default conversation navigation', () => {
  afterEach(() => vi.restoreAllMocks())
  beforeEach(() => {
    window.history.replaceState({}, '', '/')
    window.sessionStorage.clear()
    Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() })
  })

  it.each(['/', '/conversations/new'])('opens a fresh unsaved chat at %s without loading a saved conversation', async path => {
    vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockReturnValue(1500)
    window.history.replaceState({}, '', path)
    const { conversationClient } = await renderSignedIn()

    await expectNewPage()

    expect(conversationClient.get).not.toHaveBeenCalled()
    expect(conversationClient.createWithMessage).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Chicken and dairy' })).toBeVisible()
    expect(HTMLElement.prototype.scrollTo).toHaveBeenLastCalledWith({ top: 0, behavior: 'auto' })
  })

  it('still restores an explicitly linked saved conversation', async () => {
    window.history.replaceState({}, '', '/conversations/shabbat-automation')
    const { conversationClient } = await renderSignedIn()

    expect(await screen.findByRole('button', { name: 'Shabbat and automation', current: 'page' })).toBeVisible()
    await waitFor(() => expect(screen.getByRole('button', { name: 'New conversation' })).toBeEnabled())
    expect(conversationClient.get).toHaveBeenCalledWith('shabbat-automation')
    expect(window.location.pathname).toBe('/conversations/shabbat-automation')
  })

  it.each([false, true])('returns to a blank new chat after deleting the selected chat (last chat: %s)', async lastChat => {
    const clients = createDemoApplicationClients()
    const summaries = await clients.conversationClient.list()
    const overrides = lastChat ? { list: async () => summaries.slice(0, 1) } : {}
    window.history.replaceState({}, '', '/conversations/chicken-dairy')
    const { user, conversationClient } = await renderSignedIn(overrides, clients)
    await waitFor(() => expect(screen.getByRole('button', { name: 'New conversation' })).toBeEnabled())
    await user.type(screen.getByLabelText('Message AskRabbi'), 'This draft belongs to the deleted chat')
    const historyLength = window.history.length
    vi.mocked(conversationClient.get).mockClear()

    await deleteChat(user, 'Chicken and dairy', 1)

    await expectNewPage()
    expect(conversationClient.delete).toHaveBeenCalledWith('chicken-dairy')
    expect(conversationClient.get).not.toHaveBeenCalled()
    expect(conversationClient.createWithMessage).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: 'Chicken and dairy' })).not.toBeInTheDocument()
    expect(window.history.length).toBe(historyLength)
    if (!lastChat) { expect(screen.getByRole('button', { name: 'Shabbat and automation' })).toBeVisible() }
  })

  it('opens a new chat when deleting another sidebar chat and preserves the surviving draft', async () => {
    window.history.replaceState({}, '', '/conversations/chicken-dairy')
    const { user } = await renderSignedIn()
    await waitFor(() => expect(screen.getByRole('button', { name: 'New conversation' })).toBeEnabled())
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Keep this surviving draft')

    await deleteChat(user, 'Shabbat and automation', 2)

    await expectNewPage()
    await user.click(screen.getByRole('button', { name: 'Chicken and dairy' }))
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('Keep this surviving draft')
  })

  it('keeps the current page and draft when deletion fails', async () => {
    window.history.replaceState({}, '', '/conversations/chicken-dairy')
    const { user } = await renderSignedIn({ delete: async () => { throw new Error('Deletion failed. Try again.') } })
    await waitFor(() => expect(screen.getByRole('button', { name: 'New conversation' })).toBeEnabled())
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Keep this draft after a failure')

    await deleteChat(user, 'Chicken and dairy', 1)

    const dialog = screen.getByRole('dialog', { name: 'Delete "Chicken and dairy" Conversation?' })
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Deletion failed. Try again.')
    expect(window.location.pathname).toBe('/conversations/chicken-dairy')
    expect(screen.getByRole('button', { name: 'Chicken and dairy', current: 'page' })).toBeVisible()
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('Keep this draft after a failure')
  })

  it.each([false, true])('ignores the deleted chat when its initial load finishes late (failure: %s)', async fails => {
    const clients = createDemoApplicationClients()
    const details = await clients.conversationClient.get('chicken-dairy')
    let release!: (value: ConversationDetails) => void
    let reject!: (error: Error) => void
    const pending = new Promise<ConversationDetails>((resolve, rejectPromise) => { release = resolve; reject = rejectPromise })
    window.history.replaceState({}, '', '/conversations/chicken-dairy')
    const { user, conversationClient } = await renderSignedIn({ get: () => pending }, clients)
    await waitFor(() => expect(conversationClient.get).toHaveBeenCalled())

    await deleteChat(user, 'Chicken and dairy', 1)
    await expectNewPage()
    await act(async () => {
      if (fails) {
        reject(new Error('The deleted conversation no longer exists.'))
        await expect(pending).rejects.toThrow('The deleted conversation no longer exists.')
      } else {
        release({ ...details, messages: [{ id: 'late', role: 'Assistant', content: 'A stale answer from the deleted chat', createdAtUtc: details.createdAtUtc }] })
        await pending
      }
    })

    await expectNewPage()
    expect(screen.queryByText('A stale answer from the deleted chat')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Chicken and dairy' })).not.toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})

async function renderSignedIn(overrides: Partial<ConversationClient> = {}, clients = createDemoApplicationClients()) {
  const user = userEvent.setup()
  const authenticatedUser = await clients.authClient.signInWithSocialProvider('google')
  const conversationClient: ConversationClient = {
    ...clients.conversationClient,
    ...overrides,
    get: vi.fn(overrides.get ?? clients.conversationClient.get),
    delete: vi.fn(overrides.delete ?? clients.conversationClient.delete),
    createWithMessage: vi.fn(clients.conversationClient.createWithMessage),
  }
  render(<App {...clients} authClient={{ ...clients.authClient, getSession: async () => authenticatedUser }} conversationClient={conversationClient} />)
  await screen.findByRole('navigation', { name: 'Recent conversations' })
  return { user, conversationClient }
}

async function expectNewPage() {
  await waitFor(() => expect(window.location.pathname).toBe('/conversations/new'))
  await waitFor(() => expect(screen.getByRole('button', { name: 'New conversation' })).toBeEnabled())
  expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('')
  expect(screen.getByRole('button', { name: 'Choose sources: All sources' })).toBeVisible()
  expect(within(screen.getByRole('navigation', { name: 'Recent conversations' })).queryByRole('button', { current: 'page' })).not.toBeInTheDocument()
}

async function deleteChat(user: ReturnType<typeof userEvent.setup>, title: string, item: number) {
  await user.click(screen.getByRole('button', { name: `Conversation actions for ${title}, item ${item}` }))
  await user.click(screen.getByRole('menuitem', { name: 'Delete' }))
  await user.click(within(screen.getByRole('dialog', { name: `Delete "${title}" Conversation?` })).getByRole('button', { name: 'Delete' }))
}
