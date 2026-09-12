import { useState } from 'react'
import { Trash2, UserRoundX } from 'lucide-react'
import { ConfirmDeletionDialog } from '../../components/ConfirmDeletionDialog.tsx'
import { ChatPrivacyNotice } from '../../components/ChatPrivacyNotice.tsx'
import { LegalLink } from '../legal/LegalLinks.tsx'

interface UserDataControlsProps {
  isBusy: boolean
  onDeleteChats(): Promise<void>
  onDeleteAccount(): Promise<void>
}

export function UserDataControls({ isBusy, onDeleteChats, onDeleteAccount }: UserDataControlsProps) {
  const [action, setAction] = useState<'chats' | 'account' | null>(null)
  const [chatsDeleted, setChatsDeleted] = useState(false)

  async function deleteChats() {
    await onDeleteChats()
    setChatsDeleted(true)
  }

  return <>
    <div className="space-y-7">
      <div id="setting-privacy-policy" tabIndex={-1} className="settings-target">
        <h3 className="mb-3 font-semibold">Terms and privacy</h3>
        <ChatPrivacyNotice />
      </div>
      <div id="setting-terms-of-service" tabIndex={-1} className="settings-target">
        <h3 className="mb-3 font-semibold">Using AskRabbi</h3>
        <p className="text-sm leading-6 text-muted sm:text-base">Our <LegalLink document="terms-of-service" /> explain acceptable use, AI limitations, source rights, and your responsibilities.</p>
      </div>
      <div id="setting-delete-chats" tabIndex={-1} className="settings-target flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="max-w-[27rem]">
          <h3 className="font-semibold">Delete all chats</h3>
          <p className="mt-1 text-muted">Clear your entire conversation history and messages. Your account, preferences, and current usage stay the same.</p>
        </div>
        <button type="button" disabled={isBusy} onClick={() => setAction('chats')} className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-lg border border-line-strong px-4 font-semibold transition hover:border-pomegranate/40 hover:bg-pomegranate/5 hover:text-pomegranate disabled:cursor-wait disabled:opacity-50"><Trash2 aria-hidden="true" className="size-4" />Delete all chats</button>
      </div>
      <div id="setting-delete-account" tabIndex={-1} className="settings-target flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="max-w-[27rem]">
          <h3 className="font-semibold">Delete account</h3>
          <p className="mt-1 text-muted">Permanently delete your AskRabbi account, chats, personal profile, preferences, usage history, and sign-in identity.</p>
        </div>
        <button type="button" disabled={isBusy} onClick={() => setAction('account')} className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-lg border border-pomegranate/35 px-4 font-semibold text-pomegranate transition hover:bg-pomegranate/10 disabled:cursor-wait disabled:opacity-50"><UserRoundX aria-hidden="true" className="size-4" />Delete account</button>
      </div>
    </div>
    <p className="mt-3 text-sm leading-6 text-muted sm:text-base">These actions cannot be undone. Deleting AskRabbi does not delete your Google account. Service backups and security logs may be retained separately as explained in our <LegalLink document="privacy-policy" section="retention">retention and deletion policy</LegalLink>.</p>
    {isBusy ? <p className="mt-3 text-ink-soft" role="status">Wait for your current answer or account update to finish before deleting data.</p> : null}
    {chatsDeleted ? <p className="mt-3 font-semibold text-ink" role="status">All chats deleted. Your account and settings were kept.</p> : null}
    {action === 'chats' ? <ConfirmDeletionDialog title="Delete all chats?" description="Every saved conversation and its messages will be permanently deleted, including chats not currently visible in the sidebar. This cannot be undone." confirmationPhrase="DELETE ALL CHATS" confirmLabel="Delete all chats" onConfirm={deleteChats} onClose={() => setAction(null)} /> : null}
    {action === 'account' ? <ConfirmDeletionDialog title="Delete your account?" description="Your AskRabbi account and all its chats, personal profile, settings, usage history, and sign-in identity will be permanently deleted. You will be signed out on all devices. If cleanup is temporarily unavailable, your account stays disabled while we retry. This cannot be undone." confirmationPhrase="DELETE ACCOUNT" confirmLabel="Permanently delete account" onConfirm={onDeleteAccount} onClose={() => setAction(null)} /> : null}
  </>
}
