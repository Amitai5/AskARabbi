import { useState } from 'react'
import { Menu } from 'lucide-react'
import { Brand } from '../../components/Brand.tsx'
import type { AuthenticatedUser } from '../auth/authTypes.ts'
import { useAuth } from '../auth/useAuth.ts'
import { ConversationSidebar } from './ConversationSidebar.tsx'
import { InitialConversations, type ConversationSummary } from './conversationData.ts'
import { MessageComposer } from './MessageComposer.tsx'

interface ConversationDashboardProps {
  user: AuthenticatedUser
}

export function ConversationDashboard({ user }: ConversationDashboardProps) {
  const { signOut } = useAuth()
  const [conversations, setConversations] = useState<ConversationSummary[]>(() => InitialConversations)
  const [selectedId, setSelectedId] = useState(InitialConversations[0].id)
  const [draft, setDraft] = useState('')
  const [submittedQuestion, setSubmittedQuestion] = useState<string | null>(null)
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false)
  const [isDesktopSidebarOpen, setIsDesktopSidebarOpen] = useState(true)

  function handleNewConversation() {
    const id = crypto.randomUUID()
    setConversations((current) => [{ id, title: 'New conversation' }, ...current])
    setSelectedId(id)
    setSubmittedQuestion(null)
    setDraft('')
    setIsMobileSidebarOpen(false)
  }

  function handleSelectConversation(id: string) {
    setSelectedId(id)
    setSubmittedQuestion(null)
    setDraft('')
    setIsMobileSidebarOpen(false)
  }

  function handleSubmit() {
    const question = draft.trim()
    if (question.length === 0) {
      return
    }

    setConversations((current) => current.map((conversation) => conversation.id === selectedId && conversation.title === 'New conversation'
      ? { ...conversation, title: question.slice(0, 42) }
      : conversation))
    setSubmittedQuestion(question)
    setDraft('')
  }

  return (
    <div className="flex h-dvh overflow-hidden bg-parchment">
      <ConversationSidebar
        conversations={conversations}
        selectedId={selectedId}
        isMobileOpen={isMobileSidebarOpen}
        isDesktopOpen={isDesktopSidebarOpen}
        user={user}
        onCloseMobile={() => setIsMobileSidebarOpen(false)}
        onNewConversation={handleNewConversation}
        onSelectConversation={handleSelectConversation}
        onLogout={signOut}
      />

      {isMobileSidebarOpen ? (
        <button type="button" aria-label="Close conversation navigation" onClick={() => setIsMobileSidebarOpen(false)} className="fixed inset-0 z-30 bg-ink/45 lg:hidden" />
      ) : null}

      <main className="relative flex min-w-0 flex-1 flex-col bg-parchment">
        <header className="flex h-16 shrink-0 items-center border-b border-line px-4 sm:px-5">
          <button type="button" onClick={() => setIsMobileSidebarOpen(true)} className="flex size-11 items-center justify-center rounded-lg text-ink transition hover:bg-stone lg:hidden" aria-label="Open conversation navigation">
            <Menu aria-hidden="true" className="size-5" strokeWidth={1.75} />
          </button>
          <button type="button" onClick={() => setIsDesktopSidebarOpen((current) => !current)} className="hidden size-11 items-center justify-center rounded-lg text-ink transition hover:bg-stone lg:flex" aria-label="Toggle conversation navigation">
            <Menu aria-hidden="true" className="size-5" strokeWidth={1.75} />
          </button>
          <div className="mx-auto lg:hidden">
            <Brand compact />
          </div>
          <div className="size-11 lg:hidden" aria-hidden="true" />
        </header>

        <span className="pointer-events-none absolute right-4 top-20 hidden size-8 border-r border-t border-brass/60 sm:block" aria-hidden="true" />
        <span className="pointer-events-none absolute bottom-4 left-4 hidden size-8 border-b border-l border-brass/60 sm:block" aria-hidden="true" />

        <section className="flex min-h-0 flex-1 flex-col overflow-y-auto px-4 sm:px-8" aria-label="Current conversation">
          <div className="mx-auto flex w-full max-w-[62rem] flex-1 flex-col">
            {submittedQuestion === null ? (
              <div className="enter-softly flex flex-1 flex-col items-center justify-center px-2 pb-6 pt-10 text-center sm:pb-10">
                <h1 className="max-w-[50rem] font-display text-[clamp(2.65rem,5vw,4.15rem)] leading-[1.02] tracking-[-0.045em] text-ink">
                  What shall we study together?
                </h1>
                <p className="mt-6 max-w-[39rem] text-base leading-7 text-ink-soft sm:text-lg">
                  Ask a question, follow a source, or begin with something you have always wondered.
                </p>
              </div>
            ) : (
              <div className="flex-1 py-10 sm:py-14">
                <article className="mx-auto max-w-[46rem] space-y-8">
                  <div>
                    <p className="mb-2 text-xs font-semibold uppercase tracking-[0.14em] text-muted">You</p>
                    <p className="text-base leading-7 text-ink sm:text-lg">{submittedQuestion}</p>
                  </div>
                  <div className="border-l-2 border-pomegranate pl-5">
                    <p className="mb-2 font-display text-xl text-ink">AskRabbi</p>
                    <p className="leading-7 text-ink-soft">
                      This production shell is ready for the grounded-answer API. For now, your question stays in this local demo and no AI request is sent.
                    </p>
                  </div>
                </article>
              </div>
            )}

            <div className="flex justify-center pb-5 sm:pb-7">
              <MessageComposer draft={draft} onDraftChange={setDraft} onSubmit={handleSubmit} />
            </div>
          </div>
        </section>
      </main>
    </div>
  )
}
