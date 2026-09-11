import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from './AuthProvider.tsx'
import { LoginPage } from './LoginPage.tsx'
import { PwaInstallProvider } from '../pwa/PwaInstall.tsx'
import { createDemoApplicationClients } from '../../test/demoApplicationClients.ts'

afterEach(cleanup)

async function renderLogin() {
  const { authClient } = createDemoApplicationClients()
  await act(async () => {
    render(<PwaInstallProvider><AuthProvider client={authClient}><LoginPage /></AuthProvider></PwaInstallProvider>)
  })
}

describe('LoginPage', () => {
  it('lets readers inspect chat storage and provider retention before signing in', async () => {
    const user = userEvent.setup()
    await renderLogin()
    const disclosure = screen.getByText('Chat history and AI privacy')
    expect(disclosure.closest('details')).not.toHaveAttribute('open')

    await user.click(disclosure)

    expect(disclosure.closest('details')).toHaveAttribute('open')
    expect(screen.getByText(/AskRabbi saves your questions and answers in your account/)).toBeVisible()
    expect(screen.getByText(/We and our service providers may retain and review questions and answers/)).toHaveTextContent('detect abuse, investigate safety issues, and protect the service')
    expect(screen.getByText(/records kept for security and abuse prevention may be retained separately/)).toBeVisible()
    expect(disclosure.closest('details')).not.toHaveTextContent(/Azure|OpenAI|Microsoft/i)
    expect(disclosure.closest('details')?.querySelector('a')).toBeNull()
    expect(screen.getByRole('button', { name: 'Continue with Google' })).toBeEnabled()

    await user.click(disclosure)
    expect(disclosure.closest('details')).not.toHaveAttribute('open')
  })

  it('omits the install action even when installation is available and focuses Google first', async () => {
    const user = userEvent.setup()
    await renderLogin()
    const prompt = vi.fn()
    const installEvent = Object.assign(new Event('beforeinstallprompt', { cancelable: true }), {
      prompt,
      userChoice: Promise.resolve({ outcome: 'dismissed' }),
    })
    act(() => { window.dispatchEvent(installEvent) })

    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeVisible()
    expect(screen.queryByRole('button', { name: /install app/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(prompt).not.toHaveBeenCalled()

    await user.tab()
    expect(screen.getByRole('button', { name: 'Continue with Google' })).toHaveFocus()
    await user.tab()
    expect(screen.getByLabelText('Email address')).toHaveFocus()
    expect(screen.getByRole('button', { name: 'Create an account' })).toBeEnabled()
  })

  it('keeps password recovery available without an install action', async () => {
    const user = userEvent.setup()
    await renderLogin()

    await user.click(screen.getByRole('button', { name: 'Forgot your password?' }))

    expect(screen.getByRole('heading', { name: 'Reset your password' })).toBeVisible()
    expect(screen.getByLabelText('Email address')).toBeEnabled()
    expect(screen.queryByRole('button', { name: /install app/i })).not.toBeInTheDocument()
  })
})
