import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'
import { fakeCalendarClient, Jerusalem } from './features/calendar/calendarTestData.ts'
import type { ConversationTurn } from './features/conversations/conversationClient.ts'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'

describe('Calendar navigation', () => {
  beforeEach(() => {
    Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() })
    window.sessionStorage.clear()
    window.history.replaceState({}, '', '/')
  })
  afterEach(() => vi.restoreAllMocks())

  it('keeps chats as the default and requests the calendar only when opened, below Learning & Tools', async () => {
    const { user, calendar } = await signIn()
    expect(screen.getByLabelText('Message AskRabbi')).toBeVisible()
    expect(calendar.getOverview).not.toHaveBeenCalled()
    const tools = screen.getByRole('navigation', { name: 'Learning & Tools' })
    const newChat = screen.getByRole('button', { name: 'New conversation' })
    expect(tools.compareDocumentPosition(newChat) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(within(tools).getAllByRole('button').map((item) => item.textContent)).toEqual(['This week’s Dvar Torah', 'Jewish Calendar'])
    await user.click(screen.getByRole('button', { name: 'Jewish Calendar' }))
    expect(await screen.findByText('Next holiday')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Jewish Calendar' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('button', { name: 'Chicken and dairy' })).not.toHaveAttribute('aria-current')
  })

  it('preserves the selected conversation and unsent draft through calendar navigation', async () => {
    const { user } = await signIn()
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Keep my calendar question')
    await user.click(screen.getByRole('button', { name: 'Jewish Calendar' }))
    await screen.findByText('Next holiday')
    await user.click(screen.getByRole('button', { name: 'Chicken and dairy' }))
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('Keep my calendar question')
    expect(screen.getByRole('button', { name: 'Chicken and dairy' })).toHaveAttribute('aria-current', 'page')
  })

  it('opens shared personalization from the calendar and reloads its location after saving', async () => {
    const clients = createDemoApplicationClients()
    const { user, calendar } = await signIn(clients)
    const originalOverview = calendar.getOverview
    calendar.getOverview = vi.fn(async (...args: Parameters<typeof originalOverview>) => {
      const overview = await originalOverview(...args)
      const { personalization } = await clients.conversationSettingsClient.getPersonalization()
      return { ...overview, preferences: { ...overview.preferences, location: personalization?.currentLocation?.id === Jerusalem.id ? Jerusalem : null } }
    })
    await user.click(screen.getByRole('button', { name: 'Jewish Calendar' }))
    await user.click(await screen.findByRole('button', { name: 'Edit location in Personalization' }))
    await user.selectOptions(screen.getByLabelText('Current location location type'), 'city')
    await user.selectOptions(screen.getByLabelText('Current location city'), Jerusalem.id)
    await user.click(screen.getByRole('button', { name: 'Save personalization' }))
    await screen.findByText('Saved to your account')
    await user.click(screen.getByRole('button', { name: 'Back' }))
    expect(await screen.findByText(/Jerusalem, Israel ·/)).toBeVisible()
    expect(calendar.getOverview).toHaveBeenCalledTimes(2)
    expect(calendar.getPreferences).not.toHaveBeenCalled()
    const { personalization } = await clients.conversationSettingsClient.getPersonalization()
    expect(personalization?.birthLocation?.id).toBe('91302')
  })

  it('finishes a background answer without leaving the calendar and retains it in its chat', async () => {
    const clients = createDemoApplicationClients()
    let complete: (turn: ConversationTurn) => void = () => { throw new Error('Request not started') }
    const pending = new Promise<ConversationTurn>((resolve) => { complete = resolve })
    const original = clients.conversationClient.appendMessage
    let answer: ConversationTurn | undefined
    clients.conversationClient.appendMessage = vi.fn(async (...args: Parameters<typeof original>) => { answer = await original(...args); return pending })
    const { user } = await signIn(clients)
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Why is this so?')
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    await waitFor(() => expect(answer).toBeDefined())
    await user.click(screen.getByRole('button', { name: 'Jewish Calendar' }))
    await screen.findByText('Next holiday')
    await act(async () => { if (answer) { complete(answer) } })
    expect(screen.getByRole('heading', { name: 'Jewish Calendar' })).toBeVisible()
    expect(screen.queryByLabelText('Message AskRabbi')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Chicken and dairy' }))
    expect(await screen.findByText(/local demo follow-up remains grounded/)).toBeVisible()
  })

  it('restores an unsent new-conversation draft without creating a chat', async () => {
    const clients = createDemoApplicationClients()
    const create = vi.spyOn(clients.conversationClient, 'createWithMessage')
    const { user } = await signIn(clients)
    await user.click(screen.getByRole('button', { name: 'New conversation' }))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'An unsent new question')
    await user.click(screen.getByRole('button', { name: 'Jewish Calendar' }))
    await screen.findByText('Next holiday')
    await user.click(screen.getByRole('button', { name: 'Back to conversation' }))
    expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('An unsent new question')
    expect(create).not.toHaveBeenCalled()
  })

  it('remains accessible without chat allowance and does not start generation', async () => {
    const clients = createDemoApplicationClients()
    const usage = await clients.conversationSettingsClient.getUsage()
    clients.conversationSettingsClient.getUsage = async () => ({ ...usage, tokensUsed: usage.tokenLimit, tokensRemaining: 0, usedPercent: 100, isLimitReached: true })
    const create = vi.spyOn(clients.conversationClient, 'createWithMessage')
    const append = vi.spyOn(clients.conversationClient, 'appendMessage')
    const { user } = await signIn(clients)
    expect(await screen.findByText('Monthly chat limit reached')).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Jewish Calendar' }))
    expect(await screen.findByText('Next holiday')).toBeVisible()
    expect(create).not.toHaveBeenCalled()
    expect(append).not.toHaveBeenCalled()
  })

  it('closes the mobile drawer with Escape and restores focus; calendar navigation focuses its heading', async () => {
    const { user } = await signIn()
    const open = screen.getByRole('button', { name: 'Open conversation navigation' })
    await user.click(open)
    const sidebar = screen.getByRole('complementary', { name: 'Conversation navigation' })
    expect(within(sidebar).getByRole('button', { name: 'Close conversation navigation' })).toHaveFocus()
    fireEvent.keyDown(document.activeElement!, { key: 'Escape' })
    expect(sidebar).toHaveClass('invisible')
    expect(open).toHaveFocus()
    await user.click(open)
    await user.click(screen.getByRole('button', { name: 'Jewish Calendar' }))
    await screen.findByText('Next holiday')
    expect(sidebar).toHaveClass('invisible')
    expect(screen.getByRole('heading', { name: 'Jewish Calendar' })).toHaveFocus()
  })
})

async function signIn(clients = createDemoApplicationClients()) {
  const calendar = fakeCalendarClient()
  const user = userEvent.setup()
  render(<App {...clients} calendarClient={calendar} />)
  await user.click(await screen.findByRole('button', { name: 'Continue with Google' }))
  await waitFor(() => expect(screen.getByRole('button', { name: 'New conversation' })).toBeEnabled())
  await waitFor(() => expect(screen.queryByText('Preparing chat…')).not.toBeInTheDocument())
  return { user, calendar }
}
