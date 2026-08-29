import { useEffect, useRef, useState } from 'react'
import { Menu } from 'lucide-react'
import { Brand } from '../../components/Brand.tsx'
import type { AuthenticatedUser } from '../auth/authTypes.ts'
import { useAuth } from '../auth/useAuth.ts'
import type { ConversationSettingsClient } from '../personalization/conversationSettingsClient.ts'
import { PersonalizationPage } from '../personalization/PersonalizationPage.tsx'
import type { PersonalizationProfile } from '../personalization/personalizationTypes.ts'
import { SettingsPage } from '../settings/SettingsPage.tsx'
import type { UsageSummary, UserSettings } from '../settings/settingsTypes.ts'
import type { ConversationClient } from './conversationClient.ts'
import type { ConversationDetails, ConversationSummary } from './conversationData.ts'
import { ConversationSidebar } from './ConversationSidebar.tsx'
import { advanceConversationStarterIndex, ConversationStarters, getInitialConversationStarterIndex } from './conversationStarters.ts'
import { MessageComposer } from './MessageComposer.tsx'
import { AllSourceKeys, formatSourceSelection } from './sourceOptions.ts'

interface ConversationDashboardProps {
  user: AuthenticatedUser
  initialPersonalizationProfile: PersonalizationProfile
  initialUserSettings: UserSettings
  conversationClient: ConversationClient
  conversationSettingsClient: ConversationSettingsClient
  onSavePersonalization(profile: PersonalizationProfile): Promise<void>
  onSaveSettings(settings: UserSettings): Promise<UserSettings>
}

type ActiveView = 'conversation' | 'personalization' | 'settings'

