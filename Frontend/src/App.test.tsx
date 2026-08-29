import { act, fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import App from './App.tsx'
import type { ConversationClient } from './features/conversations/conversationClient.ts'
import type { ConversationDetails } from './features/conversations/conversationData.ts'
import { ConversationStarters } from './features/conversations/conversationStarters.ts'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'

const ConversationStarterHeadings = ConversationStarters.map((starter) => starter.heading)

describe('App', () => {
  beforeEach(() => {
    window.sessionStorage.clear()
    window.history.replaceState({}, '', '/')
  })

  it('validates email before starting a session', async () => {
    const user = userEvent.setup()
    await renderApp()

    await user.click(screen.getByRole('button', { name: 'Continue with email' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Enter a valid email address')
    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeVisible()
  })

  it('requests password recovery without exposing account existence', async () => {
    const user = userEvent.setup()
    await renderApp()

    await user.click(screen.getByRole('button', { name: 'Forgot your password?' }))
    await user.type(screen.getByLabelText('Email address'), 'amitai@example.com')
    await user.click(screen.getByRole('button', { name: 'Send reset link' }))

    expect(await screen.findByRole('button', { name: 'Reset email requested' })).toBeDisabled()
  })

  it('confirms a password reset from the WorkOS reset route', async () => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    window.history.replaceState({}, '', '/reset-password?token=workos-reset-token')
    render(<App authClient={clients.authClient} conversationClient={clients.conversationClient} conversationSettingsClient={clients.conversationSettingsClient} />)

    await user.type(await screen.findByLabelText('New password'), 'LongEnoughPassword!42')
    await user.type(screen.getByLabelText('Confirm new password'), 'LongEnoughPassword!42')
    await user.click(screen.getByRole('button', { name: 'Update password' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Your password was updated')
  })

  it('supports the Google login and logout flow', async () => {
    const user = userEvent.setup()
    await renderApp()

    expect(screen.queryByRole('button', { name: 'Continue with WorkOS' })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    await expectConversationStarter()

    await user.click(screen.getByRole('button', { name: 'Open profile menu' }))
    expect(screen.getByRole('menuitem', { name: 'Settings' })).toBeEnabled()
    expect(screen.getByRole('menuitem', { name: 'Personalization' })).toBeEnabled()

    await user.click(screen.getByRole('menuitem', { name: 'Log out' }))
    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeVisible()
  })

  it('requires new users to complete personalization before entering chat', async () => {
    const user = userEvent.setup()
    await renderApp()

    await user.click(screen.getByRole('button', { name: 'Create an account' }))

    expect(await screen.findByRole('heading', { name: /Welcome, Amitai.*Let’s make AskRabbi yours/ })).toBeVisible()
    expect(screen.queryByLabelText('Message AskRabbi')).not.toBeInTheDocument()
    expect(screen.getByText('Step 1 of 3')).toBeVisible()
    expect(screen.getByLabelText(/Birth time zone/)).toHaveValue('')

    await user.click(screen.getByRole('button', { name: 'Continue' }))

    expect(screen.getByText('Enter your birth date and time.')).toBeVisible()
    expect(screen.getByText('Choose the U.S. time zone where you were born.')).toBeVisible()
    expect(screen.queryByRole('heading', { name: 'Choose your languages.' })).not.toBeInTheDocument()

    const fullName = screen.getByLabelText(/Full name/)
    await user.clear(fullName)
    await user.type(fullName, 'Amitai Ben Erfanian')
    fireEvent.input(screen.getByLabelText(/Birth date and time/), { target: { value: '2001-12-17T09:30' } })
    await user.selectOptions(screen.getByLabelText(/Birth time zone/), 'America/Los_Angeles')
    await user.click(screen.getByRole('button', { name: 'Continue' }))

    expect(screen.getByRole('heading', { name: 'Choose your languages.' })).toBeVisible()
    await user.selectOptions(screen.getByLabelText(/Conversation language/), 'Persian')
    await user.selectOptions(screen.getByLabelText(/Torah and source quotations/), 'Hebrew')
    await user.click(screen.getByRole('button', { name: 'Continue' }))

    expect(screen.getByRole('heading', { name: 'Your Jewish background.' })).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Start a conversation' }))
    expect(screen.getByText('Choose the background that fits best.')).toBeVisible()
    expect(screen.getByText('Choose the heritage or community that fits best.')).toBeVisible()
    expect(screen.queryByLabelText('Message AskRabbi')).not.toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText(/Religious movement or practice/), 'Conservadox')
    await user.selectOptions(screen.getByLabelText(/Heritage or community/), 'Mizrahi')
    await user.type(screen.getByLabelText('Anything else?'), 'My family is Iranian Jewish.')
    await user.click(screen.getByRole('button', { name: 'Start a conversation' }))

    await expectConversationStarter()
    expect(screen.getByText('Persian · quotes in Hebrew')).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Open profile menu' }))
    expect(screen.getByText('Amitai Ben Erfanian')).toBeVisible()
  })

  it('creates a conversation and stores a message through the client boundary', async () => {
    const user = userEvent.setup()
    await renderApp()

    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    await expectConversationStarter()
    await user.click(screen.getByRole('button', { name: 'New conversation' }))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Why do Jewish customs differ?')
    await user.click(screen.getByRole('button', { name: 'Send message' }))

    expect(within(screen.getByRole('article')).getByText('Why do Jewish customs differ?')).toBeVisible()
    expect(await screen.findByText(/validated grounded response returned by the production API/)).toBeVisible()
  })

  it('ignores a stale initial conversation response after another conversation is selected', async () => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    const firstConversation = await clients.conversationClient.get('chicken-dairy')
    const secondConversation = await clients.conversationClient.get('shabbat-automation')
    const initialLoad = createDeferred<ConversationDetails>()
    const conversationClient: ConversationClient = {
      ...clients.conversationClient,
      get(conversationId) {
        if (conversationId === firstConversation.id) {
          return initialLoad.promise
        }
        if (conversationId === secondConversation.id) {
          return Promise.resolve({
            ...secondConversation,
            messages: [{ id: 'second-message', role: 'User', content: 'Question from the selected conversation', createdAtUtc: secondConversation.createdAtUtc }],
          })
        }
        return clients.conversationClient.get(conversationId)
      },
    }
    render(<App authClient={clients.authClient} conversationClient={conversationClient} conversationSettingsClient={clients.conversationSettingsClient} />)

    await user.click(await screen.findByRole('button', { name: 'Continue with Google' }))
    await user.click(await screen.findByRole('button', { name: 'Shabbat and automation' }))
    await act(async () => {
      initialLoad.resolve({
        ...firstConversation,
        messages: [{ id: 'first-message', role: 'User', content: 'Question from the stale conversation', createdAtUtc: firstConversation.createdAtUtc }],
      })
      await initialLoad.promise
    })

    expect(await screen.findByText('Question from the selected conversation')).toBeVisible()
    expect(screen.queryByText('Question from the stale conversation')).not.toBeInTheDocument()
  })

  it('filters the approved source set for each conversation', async () => {
    const user = userEvent.setup()
    await renderApp()

    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    await expectConversationStarter()
    await user.click(screen.getByRole('button', { name: /Choose sources:/ }))

    const sourceDialog = screen.getByRole('dialog', { name: 'Sources used for this conversation' })
    expect(within(sourceDialog).getAllByRole('checkbox')).toHaveLength(10)
    expect(within(sourceDialog).getAllByRole('checkbox').every((checkbox) => checkbox.hasAttribute('checked'))).toBe(true)

    await user.click(within(sourceDialog).getByRole('button', { name: 'Clear all sources' }))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'What do these sources say about Shabbat?')
    expect(screen.getByRole('button', { name: 'Send message' })).toBeDisabled()
    expect(screen.getByRole('alert')).toHaveTextContent('Select at least one source before sending.')

    await user.click(screen.getByRole('button', { name: 'Choose sources: Choose sources' }))
    const reopenedSourceDialog = screen.getByRole('dialog', { name: 'Sources used for this conversation' })
    await user.click(within(reopenedSourceDialog).getByRole('checkbox', { name: 'Torah' }))
    await user.click(within(reopenedSourceDialog).getByRole('checkbox', { name: 'Talmud' }))
    await user.click(screen.getByRole('button', { name: 'Choose sources: 2 sources' }))
    await user.click(screen.getByRole('button', { name: 'Send message' }))

    expect(await screen.findByText(/validated grounded response returned by the production API/)).toBeVisible()
    expect(screen.getByRole('button', { name: 'Choose sources: 2 sources' })).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'New conversation' }))
    expect(screen.getByRole('button', { name: 'Choose sources: All sources' })).toBeVisible()
  })

  it('advances the welcome prompt only when a new conversation starts', async () => {
    const user = userEvent.setup()
    await renderApp()

    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    const initialHeading = (await expectConversationStarter()).textContent

    await user.click(screen.getByRole('button', { name: 'New conversation' }))

    const nextHeading = (await expectConversationStarter()).textContent
    expect(nextHeading).not.toBe(initialHeading)
  })

  it('renames and confirms deletion of a conversation from its actions menu', async () => {
    const user = userEvent.setup()
    await renderApp()

    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    await user.click(await screen.findByRole('button', { name: 'Conversation actions for Chicken and dairy, item 1' }))
    await user.click(screen.getByRole('menuitem', { name: 'Rename' }))

    const renameInput = screen.getByLabelText('Rename Chicken and dairy')
    await user.clear(renameInput)
    await user.type(renameInput, 'Kashrut basics')
    await user.click(screen.getByRole('button', { name: 'Save conversation name' }))
    expect(screen.getByText('Kashrut basics')).toBeVisible()

    await user.click(screen.getByRole('button', { name: 'Conversation actions for Kashrut basics, item 1' }))
    await user.click(screen.getByRole('menuitem', { name: 'Delete' }))
    const confirmation = screen.getByRole('dialog', { name: 'Delete Kashrut basics' })
    expect(within(confirmation).getByText('Delete this conversation?')).toBeVisible()
    await user.click(within(confirmation).getByRole('button', { name: 'Delete' }))

    expect(screen.queryByText('Kashrut basics')).not.toBeInTheDocument()
  })

  it('captures and saves personalization to the account client', async () => {
    const user = userEvent.setup()
    await renderApp()

    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    await user.click(await screen.findByRole('button', { name: 'Open profile menu' }))
    await user.click(screen.getByRole('menuitem', { name: 'Personalization' }))

    expect(screen.getByRole('heading', { name: 'Make AskRabbi yours.' })).toBeVisible()
    expect(screen.getByLabelText('Conversation language')).toHaveValue('English')
    expect(screen.getByLabelText('Torah and source quotations')).toHaveValue('English')

    fireEvent.input(screen.getByLabelText('Birth date and time'), { target: { value: '' } })
    await user.selectOptions(screen.getByLabelText('Birth time zone'), '')
    await user.selectOptions(screen.getByLabelText('Religious movement or practice'), '')
    await user.selectOptions(screen.getByLabelText('Heritage or community'), '')
    await user.click(screen.getByRole('button', { name: 'Save personalization' }))
    expect(screen.getByText('Enter your birth date and time.')).toBeVisible()
    expect(screen.getByText('Choose the U.S. time zone where you were born.')).toBeVisible()
    expect(screen.getByText('Choose the background that fits best.')).toBeVisible()
    expect(screen.getByText('Choose the heritage or community that fits best.')).toBeVisible()

    const fullName = screen.getByLabelText('Full name')
    await user.clear(fullName)
    await user.type(fullName, 'Amitai Ben Erfanian')
    fireEvent.input(screen.getByLabelText('Birth date and time'), { target: { value: '2001-12-17T09:30' } })
    await user.selectOptions(screen.getByLabelText('Birth time zone'), 'America/Los_Angeles')
    await user.selectOptions(screen.getByLabelText('Conversation language'), 'Persian')
    await user.selectOptions(screen.getByLabelText('Torah and source quotations'), 'Hebrew')
    await user.selectOptions(screen.getByLabelText('Religious movement or practice'), 'Conservadox')
    await user.selectOptions(screen.getByLabelText('Heritage or community'), 'Mizrahi')
    await user.type(screen.getByLabelText('Additional information'), 'My family is Iranian Jewish, and I appreciate explanations that compare customs.')
    await user.click(screen.getByRole('button', { name: 'Save personalization' }))

    expect(screen.getByRole('status')).toHaveTextContent('Saved to your account')
    expect(screen.getByRole('status')).toHaveTextContent('future conversations')
    expect(screen.getByText('Amitai Ben Erfanian')).toBeVisible()

    await user.click(screen.getByRole('button', { name: 'Back to conversation' }))
    await expectConversationStarter()
    expect(screen.getByText('Persian · quotes in Hebrew')).toBeVisible()
  })

  it('shows account usage and handles settings actions', async () => {
    const user = userEvent.setup()
    await renderApp()

    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    await user.click(await screen.findByRole('button', { name: 'Open profile menu' }))
    await user.click(screen.getByRole('menuitem', { name: 'Settings' }))

    expect(screen.getByRole('heading', { name: 'Account and usage.' })).toBeVisible()
    expect(screen.getByText('amitai@example.com')).toBeVisible()
    expect(screen.getByRole('progressbar', { name: 'Monthly grounded answer usage' })).toHaveAttribute('aria-valuenow', '0')

    await user.click(screen.getByRole('button', { name: 'Reset password' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Password reset requested')
    expect(screen.getByRole('status')).toHaveTextContent('secure reset email')

    const productUpdates = screen.getByRole('switch', { name: 'Email me product updates' })
    expect(productUpdates).toHaveAttribute('aria-checked', 'false')
    await user.click(productUpdates)
    expect(productUpdates).toHaveAttribute('aria-checked', 'true')

    await user.click(screen.getByRole('button', { name: 'Save settings' }))
    expect(screen.getByRole('status')).toHaveTextContent('Settings saved')
  })
})

async function renderApp() {
  const clients = createDemoApplicationClients()
  render(<App authClient={clients.authClient} conversationClient={clients.conversationClient} conversationSettingsClient={clients.conversationSettingsClient} />)
  expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeVisible()
}

async function expectConversationStarter() {
  const heading = await screen.findByRole('heading', { level: 1 })
  expect(ConversationStarterHeadings).toContain(heading.textContent)
  expect(heading).toBeVisible()
  return heading
}

function createDeferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise
  })
  return { promise, resolve }
}
