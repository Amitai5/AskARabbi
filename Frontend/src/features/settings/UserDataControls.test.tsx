import { act, fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { UserDataControls } from './UserDataControls.tsx'

describe('Your data controls', () => {
  it('distinguishes saved chats from Azure response storage and Microsoft abuse monitoring', () => {
    const deleteChats = vi.fn()
    const deleteAccount = vi.fn()
    render(<UserDataControls isBusy={false} onDeleteChats={deleteChats} onDeleteAccount={deleteAccount} />)

    expect(screen.getByRole('heading', { name: 'Chat history and AI privacy' })).toBeVisible()
    expect(screen.getByText(/AskRabbi saves your questions and answers in your account/)).toBeVisible()
    expect(screen.getByText(/disable its stored-response feature/)).toHaveTextContent('Microsoft may still retain prompts and answers for abuse monitoring')
    expect(screen.getByText(/Deleting chats from AskRabbi does not delete Microsoft’s abuse-monitoring records/)).toBeVisible()
    expect(screen.getByText(/Service backups and security logs follow separate retention policies/)).toBeVisible()
    const detailsLink = screen.getByRole('link', { name: /Microsoft’s Azure AI privacy details/ })
    expect(detailsLink).toHaveAttribute('href', 'https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy')
    expect(detailsLink).toHaveAttribute('rel', 'noopener noreferrer')
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