export function ConversationDashboard({ user, initialPersonalizationProfile, initialUserSettings, conversationClient, conversationSettingsClient, onSavePersonalization, onSaveSettings }: ConversationDashboardProps) {
  const { requestPasswordReset, signOut } = useAuth()
  const [conversations, setConversations] = useState<ConversationSummary[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [selectedConversation, setSelectedConversation] = useState<ConversationDetails | null>(null)
  const [draft, setDraft] = useState('')
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false)
  const [isDesktopSidebarOpen, setIsDesktopSidebarOpen] = useState(true)
  const [activeView, setActiveView] = useState<ActiveView>('conversation')
  const [personalizationProfile, setPersonalizationProfile] = useState(initialPersonalizationProfile)
  const [userSettings, setUserSettings] = useState(initialUserSettings)
  const [usage, setUsage] = useState<UsageSummary | null>(null)
  const [usageError, setUsageError] = useState<string | null>(null)
  const [conversationStarterIndex, setConversationStarterIndex] = useState(getInitialConversationStarterIndex)
  const [unsavedSourceKeys, setUnsavedSourceKeys] = useState<string[]>(() => [...AllSourceKeys])
  const [isLoadingConversations, setIsLoadingConversations] = useState(true)
  const [isLoadingConversation, setIsLoadingConversation] = useState(false)
  const [isCreatingConversation, setIsCreatingConversation] = useState(false)
  const [isSending, setIsSending] = useState(false)
  const [pendingQuestion, setPendingQuestion] = useState<string | null>(null)
  const [isLoadingUsage, setIsLoadingUsage] = useState(false)
  const [conversationError, setConversationError] = useState<string | null>(null)
  const selectionRequestId = useRef(0)
  const sourceUpdateQueue = useRef(Promise.resolve())

  useEffect(() => {
    let isCurrent = true
    const requestId = selectionRequestId.current + 1
    selectionRequestId.current = requestId

    void conversationClient.list()
      .then(async (values) => {
        if (!isCurrent) {
          return
        }

        setConversations(values)
        if (values.length === 0) {
          return
        }

        const first = values[0]
        setSelectedId(first.id)
        const details = await conversationClient.get(first.id)
        if (isCurrent && selectionRequestId.current === requestId) {
          setSelectedConversation(details)
        }
      })
      .catch((error: unknown) => {
        if (isCurrent) {
          setConversationError(getErrorMessage(error, 'Your conversations could not be loaded.'))
        }
      })
      .finally(() => {
        if (isCurrent) {
          setIsLoadingConversations(false)
        }
      })

    return () => {
      isCurrent = false
    }
  }, [conversationClient])

  const personalizedUser = {
    ...user,
    name: personalizationProfile.fullName,
    initials: getInitials(personalizationProfile.fullName),
  }
  const conversationStarter = ConversationStarters[conversationStarterIndex] ?? ConversationStarters[0]
  const selectedSourceKeys = selectedConversation?.enabledSourceKeys ?? unsavedSourceKeys
  const messages = selectedConversation?.messages ?? []
  const displayedMessages = pendingQuestion === null ? messages : [...messages, { id: 'pending-user-message', role: 'User' as const, content: pendingQuestion, createdAtUtc: new Date().toISOString() }]

  async function handleNewConversation() {
    if (isCreatingConversation || isLoadingConversation || isLoadingConversations) {
      return
    }

    setIsCreatingConversation(true)
    setConversationError(null)
    try {
      const created = await conversationClient.create(undefined, AllSourceKeys)
      setConversations((current) => [toSummary(created), ...current])
      setSelectedId(created.id)
      setSelectedConversation(created)
      setUnsavedSourceKeys([...AllSourceKeys])
      setDraft('')
      setIsMobileSidebarOpen(false)
      setActiveView('conversation')
      setConversationStarterIndex((current) => advanceConversationStarterIndex(current))
    } catch (error) {
      setConversationError(getErrorMessage(error, 'A new conversation could not be created.'))
    } finally {
      setIsCreatingConversation(false)
    }
  }

  async function handleSelectConversation(id: string) {
    const requestId = selectionRequestId.current + 1
    selectionRequestId.current = requestId
    setSelectedId(id)
    setSelectedConversation(null)
    setConversationError(null)
    setIsLoadingConversation(true)
    setDraft('')
    setIsMobileSidebarOpen(false)
    setActiveView('conversation')
    try {
      const details = await conversationClient.get(id)
      if (selectionRequestId.current === requestId) {
        setSelectedConversation(details)
      }
    } catch (error) {
      if (selectionRequestId.current === requestId) {
        setConversationError(getErrorMessage(error, 'This conversation could not be loaded.'))
      }
    } finally {
      if (selectionRequestId.current === requestId) {
        setIsLoadingConversation(false)
      }
    }
  }

  function handleOpenPersonalization() {
    setIsMobileSidebarOpen(false)
    setActiveView('personalization')
  }

  function handleOpenSettings() {
    setIsMobileSidebarOpen(false)
    setActiveView('settings')
    if (usage === null && !isLoadingUsage) {
      void loadUsage()
    }
  }

  async function loadUsage() {
    setIsLoadingUsage(true)
    setUsageError(null)
    try {
      setUsage(await conversationSettingsClient.getUsage())
    } catch (error) {
      setUsageError(getErrorMessage(error, 'Usage could not be loaded.'))
    } finally {
      setIsLoadingUsage(false)
    }
  }

  async function handleSavePersonalization(profile: PersonalizationProfile) {
    await onSavePersonalization(profile)
    setPersonalizationProfile(profile)
  }

  async function handleSaveSettings(settings: UserSettings) {
    setUserSettings(await onSaveSettings(settings))
  }

  async function handleRenameConversation(id: string, title: string) {
    const previous = conversations.find((conversation) => conversation.id === id)?.title
    setConversations((current) => current.map((conversation) => conversation.id === id ? { ...conversation, title } : conversation))
    setSelectedConversation((current) => current?.id === id ? { ...current, title } : current)
    try {
      await conversationClient.rename(id, title)
    } catch (error) {
      if (previous !== undefined) {
        setConversations((current) => current.map((conversation) => conversation.id === id ? { ...conversation, title: previous } : conversation))
        setSelectedConversation((current) => current?.id === id ? { ...current, title: previous } : current)
      }
      setConversationError(getErrorMessage(error, 'The conversation could not be renamed.'))
    }
  }

  async function handleDeleteConversation(id: string) {
    setConversationError(null)
    try {
      await conversationClient.delete(id)
      const remaining = conversations.filter((conversation) => conversation.id !== id)
      setConversations(remaining)
      if (selectedId === id) {
        selectionRequestId.current += 1
        setIsLoadingConversation(false)
        const next = remaining[0]
        setSelectedId(next?.id ?? null)
        setSelectedConversation(null)
        setDraft('')
        if (next !== undefined) {
          await handleSelectConversation(next.id)
        }
      }
    } catch (error) {
      setConversationError(getErrorMessage(error, 'The conversation could not be deleted.'))
    }
  }

  async function handleSubmit() {
    const question = draft.trim()
    if (question.length === 0 || selectedSourceKeys.length === 0 || isSending || isLoadingConversation || isLoadingConversations) {
      return
    }

    setIsSending(true)
    setPendingQuestion(question)
    setConversationError(null)
    try {
      let conversation = selectedConversation
      if (conversation === null) {
        conversation = await conversationClient.create(undefined, selectedSourceKeys)
        setSelectedId(conversation.id)
      }

      const turn = await conversationClient.appendMessage(conversation.id, crypto.randomUUID(), question)
      conversation = turn.conversation
      if (conversation.title === 'New conversation') {
        const title = question.slice(0, 80)
        await conversationClient.rename(conversation.id, title)
        conversation = { ...conversation, title }
      }

      setSelectedConversation(conversation)
      setConversations((current) => [toSummary(conversation), ...current.filter((value) => value.id !== conversation.id)])
      setDraft('')
      if (turn.status !== 'answered') {
        setConversationError(turn.message ?? 'AskRabbi could not create a validated source-grounded answer. Please try again.')
      }
    } catch (error) {
      setConversationError(getErrorMessage(error, 'Your message could not be saved.'))
    } finally {
      setIsSending(false)
      setPendingQuestion(null)
    }
  }

  function handleSelectedSourceKeysChange(sourceKeys: string[]) {
    if (selectedId === null || selectedConversation === null) {
      setUnsavedSourceKeys(sourceKeys)
      return
    }

    const conversationId = selectedId
    setSelectedConversation((current) => current?.id === conversationId ? { ...current, enabledSourceKeys: sourceKeys } : current)
    setConversations((current) => current.map((conversation) => conversation.id === conversationId ? { ...conversation, enabledSourceKeys: sourceKeys } : conversation))
    if (sourceKeys.length === 0) {
      return
    }

    sourceUpdateQueue.current = sourceUpdateQueue.current
      .catch(() => undefined)
      .then(() => conversationClient.updateSources(conversationId, sourceKeys))
      .catch((error: unknown) => {
        setConversationError(getErrorMessage(error, 'The source selection could not be saved.'))
      })
  }

  return (
    <div className="flex h-dvh overflow-hidden bg-parchment">
      <ConversationSidebar
        conversations={conversations}
        selectedId={selectedId}
        isMobileOpen={isMobileSidebarOpen}
        isDesktopOpen={isDesktopSidebarOpen}
        isCreatingConversation={isCreatingConversation}
        isNewConversationDisabled={isCreatingConversation || isLoadingConversation || isLoadingConversations}
        user={personalizedUser}
        onCloseMobile={() => setIsMobileSidebarOpen(false)}
        onNewConversation={() => void handleNewConversation()}
        onSelectConversation={(id) => void handleSelectConversation(id)}
        onRenameConversation={(id, title) => void handleRenameConversation(id, title)}
        onDeleteConversation={(id) => void handleDeleteConversation(id)}
        onOpenSettings={handleOpenSettings}
        onOpenPersonalization={handleOpenPersonalization}
        onLogout={signOut}
      />

      {isMobileSidebarOpen ? <button type="button" aria-label="Close conversation navigation" onClick={() => setIsMobileSidebarOpen(false)} className="fixed inset-0 z-30 bg-ink/45 lg:hidden" /> : null}

      <main className="relative flex min-w-0 flex-1 flex-col bg-parchment">
        <header className="flex h-16 shrink-0 items-center border-b border-line px-4 sm:px-5">
          <button type="button" onClick={() => setIsMobileSidebarOpen(true)} className="flex size-11 items-center justify-center rounded-lg text-ink transition hover:bg-stone lg:hidden" aria-label="Open conversation navigation">
            <Menu aria-hidden="true" className="size-5" strokeWidth={1.75} />
          </button>
          <button type="button" onClick={() => setIsDesktopSidebarOpen((current) => !current)} className="hidden size-11 items-center justify-center rounded-lg text-ink transition hover:bg-stone lg:flex" aria-label="Toggle conversation navigation">
            <Menu aria-hidden="true" className="size-5" strokeWidth={1.75} />
          </button>
          <div className="mx-auto lg:hidden"><Brand compact /></div>
          <div className="size-11 lg:hidden" aria-hidden="true" />
        </header>

        <span className="pointer-events-none absolute right-4 top-20 hidden size-8 border-r border-t border-brass/60 sm:block" aria-hidden="true" />
        <span className="pointer-events-none absolute bottom-4 left-4 hidden size-8 border-b border-l border-brass/60 sm:block" aria-hidden="true" />

        {activeView === 'settings' ? (
          <SettingsPage user={personalizedUser} settings={userSettings} usage={usage} usageError={usageError} isLoadingUsage={isLoadingUsage} onRetryUsage={() => void loadUsage()} onBack={() => setActiveView('conversation')} onSave={handleSaveSettings} onRequestPasswordReset={() => requestPasswordReset(user.email)} />
        ) : activeView === 'personalization' ? (
          <PersonalizationPage profile={personalizationProfile} onBack={() => setActiveView('conversation')} onSave={handleSavePersonalization} />
        ) : (
          <section className="flex min-h-0 flex-1 flex-col overflow-y-auto px-4 sm:px-8" aria-label="Current conversation">
            <div className="mx-auto flex w-full max-w-[62rem] flex-1 flex-col">
              {conversationError === null ? null : <p className="mx-auto mt-5 w-full max-w-[46rem] rounded-lg border border-pomegranate/25 bg-pomegranate/5 px-4 py-3 text-sm text-pomegranate" role="alert">{conversationError}</p>}
              {isLoadingConversations || isLoadingConversation ? (
                <div className="flex flex-1 items-center justify-center"><p className="text-sm text-muted" role="status">Loading conversation…</p></div>
              ) : displayedMessages.length === 0 ? (
                <div className="enter-softly flex flex-1 flex-col items-center justify-center px-2 pb-6 pt-10 text-center sm:pb-10">
                  <h1 className="max-w-[50rem] font-display text-[clamp(2.65rem,5vw,4.15rem)] leading-[1.02] tracking-[-0.045em] text-ink">{conversationStarter.heading}</h1>
                  <p className="mt-6 max-w-[39rem] text-base leading-7 text-ink-soft sm:text-lg">{conversationStarter.supportingText}</p>
                </div>
              ) : (
                <div className="flex-1 py-10 sm:py-14">
                  <article className="mx-auto max-w-[46rem] space-y-9">
                    {displayedMessages.map((message) => (
                      <div key={message.id} className={message.role === 'Assistant' ? 'border-l-2 border-pomegranate pl-5' : ''}>
                        <p className={`mb-2 ${message.role === 'Assistant' ? 'font-display text-xl text-ink' : 'text-xs font-semibold uppercase tracking-[0.14em] text-muted'}`}>{message.role === 'Assistant' ? 'AskRabbi' : 'You'}</p>
                        <p className="whitespace-pre-wrap text-base leading-7 text-ink sm:text-lg">{message.content}</p>
                      </div>
                    ))}
                    {isSending ? (
                      <div className="border-l-2 border-brass pl-5">
                        <p className="mb-2 font-display text-xl text-ink">AskRabbi</p>
                        <p className="leading-7 text-ink-soft" role="status">Finding relevant passages in {formatSourceSelection(selectedSourceKeys).toLowerCase()}, checking the quotations, and preparing a grounded answer…</p>
                      </div>
                    ) : null}
                  </article>
                </div>
              )}

              <div className="flex justify-center pb-5 sm:pb-7">
                <MessageComposer draft={draft} selectedSourceKeys={selectedSourceKeys} conversationLanguage={personalizationProfile.conversationLanguage} quotationLanguage={personalizationProfile.quotationLanguage} isSending={isSending || isLoadingConversation || isLoadingConversations} onDraftChange={setDraft} onSelectedSourceKeysChange={handleSelectedSourceKeysChange} onSubmit={() => void handleSubmit()} />
              </div>
            </div>
          </section>
        )}
      </main>
    </div>
  )
}

function toSummary(conversation: ConversationDetails): ConversationSummary {
  return {
    id: conversation.id,
    title: conversation.title,
    enabledSourceKeys: conversation.enabledSourceKeys,
    updatedAtUtc: conversation.updatedAtUtc,
  }
}

function getInitials(name: string) {
  return name
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')
}

function getErrorMessage(error: unknown, fallback: string) {
  return error instanceof Error && error.message.trim().length > 0 ? error.message : fallback
}
