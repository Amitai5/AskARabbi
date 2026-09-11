import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'
import { searchSettings, SettingsRegistry, SettingsSections } from './features/settings/settingsRegistry.ts'

beforeEach(() => {
  localStorage.clear()
  window.history.replaceState({}, '', '/')
  Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() })
})

async function signedIn(path = '/') {
  window.history.replaceState({}, '', path)
  const clients = createDemoApplicationClients()
  const reader = await clients.authClient.signInWithEmail('reader@example.test')
  clients.authClient.getSession = () => Promise.resolve(reader)
  const user = userEvent.setup()
  const mounted = render(<App {...clients} />)
  await screen.findByRole('main')
  await waitFor(() => expect(screen.queryByText('Loading your account…')).not.toBeInTheDocument())
  return { ...mounted, user, clients }
}

describe('unified settings', () => {
  it('swaps the sidebar from a single profile-menu entry and returns to chat', async () => {
    const { user } = await signedIn()
    await user.click(await screen.findByRole('button', { name: 'Open profile menu' }))
    await user.click(screen.getByRole('menuitem', { name: 'Settings & Personalization' }))
    expect(screen.queryByRole('complementary', { name: 'Conversation navigation' })).not.toBeInTheDocument()
    expect(screen.getAllByRole('tab')).toHaveLength(SettingsSections.length)
    expect(window.location.pathname).toBe('/settings/account')
    await user.click(screen.getByRole('button', { name: 'Back' }))
    expect(screen.getByRole('complementary', { name: 'Conversation navigation' })).toBeVisible()
    expect(window.location.pathname).toBe('/')
  })

  it.each([['/settings', 'Account', '/settings/account'], ['/personalization', 'Personalization', '/settings/personalization'], ['/settings/reading', 'Reading', '/settings/reading']])('opens and canonicalizes %s', async (path, heading, expectedPath) => {
    await signedIn(path)
    expect(await screen.findByRole('heading', { name: heading })).toBeVisible()
    expect(window.location.pathname).toBe(expectedPath)
    expect(document.title).toContain(`${heading} · Settings`)
  })

  it('has a reachable destination for every registered setting, and retains personalization drafts', async () => {
    const { user } = await signedIn('/settings/personalization')
    const input = await screen.findByLabelText('Full name')
    await user.clear(input)
    await user.type(input, 'An unsaved name')
    for (const section of SettingsSections) {
      await user.click(screen.getByRole('tab', { name: section.label }))
      for (const setting of SettingsRegistry.filter(value => value.section === section.id)) {
        expect(document.getElementById(`setting-${setting.id}`), setting.id).toBeVisible()
      }
    }
    await user.click(screen.getByRole('tab', { name: 'Personalization' }))
    expect(screen.getByLabelText('Full name')).toHaveValue('An unsaved name')
  })

  it('searches keywords and descriptions then navigates, focuses, and highlights the result', async () => {
    const { user } = await signedIn('/settings/account')
    const search = await screen.findByRole('searchbox', { name: 'Search settings' })
    await user.type(search, 'night')
    await user.click(screen.getByRole('button', { name: 'Theme, Reading' }))
    expect(window.location.pathname + window.location.hash).toBe('/settings/reading#theme')
    const destination = document.getElementById('setting-theme')
    expect(destination).toHaveClass('settings-highlight')
    expect(destination).toContainElement(document.activeElement as HTMLElement)
    expect(HTMLElement.prototype.scrollTo).toHaveBeenCalled()
    await user.clear(search)
    await user.type(search, 'no such setting')
    expect(screen.getByText('No settings found. Try a different word.')).toBeVisible()
    expect(searchSettings('ZIP')).toEqual(expect.arrayContaining([expect.objectContaining({ id: 'locations' })]))
    expect(searchSettings('surrounding text')[0].id).toBe('source-context')
  })

  it('uses full-width settings cards without repeating section titles beside the controls', async () => {
    const { user } = await signedIn('/settings/account')
    const groups = [
      { section: 'Account', titles: ['Account security', 'Usage'] },
      { section: 'Reading', titles: ['Conversation defaults'] },
      { section: 'Notifications', titles: ['Product updates'] },
      { section: 'App & offline', titles: ['App and offline'] },
      { section: 'Your data', titles: ['Your data'] },
    ]
    for (const group of groups) {
      await user.click(screen.getByRole('tab', { name: group.section }))
      const panel = screen.getByRole('tabpanel')
      expect(within(panel).getByRole('heading', { name: group.section, level: 1 })).toBeVisible()
      for (const title of group.titles) {
        const region = within(panel).getByRole('region', { name: title })
        expect(within(region).queryByRole('heading', { name: title })).not.toBeInTheDocument()
        expect(region.firstElementChild).toHaveClass('min-w-0', 'rounded-2xl')
        expect(region.firstElementChild).not.toHaveClass('grid')
      }
    }
  })

  it('keeps personalization subtitles above the full-width field groups', async () => {
    await signedIn('/settings/personalization')
    for (const title of ['About you', 'Location & time zone', 'Language', 'Jewish background', 'Anything else?']) {
      const region = screen.getByRole('region', { name: title })
      const heading = within(region).getByRole('heading', { name: title, level: 2 })
      expect(heading).toBeVisible()
      expect(region.firstElementChild).toHaveClass('space-y-6')
      expect(region.firstElementChild?.firstElementChild).toContainElement(heading)
      expect(region.firstElementChild?.lastElementChild).toHaveClass('min-w-0')
    }
    expect(screen.getByLabelText('Full name')).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Save personalization' })).toBeEnabled()
  })

  it('supports keyboard tab navigation and browser history without a reload', async () => {
    const { user } = await signedIn('/settings/account')
    const account = await screen.findByRole('tab', { name: 'Account' })
    account.focus()
    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('tab', { name: 'Personalization' })).toHaveFocus()
    expect(window.location.pathname).toBe('/settings/personalization')
    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('tab', { name: 'Reading' })).toHaveAttribute('aria-selected', 'true')
    window.history.replaceState({}, '', '/settings/account')
    act(() => window.dispatchEvent(new PopStateEvent('popstate')))
    expect(screen.getByRole('heading', { name: 'Account' })).toBeVisible()
  })

  it('opens the mobile settings drawer and closes it after selecting a result', async () => {
    const { user } = await signedIn('/settings/reading')
    await user.click(await screen.findByRole('button', { name: 'Open settings navigation' }))
    expect(screen.getByRole('dialog', { name: 'Settings navigation' })).toHaveAttribute('aria-modal', 'true')
    expect(screen.getByRole('searchbox', { name: 'Search settings' })).toHaveFocus()
    expect(screen.getByRole('main')).toHaveAttribute('inert')
    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'password' } })
    await user.click(screen.getByRole('button', { name: 'Password, Account' }))
    expect(screen.queryByRole('dialog', { name: 'Settings navigation' })).not.toBeInTheDocument()
    expect(screen.getByRole('main')).not.toHaveAttribute('inert')
    expect(screen.getByRole('button', { name: 'Reset password' })).toHaveFocus()
  })

  it('restores interactive content when an open mobile drawer becomes a desktop sidebar', async () => {
    const events = new EventTarget()
    const desktop = { matches: false, addEventListener: events.addEventListener.bind(events), removeEventListener: events.removeEventListener.bind(events) }
    const appearance = { matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() }
    vi.stubGlobal('matchMedia', (query: string) => query.includes('min-width') ? desktop : appearance)
    try {
      const { user, unmount } = await signedIn('/settings/reading')
      await user.click(await screen.findByRole('button', { name: 'Open settings navigation' }))
      expect(screen.getByRole('main')).toHaveAttribute('inert')

      desktop.matches = true
      act(() => events.dispatchEvent(new Event('change')))

      expect(screen.getByRole('main')).not.toHaveAttribute('inert')
      expect(screen.queryByRole('dialog', { name: 'Settings navigation' })).not.toBeInTheDocument()
      unmount()
    } finally { vi.unstubAllGlobals() }
  })
})
