import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'
import type { WeeklyDvarTorahArticle } from './features/dvarTorah/dvarTorahTypes.ts'
import type { DvarTorahClient } from './features/dvarTorah/dvarTorahClient.ts'
import { ApiError } from './api/apiClient.ts'

const Article: WeeklyDvarTorahArticle = {
  week: { weekKey: 'diaspora:2026-08-29', shabbatDate: '2026-08-29', hebrewDate: '16 Elul 5786', parashah: 'Ki Tavo', holiday: null, inIsrael: false },
  title: 'Choosing responsibility', body: 'Choose life and care for others.\n\nThe whole community matters.', sources: [], tags: [], centralTeaching: null, torahGroundingPercent: null,
  generatedAtUtc: '2026-08-24T12:00:00Z', publishedAtUtc: '2026-08-24T12:00:00Z',
}
function client(): DvarTorahClient {
  return { getReadState: async () => ({ readWeekKeys: [] }), setReadState: vi.fn(), getCurrent: async () => ({ currentWeek: Article.week, dvarTorah: Article, isCurrentWeek: true }), getArchive: async () => ({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 }), getArchived: async () => Article, getAudioUrl: vi.fn(), getAudioTimings: vi.fn() }
}
beforeEach(() => { window.history.replaceState({}, '', '/conversations/shabbat-automation'); vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true); Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() }) })
afterEach(() => { cleanup(); vi.restoreAllMocks(); window.getSelection()?.removeAllRanges(); window.history.replaceState({}, '', '/') })

describe('Teaching conversations', () => {
  it.each([false, true])('starts a new conversation with full-teaching context (selected passage: %s)', async selected => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    const create = vi.fn(clients.conversationClient.createWithMessage)
    const append = vi.fn(clients.conversationClient.appendMessage)
    render(<App {...clients} conversationClient={{ ...clients.conversationClient, createWithMessage: create, appendMessage: append }} dvarTorahClient={client()} />)
    await user.click(await screen.findByRole('button', { name: 'Continue with Google' }))
    await waitFor(() => expect(screen.getByLabelText('Message AskRabbi')).toBeEnabled())
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Keep my other draft')
    await user.click(screen.getByRole('button', { name: 'This week’s Dvar Torah' }))
    await screen.findByRole('heading', { name: Article.title })
    if (selected) {
      const range = document.createRange()
      range.selectNodeContents(screen.getByText('Choose life and care for others.'))
      window.getSelection()?.removeAllRanges()
      window.getSelection()?.addRange(range)
      fireEvent(document, new Event('selectionchange'))
      await user.click(await screen.findByRole('button', { name: 'Ask about this passage' }))
    } else {
      await user.click(screen.getByRole('button', { name: 'Ask about this teaching' }))
    }
    expect(window.location.pathname).toBe('/conversations/new')
    expect(create).not.toHaveBeenCalled()
    expect(append).not.toHaveBeenCalled()
    expect(screen.getByLabelText('Teaching context')).toHaveTextContent(Article.title)
    expect(screen.getByLabelText('Message AskRabbi')).toHaveFocus()
    await user.clear(screen.getByLabelText('Message AskRabbi'))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'How can I put this into practice?')
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    await waitFor(() => expect(create).toHaveBeenCalledWith(expect.any(String), 'How can I put this into practice?', expect.any(Array), { weekKey: Article.week.weekKey, selectedText: selected ? 'Choose life and care for others.' : null }))
    expect(append).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Shabbat and automation' }))
    expect(await screen.findByLabelText('Message AskRabbi')).toHaveValue('Keep my other draft')
    expect(screen.queryByLabelText('Teaching context')).not.toBeInTheDocument()
  })

  it('allows removing context and does not leak it into an unrelated new conversation', async () => {
    window.history.replaceState({}, '', '/teachings/diaspora%3A2026-08-29')
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    const create = vi.fn(clients.conversationClient.createWithMessage)
    render(<App {...clients} conversationClient={{ ...clients.conversationClient, createWithMessage: create }} dvarTorahClient={client()} />)
    await user.click(await screen.findByRole('button', { name: 'Continue with Google' }))
    await user.click(await screen.findByRole('button', { name: 'Ask about this teaching' }))
    await user.click(screen.getByRole('button', { name: 'Remove teaching context' }))
    expect(screen.queryByLabelText('Teaching context')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    await waitFor(() => expect(create).toHaveBeenCalledTimes(1))
    expect(create.mock.calls[0]).toHaveLength(3)
    await user.click(screen.getByRole('button', { name: 'New conversation' }))
    expect(screen.queryByLabelText('Teaching context')).not.toBeInTheDocument()
  })

  it.each([400, 404])('keeps the question editable and context removable after a rejected teaching (%s)', async status => {
    window.history.replaceState({}, '', '/teachings')
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    const create = vi.fn(clients.conversationClient.createWithMessage).mockRejectedValueOnce(new ApiError(status, { detail: 'The teaching selection is no longer available.' }))
    render(<App {...clients} conversationClient={{ ...clients.conversationClient, createWithMessage: create }} dvarTorahClient={client()} />)
    await user.click(await screen.findByRole('button', { name: 'Continue with Google' }))
    await user.click(await screen.findByRole('button', { name: 'Ask about this teaching' }))
    await user.clear(screen.getByLabelText('Message AskRabbi'))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Keep this question')
    await user.click(screen.getByRole('button', { name: 'Send message' }))

    await screen.findByText('The teaching selection is no longer available.')
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('Keep this question')
    expect(window.history.state?.askarabbiPendingId).toBeUndefined()
    await user.click(screen.getByRole('button', { name: 'Remove teaching context' }))
    await user.click(screen.getByRole('button', { name: 'Send message' }))

    await waitFor(() => expect(create).toHaveBeenCalledTimes(2))
    expect(create.mock.calls[1]).toHaveLength(3)
  })
})
