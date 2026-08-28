import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AuthenticatedUser } from '../auth/authTypes.ts'
import { SettingsPage } from './SettingsPage.tsx'
import { createDefaultUserSettings } from './settingsTypes.ts'

const DemoUser: AuthenticatedUser = {
  id: 'demo-user',
  name: 'Demo User',
  email: 'demo@example.com',
  initials: 'DU',
  isEmailVerified: true,
}

describe('SettingsPage', () => {
  it('shows a recoverable error when password reset cannot be requested', async () => {
    const user = userEvent.setup()
    render(
      <SettingsPage
        user={DemoUser}
        settings={createDefaultUserSettings()}
        usage={null}
        usageError={null}
        isLoadingUsage={false}
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
