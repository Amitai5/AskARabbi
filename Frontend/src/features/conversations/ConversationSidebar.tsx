import { MessageCircle, Plus, X } from 'lucide-react'
import { Brand } from '../../components/Brand.tsx'
import type { AuthenticatedUser } from '../auth/authTypes.ts'
import type { ConversationSummary } from './conversationData.ts'
import { ProfileMenu } from './ProfileMenu.tsx'

interface ConversationSidebarProps {
  conversations: ConversationSummary[]
  selectedId: string
  isMobileOpen: boolean
  isDesktopOpen: boolean
  user: AuthenticatedUser
  onCloseMobile(): void
  onNewConversation(): void
  onSelectConversation(id: string): void
  onLogout(): Promise<void>
}

export function ConversationSidebar({ conversations, selectedId, isMobileOpen, isDesktopOpen, user, onCloseMobile, onNewConversation, onSelectConversation, onLogout }: ConversationSidebarProps) {
  const desktopVisibility = isDesktopOpen ? 'lg:visible lg:w-72 lg:translate-x-0' : 'lg:invisible lg:w-0 lg:-translate-x-full lg:border-r-0'
  const mobileVisibility = isMobileOpen ? 'visible translate-x-0' : 'invisible -translate-x-full'

  return (
    <aside className={`fixed inset-y-0 left-0 z-40 flex h-dvh w-[min(20rem,calc(100vw-2.5rem))] shrink-0 flex-col overflow-hidden border-r border-line bg-stone transition-[transform,width] duration-300 ease-out lg:relative lg:z-0 ${mobileVisibility} ${desktopVisibility}`} aria-label="Conversation navigation">
      <div className="flex items-center justify-between px-5 pb-5 pt-6">
        <Brand compact />
        <button type="button" onClick={onCloseMobile} className="flex size-11 items-center justify-center rounded-lg text-ink transition hover:bg-stone-deep lg:hidden" aria-label="Close conversation navigation">
          <X aria-hidden="true" className="size-5" strokeWidth={1.75} />
        </button>
      </div>

      <div className="px-4">
        <button type="button" onClick={onNewConversation} className="flex h-13 w-full items-center justify-center gap-2.5 rounded-lg bg-pomegranate px-4 text-sm font-semibold text-white transition hover:bg-pomegranate-dark">
          <Plus aria-hidden="true" className="size-5" strokeWidth={1.75} />
          New conversation
        </button>
      </div>

      <nav className="mt-7 min-h-0 flex-1 overflow-y-auto px-3 pb-4" aria-label="Recent conversations">
        <p className="px-3 pb-2 text-xs font-semibold uppercase tracking-[0.14em] text-muted">Recent</p>
        <ul className="space-y-1">
          {conversations.map((conversation) => {
            const isSelected = conversation.id === selectedId
            return (
              <li key={conversation.id}>
                <button
                  type="button"
                  onClick={() => onSelectConversation(conversation.id)}
                  aria-current={isSelected ? 'page' : undefined}
                  className={`relative flex min-h-11 w-full items-center gap-3 rounded-lg px-3 text-left text-sm text-ink transition hover:bg-stone-deep/70 ${isSelected ? 'bg-stone-deep/70 font-semibold' : ''}`}
                >
                  {isSelected ? <span className="absolute inset-y-2 left-0 w-0.5 rounded-full bg-pomegranate" /> : null}
                  <MessageCircle aria-hidden="true" className="size-[1.1rem] shrink-0" strokeWidth={1.65} />
                  <span className="truncate">{conversation.title}</span>
                </button>
              </li>
            )
          })}
        </ul>
      </nav>

      <ProfileMenu user={user} onLogout={onLogout} />
    </aside>
  )
}
