import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'
import type { ConversationDetails } from './features/conversations/conversationData.ts'

const At = '2026-09-11T12:00:00Z'
function conversation(id: string): ConversationDetails {
  return { id, title: id === 'chicken-dairy' ? 'Chicken and dairy' : 'Shabbat and automation', enabledSourceKeys: ['collection:Torah'], createdAtUtc: At, updatedAtUtc: At, messages: [1, 2].flatMap(number => [
    { id: `${id}-q${number}`, role: 'User' as const, content: `${id} question ${number}`, createdAtUtc: At },
    { id: `${id}-a${number}`, role: 'Assistant' as const, content: `${id} answer ${number}`, createdAtUtc: At, sources: [] },
  ]) }
}

beforeEach(() => {
  localStorage.clear()
  window.history.replaceState({}, '', '/')
  Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() })
})

async function setup() {
  window.history.replaceState({}, '', '/conversations/chicken-dairy')
  const clients = createDemoApplicationClients()
  const reader = await clients.authClient.signInWithEmail('reader@example.test')
  clients.authClient.getSession = () => Promise.resolve(reader)
  const get = vi.fn(async (id: string) => conversation(id))
  clients.conversationClient.get = get
  const user = userEvent.setup()
  const mounted = render(<App {...clients} />)
  await screen.findByText('chicken-dairy answer 1')
  return { ...mounted, user, get }
}

async function openMenu(user: ReturnType<typeof userEvent.setup>, name = 'Shabbat and automation, item 2') {
  const anchor = screen.getByRole('button', { name: `Conversation actions for ${name}` })
  await user.click(anchor)
  await user.click(screen.getByRole('menuitem', { name: 'Print' }))
  return anchor
}

async function loadPreview() {
  await screen.findByRole('dialog', { name: 'Print a study copy' })
  const frame = screen.getByTitle('Study copy preview') as HTMLIFrameElement
  fireEvent.load(frame)
  await waitFor(() => expect(frame.contentDocument?.querySelector('.print-document')).not.toBeNull())
  return frame
}

describe('conversation printing entry points', () => {
  it('prints all loaded answers from the sidebar without a top-of-conversation print button or another fetch', async () => {
    const { user, get } = await setup()
    expect(screen.queryByRole('button', { name: 'Print answers' })).not.toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Print answer' })).toHaveLength(2)
    get.mockClear()
    await openMenu(user, 'Chicken and dairy, item 1')
    const frame = await loadPreview()
    expect(frame.contentDocument!.querySelectorAll('.print-answer')).toHaveLength(2)
    expect(get).not.toHaveBeenCalled()
  })

  it('loads the conversation chosen in the menu, preserves the active conversation and draft, and restores focus', async () => {
    const { user, get } = await setup()
    const draft = screen.getByRole('textbox', { name: 'Message AskRabbi' })
    await user.type(draft, 'PRIVATE UNSENT DRAFT')
    const path = window.location.pathname
    const anchor = await openMenu(user)
    const frame = await loadPreview()
    expect(get).toHaveBeenCalledWith('shabbat-automation')
    expect(frame.contentDocument!.body).toHaveTextContent('shabbat-automation answer 1')
    expect(frame.contentDocument!.body).not.toHaveTextContent('chicken-dairy answer 1')
    expect(frame.contentDocument!.body).not.toHaveTextContent('PRIVATE UNSENT DRAFT')
    expect(screen.queryByRole('menu', { name: 'Actions for Shabbat and automation' })).not.toBeInTheDocument()
    expect(window.location.pathname).toBe(path)
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancel' }))
    expect(draft).toHaveValue('PRIVATE UNSENT DRAFT')
    expect(screen.getByRole('button', { name: 'Chicken and dairy', current: 'page' })).toBeVisible()
    expect(anchor).toHaveFocus()
  })

  it('ignores a cancelled load even when a newer print preview has opened', async () => {
    const { user, get } = await setup()
    let finish: (value: ConversationDetails) => void = () => {}
    get.mockImplementationOnce(() => new Promise(resolve => { finish = resolve }))
    await openMenu(user)
    expect(await screen.findByText('Preparing print preview…')).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Cancel printing' }))
    await openMenu(user, 'Chicken and dairy, item 1')
    const frame = await loadPreview()
    await act(async () => { finish(conversation('shabbat-automation')) })
    expect(frame.contentDocument!.body).toHaveTextContent('chicken-dairy answer 1')
    expect(frame.contentDocument!.body).not.toHaveTextContent('shabbat-automation answer 1')
    expect(screen.getAllByRole('dialog')).toHaveLength(1)
  })

  it('shows a recoverable load error without navigating or opening an empty preview', async () => {
    const { user, get } = await setup()
    get.mockRejectedValueOnce(new Error('Network unavailable'))
    await openMenu(user)
    expect(await screen.findByRole('alert')).toHaveTextContent('The print preview could not open')
    expect(screen.queryByRole('dialog', { name: 'Print a study copy' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Chicken and dairy', current: 'page' })).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Dismiss' }))
    await openMenu(user)
    await loadPreview()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('does not open a delayed preview after the sidebar unmounts', async () => {
    const { user, get, unmount } = await setup()
    let finish: (value: ConversationDetails) => void = () => {}
    get.mockImplementationOnce(() => new Promise(resolve => { finish = resolve }))
    await openMenu(user)
    unmount()
    await act(async () => { finish(conversation('shabbat-automation')) })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
