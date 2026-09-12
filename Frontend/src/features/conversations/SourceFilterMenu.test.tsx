import { useState } from 'react'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SourceFilterMenu } from './SourceFilterMenu.tsx'
import { AllSourceKeys, SourceOptions } from './sourceOptions.ts'

function Menu() {
  const [keys, setKeys] = useState<string[]>([...AllSourceKeys])
  return <SourceFilterMenu selectedSourceKeys={keys} isDisabled={false} onChange={setKeys} />
}

describe('SourceFilterMenu', () => {
  it('keeps source presets and individual checkboxes usable in the compact menu', async () => {
    const user = userEvent.setup()
    render(<Menu />)
    await user.click(screen.getByRole('button', { name: 'Choose sources: All sources' }))
    const menu = screen.getByRole('dialog', { name: 'Sources used for this conversation' })
    expect(menu).toHaveClass('readable-menu', 'scale-90', 'origin-bottom-left')
    expect(within(menu).getAllByRole('checkbox')).toHaveLength(SourceOptions.length)
    for (const source of SourceOptions) {
      expect(within(menu).getByText(source.description)).not.toHaveClass('truncate')
    }
    await user.click(within(menu).getByRole('button', { name: 'Clear all sources' }))
    expect(within(menu).getAllByRole('checkbox').every(input => !(input as HTMLInputElement).checked)).toBe(true)
    await user.click(within(menu).getByRole('checkbox', { name: 'Torah' }))
    expect(screen.getByRole('button', { name: 'Choose sources: 1 sources' })).toBeVisible()
    await user.click(within(menu).getByRole('button', { name: 'Select core sources' }))
    expect(screen.getByRole('button', { name: 'Choose sources: Core sources' })).toBeVisible()
    await user.click(within(menu).getByRole('button', { name: 'Select all sources' }))
    expect(within(menu).getAllByRole('checkbox').every(input => (input as HTMLInputElement).checked)).toBe(true)
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Choose sources: All sources' })).toHaveFocus()
  })

  it('does not allow source changes while the composer is disabled', async () => {
    const change = vi.fn()
    const user = userEvent.setup()
    render(<SourceFilterMenu selectedSourceKeys={AllSourceKeys} isDisabled onChange={change} />)
    await user.click(screen.getByRole('button', { name: 'Choose sources: All sources' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(change).not.toHaveBeenCalled()
  })
})
