import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from './App.tsx'

describe('App', () => {
  it('validates email before starting a demo session', async () => {
    const user = userEvent.setup()
    render(<App />)

    await user.click(screen.getByRole('button', { name: 'Continue with email' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Enter a valid email address')
    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeVisible()
  })

  it('supports the demo Google login and logout flow', async () => {
    const user = userEvent.setup()
    render(<App />)

    expect(screen.queryByRole('button', { name: 'Continue with WorkOS' })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    expect(await screen.findByRole('heading', { name: 'What shall we study together?' })).toBeVisible()

    await user.click(screen.getByRole('button', { name: 'Open profile menu' }))
    expect(screen.getByRole('menuitem', { name: /Settings/ })).toBeDisabled()
    expect(screen.getByRole('menuitem', { name: 'Personalization' })).toBeEnabled()

    await user.click(screen.getByRole('menuitem', { name: 'Log out' }))
    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeVisible()
  })

  it('creates a local conversation without calling a backend', async () => {
    const user = userEvent.setup()
    render(<App />)

    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    await user.click(screen.getByRole('button', { name: 'New conversation' }))
    await user.type(screen.getByLabelText('Message AskRabbi'), 'Why do Jewish customs differ?')
    await user.click(screen.getByRole('button', { name: 'Send message' }))

    expect(within(screen.getByRole('article')).getByText('Why do Jewish customs differ?')).toBeVisible()
    expect(screen.getByText(/ready for the grounded-answer API/)).toBeVisible()
  })

  it('captures and saves personalization for the current session', async () => {
    const user = userEvent.setup()
    render(<App />)

    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    await user.click(screen.getByRole('button', { name: 'Open profile menu' }))
    await user.click(screen.getByRole('menuitem', { name: 'Personalization' }))

    expect(screen.getByRole('heading', { name: 'Help us meet you where you are.' })).toBeVisible()

    await user.click(screen.getByRole('button', { name: 'Save personalization' }))
    expect(screen.getByText('Enter your birth date and time.')).toBeVisible()
    expect(screen.getByText('Choose the background that fits best.')).toBeVisible()
    expect(screen.getByText('Choose the heritage or community that fits best.')).toBeVisible()

    const fullName = screen.getByLabelText('Full name')
    await user.clear(fullName)
    await user.type(fullName, 'Amitai Ben Erfanian')
    fireEvent.change(screen.getByLabelText('Birth date and time'), { target: { value: '2001-12-17T09:30' } })
    await user.type(screen.getByLabelText('Birthplace'), 'Los Angeles, United States')
    const timeZone = screen.getByLabelText('Birth time zone')
    await user.clear(timeZone)
    await user.type(timeZone, 'America/Los_Angeles')
    await user.selectOptions(screen.getByLabelText('Religious movement or practice'), 'Conservadox')
    await user.selectOptions(screen.getByLabelText('Heritage or community'), 'Mizrahi')
    await user.type(screen.getByLabelText('Additional information'), 'My family is Iranian Jewish, and I appreciate explanations that compare customs.')
    await user.click(screen.getByRole('button', { name: 'Save personalization' }))

    expect(screen.getByRole('status')).toHaveTextContent('Saved for this session')
    expect(screen.getByText('Amitai Ben Erfanian')).toBeVisible()

    await user.click(screen.getByRole('button', { name: 'Back to conversation' }))
    expect(screen.getByRole('heading', { name: 'What shall we study together?' })).toBeVisible()
  })
})
