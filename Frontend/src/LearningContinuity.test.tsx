import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'
import { calendarOverview, fakeCalendarClient, RoshHashanah } from './features/calendar/calendarTestData.ts'
import { StarterQuestions } from './features/conversations/StarterQuestions.tsx'
import type { ConversationClient } from './features/conversations/conversationClient.ts'
import type { DvarTorahClient } from './features/dvarTorah/dvarTorahClient.ts'
import type { WeeklyDvarTorahArticle } from './features/dvarTorah/dvarTorahTypes.ts'

const Article: WeeklyDvarTorahArticle = {
  week: { weekKey: 'diaspora:2026-09-05', shabbatDate: '2026-09-05', hebrewDate: '23 Elul 5786', parashah: 'Nitzavim', holiday: null, inIsrael: false },
  title: 'Choosing life together', body: 'A lesson for us all.', sources: [], tags: [], centralTeaching: null, torahGroundingPercent: null,
  generatedAtUtc: '2026-09-04T12:00:00Z', publishedAtUtc: '2026-09-04T12:00:00Z',
}
function teachingClient(): DvarTorahClient {
  return {
    getCurrent: vi.fn().mockResolvedValue({ currentWeek: Article.week, dvarTorah: Article, isCurrentWeek: true }),
    getArchive: vi.fn().mockResolvedValue({ items: [{ week: Article.week, title: Article.title, tags: [], publishedAtUtc: Article.publishedAtUtc }], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 }),
    getArchived: vi.fn().mockResolvedValue(Article), getAudioUrl: vi.fn(), getAudioTimings: vi.fn(),
  }
}
beforeEach(() => {
  window.history.replaceState({}, '', '/')
  window.sessionStorage.clear()
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
  Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() })
})
afterEach(() => { cleanup(); vi.restoreAllMocks(); window.history.replaceState({}, '', '/') })

async function signIn(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: 'Continue with Google' }))
  await waitFor(() => expect(screen.getByRole('button', { name: 'New conversation' })).toBeEnabled())
}

