import { render, screen, within } from '@testing-library/react'
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
    expect(screen.getByRole('menuitem', { name: /Personalization/ })).toBeDisabled()

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
})
