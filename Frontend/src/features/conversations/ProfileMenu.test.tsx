import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ProfileMenu } from './ProfileMenu.tsx'
import type { UsageSummary } from '../settings/settingsTypes.ts'

const Usage: UsageSummary = { usedPercent: 34, isLimitReached: false, tokensUsed: 34, tokenLimit: 100, tokensRemaining: 66, periodStartUtc: '2026-09-01T00:00:00Z', periodEndUtc: '2026-10-01T00:00:00Z' }
const defaults = { user: { id: 'reader', name: 'A reader', email: 'reader@example.test', initials: 'AR', isEmailVerified: true }, usage: Usage, isLoadingUsage: false, usageError: null, isOffline: false }

describe('profile usage', () => {
  it.each([
    { label: '66% left', props: {} },
    { label: '0% left', props: { usage: { ...Usage, isLimitReached: true } } },
    { label: 'Loading…', props: { usage: null, isLoadingUsage: true } },
    { label: 'Unavailable', props: { usageError: 'Cannot refresh usage' } },
    { label: 'Unavailable', props: { usage: null } },
    { label: 'Offline', props: { isOffline: true } },
  ])('shows $label rather than inventing an allowance', async ({ label, props }) => {
    const user = userEvent.setup()
    render(<ProfileMenu {...defaults} {...props} onOpenUsage={vi.fn()} onOpenSettings={vi.fn()} onLogout={vi.fn()} />)
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Open profile menu' }))
    expect(screen.getByRole('menuitem', { name: `Usage, ${label}` })).toBeVisible()
    expect(screen.getByRole('menuitem', { name: 'Settings & Personalization' })).toBeVisible()
  })

  it('supports keyboard navigation, opens usage, and returns focus after Escape', async () => {
    const user = userEvent.setup()
    const openUsage = vi.fn()
    render(<ProfileMenu {...defaults} onOpenUsage={openUsage} onOpenSettings={vi.fn()} onLogout={vi.fn()} />)
    const trigger = screen.getByRole('button', { name: 'Open profile menu' })
    await user.click(trigger)
    expect(screen.getByRole('menuitem', { name: 'Usage, 66% left' })).toHaveFocus()
    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('menuitem', { name: 'Settings & Personalization' })).toHaveFocus()
    await user.keyboard('{End}')
    expect(screen.getByRole('menuitem', { name: 'Log out' })).toHaveFocus()
    await user.keyboard('{Home}{Enter}')
    expect(openUsage).toHaveBeenCalledOnce()
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
    await user.click(trigger)
    await user.keyboard('{Escape}')
    expect(trigger).toHaveFocus()
  })
})
