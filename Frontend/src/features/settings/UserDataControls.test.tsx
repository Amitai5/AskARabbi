import { act, fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { UserDataControls } from './UserDataControls.tsx'

describe('Your data controls', () => {
  it('keeps the concise policy links, searchable legal sections, and unchanged deletion controls', () => {
    const deleteChats = vi.fn()
    const deleteAccount = vi.fn()
    const { container } = render(<UserDataControls isBusy={false} onDeleteChats={deleteChats} onDeleteAccount={deleteAccount} />)

    expect(screen.getByRole('heading', { name: 'Terms and privacy' })).toBeVisible()
    const terms = screen.getByRole('link', { name: /^Terms of Use/ })
    const privacy = screen.getByRole('link', { name: /^Privacy Policy/ })
    expect(terms).toHaveAttribute('href', 'https://askarabbi.ai/terms')
    expect(privacy).toHaveAttribute('href', 'https://askarabbi.ai/privacy')
    for (const link of [terms, privacy]) {
      expect(link).toBeVisible()
      expect(link).toHaveAttribute('target', '_blank')
      expect(link).toHaveAttribute('rel', 'noopener noreferrer')
      expect(link).toHaveAccessibleName(/opens in a new tab/)
    }
    expect(container).not.toHaveTextContent(/We and our service providers may retain and review|records kept for security and abuse prevention/)
    expect(screen.getByText(/Service backups and security logs may be retained separately/)).toBeVisible()
    expect(container).not.toHaveTextContent(/Azure|OpenAI|Microsoft/i)
    expect(screen.getByRole('button', { name: 'Delete all chats' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Delete account' })).toBeEnabled()
    expect(screen.getByRole('link', { name: /Terms of Service/ })).toHaveAttribute('href', 'https://askarabbi.ai/terms')
    expect(screen.getByRole('link', { name: /retention and deletion policy/ })).toHaveAttribute('href', 'https://askarabbi.ai/privacy#retention')
    expect(privacy.closest('.settings-target')).toHaveAttribute('id', 'setting-privacy-policy')
    expect(screen.getByRole('heading', { name: 'Using AskRabbi' }).closest('.settings-target')).toHaveAttribute('id', 'setting-terms-of-service')
    expect(deleteChats).not.toHaveBeenCalled()
    expect(deleteAccount).not.toHaveBeenCalled()
  })

  it('requires the exact phrase and allows cancellation without deleting anything', async () => {
    const user = userEvent.setup()
    const deleteChats = vi.fn()
    const deleteAccount = vi.fn()
    render(<UserDataControls isBusy={false} onDeleteChats={deleteChats} onDeleteAccount={deleteAccount} />)
    const trigger = screen.getByRole('button', { name: 'Delete all chats' })
    await user.click(trigger)
    const dialog = screen.getByRole('dialog', { name: 'Delete all chats?' })
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toHaveFocus()
    const submit = within(dialog).getByRole('button', { name: 'Delete all chats' })
    expect(submit).toBeDisabled()
    await user.type(within(dialog).getByRole('textbox'), 'DELETE ALL')
    expect(submit).toBeDisabled()
    fireEvent(dialog, new Event('cancel', { cancelable: true }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(trigger).toHaveFocus()
    expect(deleteChats).not.toHaveBeenCalled()
    expect(deleteAccount).not.toHaveBeenCalled()
  })

  it('prevents duplicate submission and closing while erasure is in progress', async () => {
    const user = userEvent.setup()
    let complete = () => {}
    const deleteChats = vi.fn(() => new Promise<void>((resolve) => { complete = resolve }))
    render(<UserDataControls isBusy={false} onDeleteChats={deleteChats} onDeleteAccount={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: 'Delete all chats' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByRole('textbox'), 'DELETE ALL CHATS')
    await user.dblClick(within(dialog).getByRole('button', { name: 'Delete all chats' }))
    expect(deleteChats).toHaveBeenCalledTimes(1)
    expect(within(dialog).getByRole('button', { name: 'Deleting…' })).toBeDisabled()
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toBeDisabled()
    fireEvent(dialog, new Event('cancel', { cancelable: true }))
    expect(dialog).toBeVisible()
    await act(async () => complete())
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('All chats deleted')
  })

  it('keeps account confirmation open and retryable when the request fails', async () => {
    const user = userEvent.setup()
    const deleteAccount = vi.fn().mockRejectedValueOnce(new Error('An answer is still in progress.')).mockResolvedValue(undefined)
    const deleteChats = vi.fn()
    render(<UserDataControls isBusy={false} onDeleteChats={deleteChats} onDeleteAccount={deleteAccount} />)
    await user.click(screen.getByRole('button', { name: 'Delete account' }))
    const dialog = screen.getByRole('dialog', { name: 'Delete your account?' })
    await user.type(within(dialog).getByRole('textbox'), 'DELETE ACCOUNT')
    await user.click(within(dialog).getByRole('button', { name: 'Permanently delete account' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('An answer is still in progress')
    expect(dialog).toBeVisible()
    expect(deleteChats).not.toHaveBeenCalled()
    await user.click(within(dialog).getByRole('button', { name: 'Permanently delete account' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(deleteAccount).toHaveBeenCalledTimes(2)
  })

  it('disables data deletion while a reply or settings update is pending', () => {
    render(<UserDataControls isBusy onDeleteChats={vi.fn()} onDeleteAccount={vi.fn()} />)
    expect(screen.getByRole('button', { name: 'Delete all chats' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Delete account' })).toBeDisabled()
  })
})
