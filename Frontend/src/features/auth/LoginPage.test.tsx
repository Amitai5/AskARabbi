import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from './AuthProvider.tsx'
import { LoginPage } from './LoginPage.tsx'
import { PwaInstallProvider } from '../pwa/PwaInstall.tsx'
import { createDemoApplicationClients } from '../../test/demoApplicationClients.ts'

afterEach(() => {
  cleanup()
  window.history.replaceState(null, '', '/')
})

async function renderLogin() {
  const { authClient } = createDemoApplicationClients()
  await act(async () => {
    render(<PwaInstallProvider><AuthProvider client={authClient}><LoginPage /></AuthProvider></PwaInstallProvider>)
  })
}

describe('LoginPage', () => {
  it('shows a support link when full while keeping existing-account sign-in and recovery available', async () => {
    const clients = createDemoApplicationClients()
    clients.authClient.getRegistrationAvailability = vi.fn().mockResolvedValue({ isOpen: false })
    const signUp = vi.spyOn(clients.authClient, 'signUp')
    const user = userEvent.setup()
    await act(async () => { render(<AuthProvider client={clients.authClient}><LoginPage /></AuthProvider>) })

    expect(screen.getByText('Registration is currently full')).toBeVisible()
    expect(screen.getByRole('link', { name: 'support@askarabbi.ai' })).toHaveAttribute('href', 'mailto:support@askarabbi.ai')
    expect(screen.queryByRole('button', { name: 'Create an account' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Continue with Google' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Continue with email' })).toBeEnabled()
    await user.click(screen.getByRole('button', { name: 'Forgot your password?' }))
    expect(screen.getByRole('heading', { name: 'Reset your password' })).toBeVisible()
    expect(signUp).not.toHaveBeenCalled()
  })

  it('preserves the server capacity-denial message if the availability refresh fails', async () => {
    window.history.replaceState(null, '', '/?registration=closed')
    const clients = createDemoApplicationClients()
    clients.authClient.getRegistrationAvailability = vi.fn().mockRejectedValue(new Error('Offline'))
    await act(async () => { render(<AuthProvider client={clients.authClient}><LoginPage /></AuthProvider>) })

    expect(screen.getByText('Registration is currently full')).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Create an account' })).not.toBeInTheDocument()
    expect(window.location.search).toBe('')
  })

  it('offers a retry after an availability failure and restores signup when it succeeds', async () => {
    const clients = createDemoApplicationClients()
    clients.authClient.getRegistrationAvailability = vi.fn().mockRejectedValueOnce(new Error('Unavailable')).mockResolvedValue({ isOpen: true })
    const user = userEvent.setup()
    await act(async () => { render(<AuthProvider client={clients.authClient}><LoginPage /></AuthProvider>) })

    expect(screen.getByText(/couldn’t check registration availability/)).toBeVisible()
    expect(screen.getByRole('button', { name: 'Continue with Google' })).toBeEnabled()
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByRole('button', { name: 'Create an account' })).toBeEnabled()
  })

  it('links to terms and privacy before signing in without an expandable retention explanation', async () => {
    await renderLogin()
    const terms = screen.getByRole('link', { name: /^Terms of Service/ })
    const privacy = screen.getByRole('link', { name: /^Privacy Policy/ })
    expect(terms).toBeVisible()
    expect(privacy).toBeVisible()
    expect(terms).toHaveAttribute('href', '/terms-of-service')
    expect(privacy).toHaveAttribute('href', '/privacy-policy')
    for (const link of [terms, privacy]) {
      expect(link).toHaveAttribute('target', '_blank')
      expect(link).toHaveAttribute('rel', 'noopener noreferrer')
      expect(link).toHaveAccessibleName(/opens in a new tab/)
      expect(link.closest('details')).toBeNull()
    }
    expect(screen.getByText(/By continuing with Google or email, or creating an account/)).toBeVisible()
    expect(screen.queryByText('Chat history and AI privacy')).not.toBeInTheDocument()
    expect(screen.queryByText(/We and our service providers may retain and review/)).not.toBeInTheDocument()
    expect(screen.queryByText(/records kept for security and abuse prevention/)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Continue with Google' })).toBeEnabled()
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
    expect(screen.getByRole('link', { name: /^Privacy Policy/ })).toBeVisible()
    expect(screen.getByRole('link', { name: /^Terms of Service/ })).toBeVisible()
  })
})
