import { useEffect, useState, type FormEvent } from 'react'
import { BookOpenText, Check, Ellipsis, MessageCircle, Pencil, Plus, Trash2, X } from 'lucide-react'
import { Brand } from '../../components/Brand.tsx'
import type { AuthenticatedUser } from '../auth/authTypes.ts'
import type { ConversationSummary } from './conversationData.ts'
import { ProfileMenu } from './ProfileMenu.tsx'

interface ConversationSidebarProps {
  conversations: ConversationSummary[]
  selectedId: string | null
  isMobileOpen: boolean
  isDesktopOpen: boolean
  isNewConversationDisabled: boolean
  isConversationNavigationDisabled: boolean
  isDvarTorahSelected: boolean
  user: AuthenticatedUser
  onCloseMobile(): void
  onNewConversation(): void
  onSelectConversation(id: string): void
  onRenameConversation(id: string, title: string): void
  onDeleteConversation(id: string): void
  onOpenDvarTorah(): void
  onOpenSettings(): void
  onOpenPersonalization(): void
  onLogout(): Promise<void>
}

export function ConversationSidebar({ conversations, selectedId, isMobileOpen, isDesktopOpen, isNewConversationDisabled, isConversationNavigationDisabled, isDvarTorahSelected, user, onCloseMobile, onNewConversation, onSelectConversation, onRenameConversation, onDeleteConversation, onOpenDvarTorah, onOpenSettings, onOpenPersonalization, onLogout }: ConversationSidebarProps) {
  const [openMenuId, setOpenMenuId] = useState<string | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [renameDraft, setRenameDraft] = useState('')
  const [deleteConfirmationId, setDeleteConfirmationId] = useState<string | null>(null)
  const desktopVisibility = isDesktopOpen ? 'lg:visible lg:w-72 lg:translate-x-0' : 'lg:invisible lg:w-0 lg:-translate-x-full lg:border-r-0'
  const mobileVisibility = isMobileOpen ? 'visible translate-x-0' : 'invisible -translate-x-full'

  useEffect(() => {
    if (openMenuId === null && editingId === null && deleteConfirmationId === null) {
      return
    }

    function handlePointerDown(event: PointerEvent) {
      if (!(event.target instanceof Element) || event.target.closest('[data-conversation-actions]') === null) {
        setOpenMenuId(null)
        setDeleteConfirmationId(null)
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key !== 'Escape') {
        return
      }

      if (editingId !== null) {
        setEditingId(null)
        setRenameDraft('')
      }
      setOpenMenuId(null)
      setDeleteConfirmationId(null)
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [deleteConfirmationId, editingId, openMenuId])

  function closeActionMenu() {
    setOpenMenuId(null)
    setDeleteConfirmationId(null)
  }

  function startRename(conversation: ConversationSummary) {
    setEditingId(conversation.id)
    setRenameDraft(conversation.title)
    closeActionMenu()
  }

  function submitRename(event: FormEvent<HTMLFormElement>, conversationId: string) {
    event.preventDefault()
    const title = renameDraft.trim()
    if (title.length === 0) {
      return
    }

    onRenameConversation(conversationId, title)
    setEditingId(null)
    setRenameDraft('')
  }

  function cancelRename() {
    setEditingId(null)
    setRenameDraft('')
  }

  function selectConversation(id: string) {
    if (isConversationNavigationDisabled) {
      return
    }

    closeActionMenu()
    onSelectConversation(id)
  }

  function deleteConversation(id: string) {
    closeActionMenu()
    onDeleteConversation(id)
  }

  function openDvarTorah() {
    if (isConversationNavigationDisabled) {
      return
    }

    closeActionMenu()
    onOpenDvarTorah()
  }

  return (
    <aside className={`fixed inset-y-0 left-0 z-40 flex h-dvh min-h-0 w-[min(20rem,calc(100vw-2.5rem))] shrink-0 flex-col overflow-hidden overscroll-none border-r border-line bg-stone transition-[transform,width] duration-300 ease-out lg:relative lg:z-0 ${mobileVisibility} ${desktopVisibility}`} aria-label="Conversation navigation">
      <div className="flex items-center justify-between px-5 pb-5 pt-6">
        <Brand compact />
        <button type="button" onClick={onCloseMobile} className="flex size-11 items-center justify-center rounded-lg text-ink transition hover:bg-stone-deep lg:hidden" aria-label="Close conversation navigation">
          <X aria-hidden="true" className="size-5" strokeWidth={1.75} />
        </button>
      </div>

      <div className="px-4">
        <button type="button" disabled={isNewConversationDisabled} onClick={onNewConversation} className="flex h-13 w-full items-center justify-center gap-2.5 rounded-lg bg-pomegranate px-4 text-sm font-semibold text-white transition hover:bg-pomegranate-dark disabled:cursor-wait disabled:opacity-60">
          <Plus aria-hidden="true" className="size-5" strokeWidth={1.75} />
          New conversation
        </button>
      </div>

      <nav className="mt-6 px-3" aria-label="Weekly learning">
        <p className="px-3 pb-2 text-xs font-semibold uppercase tracking-[0.14em] text-muted">Weekly learning</p>
        <div className={`relative flex min-h-11 items-center rounded-lg transition hover:bg-stone-deep/70 ${isDvarTorahSelected ? 'bg-stone-deep/70 font-semibold' : ''}`}>
          {isDvarTorahSelected ? <span className="absolute inset-y-2 left-0 w-0.5 rounded-full bg-pomegranate" /> : null}
          <button type="button" disabled={isConversationNavigationDisabled} onClick={openDvarTorah} aria-current={isDvarTorahSelected ? 'page' : undefined} className="flex min-h-11 min-w-0 flex-1 items-center gap-3 px-3 text-left text-sm text-ink disabled:cursor-wait disabled:opacity-60">
            <BookOpenText aria-hidden="true" className="size-[1.1rem] shrink-0" strokeWidth={1.65} />
            <span className="truncate">This week’s Dvar Torah</span>
          </button>
        </div>
      </nav>

      <nav className="mt-5 min-h-0 flex-1 touch-pan-y overflow-y-auto overscroll-y-contain px-3 pb-4" aria-label="Recent conversations">
        <p className="px-3 pb-2 text-xs font-semibold uppercase tracking-[0.14em] text-muted">Recent</p>
        <ul className="space-y-1">
          {conversations.map((conversation, index) => {
            const isSelected = !isDvarTorahSelected && conversation.id === selectedId
            return (
              <li key={conversation.id} className="relative">
                {editingId === conversation.id ? (
                  <form className="flex min-h-11 items-center gap-1 rounded-lg bg-stone-deep/70 pl-3 pr-1" onSubmit={(event) => submitRename(event, conversation.id)}>
                    <MessageCircle aria-hidden="true" className="size-[1.1rem] shrink-0" strokeWidth={1.65} />
                    <input autoFocus type="text" maxLength={80} value={renameDraft} onChange={(event) => setRenameDraft(event.target.value)} className="h-9 min-w-0 flex-1 rounded-md border border-line-strong bg-paper px-2 text-sm text-ink focus:border-pomegranate focus:outline-none" aria-label={`Rename ${conversation.title}`} />
                    <button type="submit" disabled={renameDraft.trim().length === 0} className="flex size-8 shrink-0 items-center justify-center rounded-md text-ink transition hover:bg-paper disabled:cursor-not-allowed disabled:opacity-40" aria-label="Save conversation name">
                      <Check aria-hidden="true" className="size-4" strokeWidth={1.9} />
                    </button>
                    <button type="button" onClick={cancelRename} className="flex size-8 shrink-0 items-center justify-center rounded-md text-muted transition hover:bg-paper hover:text-ink" aria-label="Cancel rename">
                      <X aria-hidden="true" className="size-4" strokeWidth={1.8} />
                    </button>
                  </form>
                ) : (
                  <div className={`group relative flex min-h-11 items-center rounded-lg transition hover:bg-stone-deep/70 ${isSelected ? 'bg-stone-deep/70 font-semibold' : ''}`}>
                    {isSelected ? <span className="absolute inset-y-2 left-0 w-0.5 rounded-full bg-pomegranate" /> : null}
                    <button type="button" disabled={isConversationNavigationDisabled} onClick={() => selectConversation(conversation.id)} aria-current={isSelected ? 'page' : undefined} className="flex min-h-11 min-w-0 flex-1 items-center gap-3 pl-3 pr-1 text-left text-sm text-ink disabled:cursor-wait disabled:opacity-60">
                      <MessageCircle aria-hidden="true" className="size-[1.1rem] shrink-0" strokeWidth={1.65} />
                      <span className="truncate">{conversation.title}</span>
                    </button>

                    <div className="relative mr-1" data-conversation-actions>
                      <button type="button" disabled={isConversationNavigationDisabled} onClick={() => {
                        setDeleteConfirmationId(null)
                        setOpenMenuId((current) => current === conversation.id ? null : conversation.id)
                      }} className={`flex size-9 items-center justify-center rounded-md text-muted transition hover:bg-paper hover:text-ink focus:opacity-100 disabled:cursor-wait disabled:opacity-30 ${openMenuId === conversation.id ? 'bg-paper text-ink opacity-100' : 'opacity-55 group-hover:opacity-100'}`} aria-label={`Conversation actions for ${conversation.title}, item ${index + 1}`} aria-haspopup="menu" aria-expanded={openMenuId === conversation.id}>
                        <Ellipsis aria-hidden="true" className="size-[1.1rem]" strokeWidth={1.8} />
                      </button>

                      {openMenuId === conversation.id ? (
                        deleteConfirmationId === conversation.id ? (
                          <div className="absolute right-0 top-10 z-30 w-52 rounded-xl border border-line bg-paper p-3 shadow-menu" role="dialog" aria-label={`Delete ${conversation.title}`}>
                            <p className="text-sm font-semibold text-ink">Delete this conversation?</p>
                            <p className="mt-1 text-xs leading-5 text-muted">This permanently removes it from your account.</p>
                            <div className="mt-3 flex justify-end gap-2">
                              <button type="button" onClick={() => setDeleteConfirmationId(null)} className="h-9 rounded-lg px-3 text-xs font-semibold text-ink transition hover:bg-stone">Cancel</button>
                              <button type="button" onClick={() => deleteConversation(conversation.id)} className="h-9 rounded-lg bg-pomegranate px-3 text-xs font-semibold text-white transition hover:bg-pomegranate-dark">Delete</button>
                            </div>
                          </div>
                        ) : (
                          <div className="absolute right-0 top-10 z-30 w-44 rounded-xl border border-line bg-paper p-1.5 shadow-menu" role="menu" aria-label={`Actions for ${conversation.title}`}>
                            <button type="button" onClick={() => startRename(conversation)} className="flex h-10 w-full items-center gap-2.5 rounded-lg px-3 text-left text-sm text-ink transition hover:bg-stone" role="menuitem">
                              <Pencil aria-hidden="true" className="size-4" strokeWidth={1.7} />
                              Rename
                            </button>
                            <button type="button" onClick={() => setDeleteConfirmationId(conversation.id)} className="flex h-10 w-full items-center gap-2.5 rounded-lg px-3 text-left text-sm text-pomegranate transition hover:bg-stone" role="menuitem">
                              <Trash2 aria-hidden="true" className="size-4" strokeWidth={1.7} />
                              Delete
                            </button>
                          </div>
                        )
                      ) : null}
                    </div>
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      </nav>

      <ProfileMenu user={user} onOpenSettings={onOpenSettings} onOpenPersonalization={onOpenPersonalization} onLogout={onLogout} />
    </aside>
  )
}