describe('Learning continuity', () => {
  it('shows six starter questions using cached holidays without fetching anything', async () => {
    const user = userEvent.setup()
    const client = fakeCalendarClient()
    client.getCachedOverview = () => calendarOverview()
    const choose = vi.fn()
    render(<StarterQuestions client={client} disabled={false} onChoose={choose} />)
    expect(within(screen.getByRole('group', { name: 'Questions to explore' })).getAllByRole('button')).toHaveLength(6)
    await user.click(screen.getByRole('button', { name: 'What is Rosh Hashanah about, and how is it observed?' }))
    expect(choose).toHaveBeenCalledWith('What is Rosh Hashanah about, and how is it observed?')
    expect(client.getOverview).not.toHaveBeenCalled()
  })

  it('prepares a starter without sending or overwriting the current draft', async () => {
    window.history.replaceState({}, '', '/conversations/new')
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    const create = vi.fn(clients.conversationClient.createWithMessage)
    render(<App {...clients} conversationClient={{ ...clients.conversationClient, createWithMessage: create }} calendarClient={fakeCalendarClient()} />)
    await signIn(user)
    await user.type(screen.getByLabelText('Message AskRabbi'), 'My own thought')
    await user.click(screen.getByRole('button', { name: 'What does the Shema mean?' }))
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('My own thought\n\nWhat does the Shema mean?')
    expect(screen.getByLabelText('Message AskRabbi')).toHaveFocus()
    expect(create).not.toHaveBeenCalled()
  })

  it('restores the calendar range and search after a full remount', async () => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    const calendarClient = fakeCalendarClient()
    const first = render(<App {...clients} calendarClient={calendarClient} />)
    await signIn(user)
    await user.click(screen.getByRole('button', { name: 'Jewish Calendar' }))
    await user.click(await screen.findByRole('button', { name: '180 days' }))
    await user.type(screen.getByLabelText('Search upcoming holidays'), 'Rosh')
    expect(window.location.pathname).toBe('/calendar')
    expect(new URLSearchParams(window.location.search).get('days')).toBe('180')
    first.unmount()
    render(<App {...clients} calendarClient={calendarClient} />)
    await signIn(user)
    expect(await screen.findByRole('button', { name: '180 days' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByLabelText('Search upcoming holidays')).toHaveValue('Rosh')
    expect(screen.queryByLabelText('Message AskRabbi')).not.toBeInTheDocument()
  })

  it('restores a selected teaching directly and after visiting settings', async () => {
    window.history.replaceState({}, '', '/teachings/diaspora%3A2026-09-05')
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    const dvar = teachingClient()
    render(<App {...clients} dvarTorahClient={dvar} />)
    await signIn(user)
    expect(await screen.findByRole('heading', { name: Article.title })).toBeVisible()
    expect(dvar.getArchived).toHaveBeenCalledWith(Article.week.weekKey)
    await user.click(screen.getByRole('button', { name: 'Open profile menu' }))
    await user.click(screen.getByRole('menuitem', { name: 'Settings & Personalization' }))
    await user.click(screen.getByRole('button', { name: 'Back' }))
    expect(await screen.findByRole('heading', { name: Article.title })).toBeVisible()
    expect(window.location.pathname).toBe('/teachings/diaspora%3A2026-09-05')
  })

  it('restores an exact conversation rather than the latest one', async () => {
    window.history.replaceState({}, '', '/conversations/shabbat-automation')
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    const get = vi.fn(clients.conversationClient.get)
    render(<App {...clients} conversationClient={{ ...clients.conversationClient, get }} />)
    await signIn(user)
    expect(screen.getByRole('button', { name: 'Shabbat and automation', current: 'page' })).toBeVisible()
    expect(get).toHaveBeenCalledWith('shabbat-automation')
    expect(get).not.toHaveBeenCalledWith('chicken-dairy')
  })

  it('handles browser back and forward and restores unsent drafts per conversation', async () => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    render(<App {...clients} calendarClient={fakeCalendarClient()} />)
    await signIn(user)
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Keep this question')
    await user.click(screen.getByRole('button', { name: 'Shabbat and automation' }))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Another draft')
    act(() => { window.history.back() })
    await waitFor(() => expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('Keep this question'))
    act(() => { window.history.forward() })
    await waitFor(() => expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('Another draft'))
  })

  it('does not silently show a different conversation when a deep link is missing', async () => {
    window.history.replaceState({}, '', '/conversations/missing')
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    render(<App {...clients} conversationClient={{ ...clients.conversationClient, get: vi.fn().mockRejectedValue(new Error('Conversation not found.')) }} />)
    await signIn(user)
    expect(await screen.findByRole('alert')).toHaveTextContent('Conversation not found.')
    expect(screen.queryByRole('button', { name: 'Chicken and dairy', current: 'page' })).not.toBeInTheDocument()
  })

  it('browser Back returns to the answer whose temporary conversation ID completed in the background', async () => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    let release!: () => void
    const gate = new Promise<void>(resolve => { release = resolve })
    const create: ConversationClient['createWithMessage'] = async (...args) => { await gate; return clients.conversationClient.createWithMessage(...args) }
    render(<App {...clients} conversationClient={{ ...clients.conversationClient, createWithMessage: create }} calendarClient={fakeCalendarClient()} />)
    await signIn(user)
    await user.click(screen.getByRole('button', { name: 'New conversation' }))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Keep the pending page')
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    await user.click(screen.getByRole('button', { name: 'Jewish Calendar' }))
    await screen.findByRole('heading', { name: 'Jewish Calendar' })
    await act(async () => release())
    await screen.findByText('Answer ready')
    act(() => window.history.back())
    expect(await screen.findByText(/this local demo represents/)).toBeVisible()
    expect(window.location.pathname).toMatch(/^\/conversations\/(?!new)/)
  })

  it.each(['calendar', 'teaching'])('announces a background answer in the %s without moving the reader, retaining the dot after dismissal', async destination => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    let release!: () => void
    const gate = new Promise<void>(resolve => { release = resolve })
    const create: ConversationClient['createWithMessage'] = async (...args) => { await gate; return clients.conversationClient.createWithMessage(...args) }
    render(<App {...clients} conversationClient={{ ...clients.conversationClient, createWithMessage: create }} calendarClient={fakeCalendarClient()} dvarTorahClient={teachingClient()} />)
    await signIn(user)
    await user.click(screen.getByRole('button', { name: 'New conversation' }))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'A new question')
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    await user.click(screen.getByRole('button', { name: destination === 'calendar' ? 'Jewish Calendar' : 'This week’s Dvar Torah' }))
    const heading = await screen.findByRole('heading', { name: destination === 'calendar' ? 'Jewish Calendar' : Article.title })
    heading.focus()
    const path = window.location.pathname
    await act(async () => release())
    expect(await screen.findByText('Answer ready')).toBeVisible()
    expect(window.location.pathname).toBe(path)
    expect(heading).toBeVisible()
    expect(screen.getByRole('img', { name: 'Unread answer for A new question' })).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Dismiss answer ready notification' }))
    expect(screen.getByRole('img', { name: 'Unread answer for A new question' })).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'A new question' }))
    expect(await screen.findByText(/this local demo represents/)).toBeVisible()
    expect(screen.queryByRole('img', { name: /Unread answer/ })).not.toBeInTheDocument()
    expect(window.location.pathname).toMatch(/^\/conversations\/(?!new)/)
  })

  it('prepares the exact holiday reference and dates while preserving a draft', async () => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    const append = vi.fn(clients.conversationClient.appendMessage)
    render(<App {...clients} conversationClient={{ ...clients.conversationClient, appendMessage: append }} calendarClient={fakeCalendarClient()} />)
    await signIn(user)
    await user.type(screen.getByLabelText('Message AskRabbi'), 'My unfinished question')
    await user.click(screen.getByRole('button', { name: 'Jewish Calendar' }))
    const highlight = await screen.findByRole('region', { name: 'Rosh Hashanah' })
    await user.click(within(highlight).getByRole('button', { name: 'Ask about this: Rosh Hashanah' }))
    const draft = screen.getByLabelText('Message AskRabbi') as HTMLTextAreaElement
    expect(draft.value).toContain('My unfinished question\n\n')
    expect(draft.value).toContain(RoshHashanah.sourceUrl)
    expect(draft.value).toContain(RoshHashanah.startDate)
    expect(draft).toHaveFocus()
    expect(append).not.toHaveBeenCalled()
  })

  it('prepares a source reference without sending and closes the source reader', async () => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    const append = vi.fn(clients.conversationClient.appendMessage)
    render(<App {...clients} conversationClient={{ ...clients.conversationClient, appendMessage: append }} />)
    await signIn(user)
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Explain this')
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    await user.click(await screen.findByRole('button', { name: 'View source 1' }))
    await user.click(screen.getAllByRole('button', { name: 'Ask about this: Mishnah Chullin 8:1' })[0])
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('Can you explain Mishnah Chullin 8:1 and its context?\nSource: https://www.sefaria.org/Mishnah_Chullin.8.1')
    expect(screen.queryByRole('heading', { name: 'Source reader' })).not.toBeInTheDocument()
    expect(append).toHaveBeenCalledOnce()
  })

  it('never truncates an existing long draft to insert a question', async () => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    render(<App {...clients} />)
    await signIn(user)
    fireEvent.change(screen.getByLabelText('Message AskRabbi'), { target: { value: 'x'.repeat(3990) } })
    await user.click(screen.getByRole('button', { name: 'What does the Shema mean?' }))
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('x'.repeat(3990))
    expect(screen.getByText(/Shorten it first; your text has been kept/)).toBeVisible()
  })
})
