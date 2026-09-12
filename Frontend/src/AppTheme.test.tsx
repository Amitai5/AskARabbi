import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'
import type { AuthenticatedUser } from './features/auth/authTypes.ts'
import { ActiveReadingUserKey, applyReadingPreferences, cacheReadingPreferences, clearActiveReadingUser, DefaultReadingPreferences, readReadingCache } from './features/reading/readingPreferences.ts'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>(done => { resolve = done })
  return { promise, resolve }
}

beforeEach(() => {
  localStorage.clear()
  sessionStorage.clear()
  window.history.replaceState({}, '', '/')
  Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() })
  vi.stubGlobal('matchMedia', () => ({ matches: true, addEventListener() {}, removeEventListener() {} }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  clearActiveReadingUser()
})

describe('public and account themes', () => {
  it('keeps sign-in light during session hydration and clears only the active cache marker for anonymous visitors', async () => {
    const clients = createDemoApplicationClients()
    const session = deferred<null>()
    const preferences = { ...DefaultReadingPreferences, theme: 'system' as const }
    cacheReadingPreferences('previous-reader', preferences, false)
    applyReadingPreferences(preferences)
    render(<App {...clients} authClient={{ ...clients.authClient, getSession: () => session.promise }} />)

    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeVisible()
    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem(ActiveReadingUserKey)).toBe('previous-reader')

    await act(async () => session.resolve(null))

    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem(ActiveReadingUserKey)).toBeNull()
    expect(readReadingCache('previous-reader')?.preferences).toEqual(preferences)
  })

  it('restores the authenticated account theme and returns to light on logout', async () => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    const account = await clients.authClient.signInWithSocialProvider('google')
    if (!account) { throw new Error('Expected a demo account') }
    const preferences = { ...DefaultReadingPreferences, theme: 'dark' as const }
    cacheReadingPreferences(account.id, preferences, false)
    await clients.conversationSettingsClient.updateReadingPreferences(preferences)
    const session = deferred<AuthenticatedUser | null>()
    render(<App {...clients} authClient={{ ...clients.authClient, getSession: () => session.promise }} />)
    expect(document.documentElement.dataset.theme).toBe('light')

    await act(async () => session.resolve(account))
    await user.click(await screen.findByRole('button', { name: 'Open profile menu' }))
    expect(document.documentElement.dataset.theme).toBe('dark')
    await user.click(screen.getByRole('menuitem', { name: 'Log out' }))

    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeVisible()
    expect(document.documentElement.dataset.theme).toBe('light')
    expect(readReadingCache(account.id)?.preferences.theme).toBe('dark')

    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    await screen.findByRole('button', { name: 'Open profile menu' })
    expect(document.documentElement.dataset.theme).toBe('dark')
  })

  it('starts an account with light defaults and lets the user select Dark or System in settings', async () => {
    const user = userEvent.setup()
    const clients = createDemoApplicationClients()
    render(<App {...clients} />)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Continue with Google' })).toBeEnabled())
    await user.click(screen.getByRole('button', { name: 'Continue with Google' }))
    await user.click(await screen.findByRole('button', { name: 'Open profile menu' }))
    await user.click(screen.getByRole('menuitem', { name: 'Settings & Personalization' }))
    await user.click(screen.getByRole('tab', { name: 'Reading' }))

    expect(screen.getByRole('radio', { name: 'Light' })).toBeChecked()
    expect(document.documentElement.dataset.theme).toBe('light')
    await user.click(screen.getByRole('radio', { name: 'Dark' }))
    expect(document.documentElement.dataset.theme).toBe('dark')
    await user.click(screen.getByRole('radio', { name: 'System' }))
    expect(document.documentElement.dataset.theme).toBe('dark')
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Reading preferences saved.'))
    expect((await clients.conversationSettingsClient.getReadingPreferences()).theme).toBe('system')
  })

  it('keeps password reset light even if an existing session has a dark preference', async () => {
    const clients = createDemoApplicationClients()
    const account = await clients.authClient.signInWithSocialProvider('google')
    applyReadingPreferences({ ...DefaultReadingPreferences, theme: 'dark' })
    window.history.replaceState({}, '', '/reset-password?token=example-token')
    render(<App {...clients} authClient={{ ...clients.authClient, getSession: async () => account }} />)

    expect(await screen.findByLabelText('New password')).toBeVisible()
    await act(async () => {})
    expect(document.documentElement.dataset.theme).toBe('light')
  })
})
