import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AuthenticatedUser } from '../auth/authTypes.ts'
import { SettingsPage } from './SettingsPage.tsx'
import { createDefaultUserSettings, formatUsageRemainingPercent } from './settingsTypes.ts'
import { PwaInstallProvider } from '../pwa/PwaInstall.tsx'

const DemoUser: AuthenticatedUser = {
  id: 'demo-user',
  name: 'Demo User',
  email: 'demo@example.com',
  initials: 'DU',
  isEmailVerified: true,
}

describe('SettingsPage', () => {
  it('groups installation and offline controls in one unruled section', () => {
    render(<PwaInstallProvider><SettingsPage user={DemoUser} settings={createDefaultUserSettings()} usage={null} usageError={null} isLoadingUsage={false} isDataBusy={false} onDeleteChats={vi.fn()} onDeleteAccount={vi.fn()} onBack={vi.fn()} onSave={vi.fn()} onRequestPasswordReset={vi.fn()} onRetryUsage={vi.fn()} /></PwaInstallProvider>)

    const section = screen.getByRole('region', { name: 'App and offline' })
    expect(within(section).getByRole('button', { name: 'Install app' })).toBeVisible()
    expect(within(section).getByRole('switch', { name: 'Make weekly audio available offline' })).toBeVisible()
    expect(screen.queryByRole('heading', { name: 'Offline learning' })).not.toBeInTheDocument()
    expect(section.className).not.toMatch(/border-/)
    expect(section.querySelector('.border-y')).toBeNull()
  })

  it.each([[0, false, '100'], [25, false, '75'], [99.99, false, '<0.1'], [100, true, '0'], [150, true, '0']] as const)('formats percentage-only allowance at %s percent used', (usedPercent, isLimitReached, expected) => {
    expect(formatUsageRemainingPercent({ usedPercent, isLimitReached, periodStartUtc: '', periodEndUtc: '', tokensUsed: 0, tokenLimit: 10_000_000, tokensRemaining: 0 })).toBe(expected)
  })

  it('shows a recoverable error when password reset cannot be requested', async () => {
    const user = userEvent.setup()
    render(
      <SettingsPage
        user={DemoUser}
        settings={createDefaultUserSettings()}
        usage={null}
        usageError={null}
        isLoadingUsage={false}
        isDataBusy={false}
        onDeleteChats={vi.fn()}
        onDeleteAccount={vi.fn()}
        onBack={vi.fn()}
        onSave={() => Promise.resolve()}
        onRequestPasswordReset={() => Promise.reject(new Error('Provider unavailable'))}
        onRetryUsage={vi.fn()}
      />,
    )

    await user.click(screen.getByRole('button', { name: 'Reset password' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Password reset could not be requested')
    expect(screen.getByRole('button', { name: 'Reset password' })).toBeEnabled()
  })
})
