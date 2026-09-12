import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SettingsSidebar } from './SettingsSidebar.tsx'

describe('SettingsSidebar', () => {
  it('uses consistent row spacing and distinguishes the active section from hover', async () => {
    const user = userEvent.setup()
    const navigate = vi.fn()
    render(<SettingsSidebar section="personalization" isMobileOpen={false} onClose={vi.fn()} onBack={vi.fn()} onNavigate={navigate} />)
    const tabs = screen.getAllByRole('tab')
    expect(screen.getByRole('tablist')).toHaveClass('flex', 'flex-col', 'gap-1')
    for (const tab of tabs) { expect(tab).toHaveClass('min-h-12', 'px-3', 'py-2.5') }
    expect(screen.getByRole('tab', { name: 'Personalization' })).toHaveAttribute('aria-selected', 'true')
    const reading = screen.getByRole('tab', { name: 'Reading' })
    expect(reading).toHaveClass('hover:bg-stone-deep/40')
    expect(reading).not.toHaveClass('bg-stone-deep')
    screen.getByRole('tab', { name: 'Personalization' }).focus()
    await user.keyboard('{ArrowDown}')
    expect(reading).toHaveFocus()
    expect(navigate).toHaveBeenCalledWith('reading', undefined, true)
  })

  it('keeps search and Back available above a scrollable section list', async () => {
    const user = userEvent.setup()
    const back = vi.fn()
    render(<SettingsSidebar section="account" isMobileOpen={false} onClose={vi.fn()} onBack={back} onNavigate={vi.fn()} />)
    expect(screen.getByRole('complementary', { name: 'Settings navigation' })).toHaveClass('min-h-0', 'overflow-hidden')
    const search = screen.getByRole('searchbox', { name: 'Search settings' })
    expect(search.closest('.shrink-0')).toHaveClass('sidebar-scroll', 'overflow-hidden', 'px-4')
    await user.type(search, 'theme')
    expect(screen.getByRole('button', { name: 'Theme, Reading' })).toBeVisible()
    await user.keyboard('{Escape}')
    expect(search).toHaveValue('')
    await user.click(screen.getByRole('button', { name: 'Back' }))
    expect(back).toHaveBeenCalledOnce()
  })
})
