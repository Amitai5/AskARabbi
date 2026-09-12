import { lazy, Suspense, useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/apiClient.ts'
import { ChatUsageNotice } from './ChatUsageNotice.tsx'
import { useMonthlyUsage } from '../settings/useMonthlyUsage.ts'
import { Menu } from 'lucide-react'
import { Brand } from '../../components/Brand.tsx'
import type { AuthenticatedUser } from '../auth/authTypes.ts'
import { useAuth } from '../auth/useAuth.ts'
import type { DvarTorahClient } from '../dvarTorah/dvarTorahClient.ts'
import type { CalendarClient } from '../calendar/calendarClient.ts'
import type { ConversationSettingsClient } from '../personalization/conversationSettingsClient.ts'
import type { PersonalizationProfile } from '../personalization/personalizationTypes.ts'
import { UnifiedSettingsPage } from '../settings/UnifiedSettingsPage.tsx'
import { SettingsSidebar } from '../settings/SettingsSidebar.tsx'
import { readSettingsRoute, SettingsRegistry, type SettingsSectionId } from '../settings/settingsRegistry.ts'
import { FocusedReadingProvider, FocusedReadingToolbar } from '../reading/FocusedReading.tsx'
import { useFocusedReading } from '../reading/focusedReadingContext.ts'
import { publishUserDataEvent, subscribeToUserDataEvents } from '../settings/userDataEvents.ts'
import type { UserSettings } from '../settings/settingsTypes.ts'
import type { ConversationClient, ConversationTurn } from './conversationClient.ts'
import type { ConversationDetails, ConversationMessage, ConversationSummary } from './conversationData.ts'
import { normalizeConversationTitle } from './conversationData.ts'
import { AnswerProgress } from './AnswerProgress.tsx'
import { AssistantMessage } from './AssistantMessage.tsx'
import { ConversationSidebar } from './ConversationSidebar.tsx'
import { advanceConversationStarterIndex, ConversationStarters, getInitialConversationStarterIndex } from './conversationStarters.ts'
import { MessageComposer } from './MessageComposer.tsx'
import { AllSourceKeys, formatSourceSelection } from './sourceOptions.ts'
import { SourceReader } from './SourceReader.tsx'
import { UserMessage } from './UserMessage.tsx'
import { useOnlineStatus } from '../pwa/useOnlineStatus.ts'
import { PrintAction } from '../printing/PrintAction.tsx'
import { collectPrintAnswers, type PrintRequest } from '../printing/printTypes.ts'

const WeeklyDvarTorahPage = lazy(() => import('../dvarTorah/WeeklyDvarTorahPage.tsx').then((module) => ({ default: module.WeeklyDvarTorahPage })))
const CalendarPage = lazy(() => import('../calendar/CalendarPage.tsx').then((module) => ({ default: module.CalendarPage })))
const DashboardScrollLockClass = 'conversation-dashboard-scroll-lock'

interface ConversationDashboardProps {
  user: AuthenticatedUser
  initialPersonalizationProfile: PersonalizationProfile
  initialUserSettings: UserSettings
  conversationClient: ConversationClient
  conversationSettingsClient: ConversationSettingsClient
  dvarTorahClient: DvarTorahClient
  calendarClient: CalendarClient
  onSavePersonalization(profile: PersonalizationProfile): Promise<PersonalizationProfile>
  onSaveSettings(settings: UserSettings): Promise<UserSettings>
}

type ActiveView = 'conversation' | 'dvarTorah' | 'calendar' | 'settings'

interface SourceReaderSelection {
  messageId: string
  sourceNumber: number
}

interface ActiveSourceReader {
  messageId: string
  sources: NonNullable<ConversationMessage['sources']>
  selectedIndex: number
}

interface ConversationSession {
  conversation: ConversationDetails
  isNew: boolean
  draft: string
  error: string | null
}

export function ConversationDashboard(props: ConversationDashboardProps) {
  return <FocusedReadingProvider><DashboardContent {...props} /></FocusedReadingProvider>
}

function DashboardContent({ user, initialPersonalizationProfile, initialUserSettings, conversationClient, conversationSettingsClient, dvarTorahClient, calendarClient, onSavePersonalization, onSaveSettings }: ConversationDashboardProps) {
  const { requestPasswordReset, signOut, deleteAccount } = useAuth()
  const [conversations, setConversations] = useState<ConversationSummary[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [selectedConversation, setSelectedConversation] = useState<ConversationDetails | null>(null)
  const [draft, setDraft] = useState('')
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false)
  const closeMobileSidebar = useCallback(() => setIsMobileSidebarOpen(false), [])
  const [activeView, setActiveView] = useState<ActiveView>(() => readSettingsRoute() ? 'settings' : 'conversation')
  const [settingsSection, setSettingsSection] = useState<SettingsSectionId>(() => readSettingsRoute() ?? 'account')
  const [targetSetting, setTargetSetting] = useState<string | null>(readSettingHash)
  const [settingsNavigationKey, setSettingsNavigationKey] = useState(0)
  const settingsReturnView = useRef<ActiveView>('conversation')
  const focusedReading = useFocusedReading()
  const { exit: exitFocusedReading } = focusedReading
  const [personalizationProfile, setPersonalizationProfile] = useState(initialPersonalizationProfile)
  const [userSettings, setUserSettings] = useState(initialUserSettings)
  const { usage, error: usageError, isLoading: isLoadingUsage, refresh: loadUsage, update: updateUsage } = useMonthlyUsage(conversationSettingsClient, user.id)
  const isOnline = useOnlineStatus()
  const isChatDisabled = !isOnline || usage === null || usage.isLimitReached
  const [conversationStarterIndex, setConversationStarterIndex] = useState(getInitialConversationStarterIndex)
  const [unsavedSourceKeys, setUnsavedSourceKeys] = useState<string[]>(() => [...AllSourceKeys])
  const [isLoadingConversations, setIsLoadingConversations] = useState(true)
  const [isLoadingConversation, setIsLoadingConversation] = useState(false)
  const [pendingQuestions, setPendingQuestions] = useState<ReadonlyMap<string, ConversationMessage>>(() => new Map())
  const [conversationError, setConversationError] = useState<string | null>(null)
  const [sourceReaderSelection, setSourceReaderSelection] = useState<SourceReaderSelection | null>(null)
  const selectionRequestId = useRef(0)
  const dataGeneration = useRef(0)
  const selectedIdRef = useRef<string | null>(null)
  // Keep in-flight and completed turns in the dashboard, not the currently visible page.
  const conversationSessions = useRef(new Map<string, ConversationSession>())
  const sendingConversationIds = useRef(new Set<string>())
  const sourceUpdateQueues = useRef(new Map<string, Promise<boolean>>())
  const sourceReaderTriggerRef = useRef<HTMLButtonElement | null>(null)
  const conversationScrollRef = useRef<HTMLElement | null>(null)
  const shouldScrollToLatestRef = useRef(true)

  useEffect(() => {
    const desktop = window.matchMedia?.('(min-width: 64rem)')
    const closeDrawerOnDesktop = () => { if (desktop?.matches) { setIsMobileSidebarOpen(false) } }
    desktop?.addEventListener('change', closeDrawerOnDesktop)
    return () => desktop?.removeEventListener('change', closeDrawerOnDesktop)
  }, [])

  useEffect(() => {
    const section = readSettingsRoute()
    if (section) { window.history.replaceState(window.history.state, '', `/settings/${section}${window.location.hash}`) }
    function navigateHistory() {
      exitFocusedReading()
      const next = readSettingsRoute()
      if (next) {
        setSettingsSection(next)
        setTargetSetting(readSettingHash())
        setSettingsNavigationKey(key => key + 1)
        setActiveView('settings')
      } else {
        const view = window.history.state?.askarabbiView
        setActiveView(view === 'calendar' || view === 'dvarTorah' ? view : 'conversation')
        shouldScrollToLatestRef.current = true
      }
      setIsMobileSidebarOpen(false)
    }
    window.addEventListener('popstate', navigateHistory)
    return () => window.removeEventListener('popstate', navigateHistory)
  }, [exitFocusedReading])

  function navigateView(view: Exclude<ActiveView, 'settings'>) {
    if (readSettingsRoute() || window.history.state?.askarabbiView !== view) {
      window.history.pushState({ ...window.history.state, askarabbiView: view }, '', '/')
    }
    focusedReading.exit()
    setActiveView(view)
  }

  function navigateSettings(section: SettingsSectionId, settingId?: string, keepNavigationOpen = false) {
    if (activeView !== 'settings') { settingsReturnView.current = activeView }
    focusedReading.exit()
    window.history.pushState(window.history.state, '', `/settings/${section}${settingId ? `#${settingId}` : ''}`)
    setSettingsSection(section)
    setTargetSetting(settingId ?? null)
    setSettingsNavigationKey(key => key + 1)
    setActiveView('settings')
    if (!keepNavigationOpen) { setIsMobileSidebarOpen(false) }
    setSourceReaderSelection(null)
  }

  const clearChatState = useCallback(() => {
    dataGeneration.current += 1
    selectionRequestId.current += 1
    selectedIdRef.current = null
    conversationSessions.current.clear()
    sourceUpdateQueues.current.clear()
    sendingConversationIds.current.clear()
    setConversations([])
    setSelectedId(null)
    setSelectedConversation(null)
    setPendingQuestions(new Map())
    setSourceReaderSelection(null)
    setConversationError(null)
    setIsLoadingConversation(false)
    setDraft('')
  }, [])

  useEffect(() => subscribeToUserDataEvents(user.id, (event) => {
    if (event.kind === 'chats-deleted') { clearChatState() }
  }), [clearChatState, user.id])

  useEffect(() => {
    document.documentElement.classList.add(DashboardScrollLockClass)
    document.body.classList.add(DashboardScrollLockClass)

    return () => {
      document.documentElement.classList.remove(DashboardScrollLockClass)
      document.body.classList.remove(DashboardScrollLockClass)
    }
  }, [])

  const handleOpenSourceReader = useCallback((messageId: string, sourceNumber: number, trigger: HTMLButtonElement) => {
    sourceReaderTriggerRef.current = trigger
    setSourceReaderSelection({ messageId, sourceNumber })
  }, [])

  const handleSelectReaderSource = useCallback((sourceNumber: number) => {
    setSourceReaderSelection((current) => current === null ? null : { ...current, sourceNumber })
  }, [])

  const handleCloseSourceReader = useCallback(() => {
    const trigger = sourceReaderTriggerRef.current
    setSourceReaderSelection(null)
    if (trigger?.isConnected === true) {
      trigger.focus()
    }
  }, [])

  useEffect(() => {
    let isCurrent = true
    const generation = dataGeneration.current
    const requestId = selectionRequestId.current + 1
    selectionRequestId.current = requestId

    void conversationClient.list()
      .then(async (values) => {
        if (!isCurrent || dataGeneration.current !== generation) {
          return
        }

        setConversations(values)
        if (values.length === 0) {
          return
        }

        const first = values[0]
        shouldScrollToLatestRef.current = true
        selectedIdRef.current = first.id
        setSelectedId(first.id)
        const details = await conversationClient.get(first.id)
        if (isCurrent && selectionRequestId.current === requestId) {
          setSelectedConversation(details)
          setConversations((current) => reconcileConversationSummary(current, details))
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
  const pendingQuestion = selectedId === null ? null : pendingQuestions.get(selectedId) ?? null
  const isSending = pendingQuestion !== null
  const displayedMessages = pendingQuestion === null || messages.some((message) => message.id === pendingQuestion.id) ? messages : [...messages, pendingQuestion]
  const latestDisplayedMessageId = displayedMessages.at(-1)?.id ?? null
  const activeSourceReader = resolveActiveSourceReader(displayedMessages, sourceReaderSelection)
  const getAnswerPrintRequest = useCallback((initialAnswerId?: string): PrintRequest => ({ kind: 'answers', title: normalizeConversationTitle(selectedConversation?.title), answers: collectPrintAnswers(selectedConversation?.messages ?? []), initialAnswerId }), [selectedConversation])

  useLayoutEffect(() => {
    if (!shouldScrollToLatestRef.current || focusedReading.target !== null || activeView !== 'conversation' || isLoadingConversations || isLoadingConversation) {
      return
    }

    const scrollContainer = conversationScrollRef.current
    if (scrollContainer === null) {
      return
    }

    shouldScrollToLatestRef.current = false
    const scrollToLatest = () => scrollContainer.scrollTo({ top: scrollContainer.scrollHeight, behavior: 'auto' })
    scrollToLatest()

    let finalFrame = 0
    const layoutFrame = window.requestAnimationFrame(() => {
      scrollToLatest()
      finalFrame = window.requestAnimationFrame(scrollToLatest)
    })
    return () => {
      window.cancelAnimationFrame(layoutFrame)
      window.cancelAnimationFrame(finalFrame)
    }
  }, [activeView, focusedReading.target, isLoadingConversation, isLoadingConversations, latestDisplayedMessageId, selectedId])

  function handleNewConversation() {
    if (!navigator.onLine || isLoadingConversations) {
      return
    }

    rememberSelectedConversation()
    selectionRequestId.current += 1
    shouldScrollToLatestRef.current = true
    setConversationError(null)
    selectedIdRef.current = null
    setSelectedId(null)
    setSelectedConversation(null)
    setIsLoadingConversation(false)
    setSourceReaderSelection(null)
    setUnsavedSourceKeys([...AllSourceKeys])
    setDraft('')
    setIsMobileSidebarOpen(false)
    navigateView('conversation')
    setConversationStarterIndex((current) => advanceConversationStarterIndex(current))
  }

  async function handleSelectConversation(id: string) {
    if (!navigator.onLine) { return }
    rememberSelectedConversation()
    const requestId = selectionRequestId.current + 1
    selectionRequestId.current = requestId
    shouldScrollToLatestRef.current = true
    selectedIdRef.current = id
    setSelectedId(id)
    setSelectedConversation(null)
    setSourceReaderSelection(null)
    setConversationError(null)
    setIsLoadingConversation(true)
    setDraft('')
    setIsMobileSidebarOpen(false)
    navigateView('conversation')
    const session = conversationSessions.current.get(id)
    if (session !== undefined) {
      setSelectedConversation(session.conversation)
      setDraft(session.draft)
      setConversationError(session.error)
      setIsLoadingConversation(false)
      return
    }

    try {
      const details = await conversationClient.get(id)
      if (selectionRequestId.current === requestId) {
        setSelectedConversation(details)
        setConversations((current) => reconcileConversationSummary(current, details))
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
    setSourceReaderSelection(null)
    navigateSettings('personalization')
  }

  function handleOpenDvarTorah() {
    if (!navigator.onLine) {
      window.location.assign('/offline.html')
      return
    }
    setIsMobileSidebarOpen(false)
    setSourceReaderSelection(null)
    navigateView('dvarTorah')
  }

  function handleOpenSettings(settingId?: string) {
    setIsMobileSidebarOpen(false)
    setSourceReaderSelection(null)
    navigateSettings('account', settingId)
    if (usage === null && !isLoadingUsage) {
      void loadUsage()
    }
  }

  function handleOpenCalendar() {
    setIsMobileSidebarOpen(false)
    setSourceReaderSelection(null)
    navigateView('calendar')
  }

  function handleBackToConversation() {
    shouldScrollToLatestRef.current = true
    navigateView('conversation')
  }

  async function handleSavePersonalization(profile: PersonalizationProfile) {
    const saved = await onSavePersonalization(profile)
    setPersonalizationProfile(saved)
    return saved
  }

  async function handleSaveSettings(settings: UserSettings) {
    setUserSettings(await onSaveSettings(settings))
  }

  async function handleRenameConversation(id: string, title: string) {
    if (!navigator.onLine || sendingConversationIds.current.has(id)) {
      return
    }

    const previous = conversations.find((conversation) => conversation.id === id)?.title
    setConversations((current) => current.map((conversation) => conversation.id === id ? { ...conversation, title } : conversation))
    setSelectedConversation((current) => current?.id === id ? { ...current, title } : current)
    try {
      if (conversationSessions.current.get(id)?.isNew !== true) {
        await conversationClient.rename(id, title)
      }
      const session = conversationSessions.current.get(id)
      if (session !== undefined) {
        session.conversation = { ...session.conversation, title }
      }
    } catch (error) {
      if (previous !== undefined) {
        setConversations((current) => current.map((conversation) => conversation.id === id ? { ...conversation, title: previous } : conversation))
        setSelectedConversation((current) => current?.id === id ? { ...current, title: previous } : current)
      }
      setConversationError(getErrorMessage(error, 'The conversation could not be renamed.'))
    }
  }

  async function handleDeleteAllChats() {
    if (sendingConversationIds.current.size > 0) {
      throw new Error('Wait for your current answers to finish before deleting chats.')
    }
    await Promise.all(sourceUpdateQueues.current.values())
    await conversationClient.deleteAll()
    clearChatState()
    publishUserDataEvent({ userId: user.id, kind: 'chats-deleted' })
  }

  async function handleDeleteConversation(id: string) {
    if (!navigator.onLine || sendingConversationIds.current.has(id)) {
      return
    }

    setConversationError(null)
    try {
      if (conversationSessions.current.get(id)?.isNew !== true) {
        await conversationClient.delete(id)
      }
      conversationSessions.current.delete(id)
      sourceUpdateQueues.current.delete(id)
      const remaining = conversations.filter((conversation) => conversation.id !== id)
      setConversations((current) => current.filter((conversation) => conversation.id !== id))
      if (selectedIdRef.current === id) {
        selectionRequestId.current += 1
        setSourceReaderSelection(null)
        setIsLoadingConversation(false)
        const next = remaining[0]
        selectedIdRef.current = next?.id ?? null
        setSelectedId(next?.id ?? null)
        setSelectedConversation(null)
        setDraft('')
        if (next !== undefined) {
          await handleSelectConversation(next.id)
        }
      }
    } catch (error) {
      setConversationError(getErrorMessage(error, 'The conversation could not be deleted.'))
      throw error
    }
  }

  async function handleSubmit() {
    const generation = dataGeneration.current
    const question = draft.trim()
    if (!navigator.onLine || isChatDisabled || pendingQuestions.size > 0 || question.length === 0 || selectedSourceKeys.length === 0 || isSending || isLoadingConversation || isLoadingConversations || selectedId !== selectedIdRef.current) {
      return
    }

    const messageId = crypto.randomUUID()
    const timestamp = new Date().toISOString()
    const conversationId = selectedId ?? `pending:${messageId}`
    if (sendingConversationIds.current.size > 0) {
      return
    }

    const conversationBeforeSend = selectedConversation
    const session: ConversationSession = {
      conversation: selectedConversation ?? {
        id: conversationId,
        title: normalizeConversationTitle(question).slice(0, 80),
        enabledSourceKeys: [...selectedSourceKeys],
        messages: [],
        createdAtUtc: timestamp,
        updatedAtUtc: timestamp,
      },
      isNew: selectedConversation === null || conversationSessions.current.get(conversationId)?.isNew === true,
      draft: '',
      error: null,
    }
    const pendingMessage: ConversationMessage = { id: messageId, role: 'User', content: question, createdAtUtc: timestamp }
    conversationSessions.current.set(conversationId, session)
    sendingConversationIds.current.add(conversationId)
    shouldScrollToLatestRef.current = true
    selectedIdRef.current = conversationId
    setSelectedId(conversationId)
    setSelectedConversation(session.conversation)
    setConversations((current) => [toSummary(session.conversation), ...current.filter((value) => value.id !== conversationId)])
    setPendingQuestions((current) => new Map(current).set(conversationId, pendingMessage))
    setDraft('')
    setConversationError(null)
    try {
      if (!session.isNew && !await waitForSourceUpdates(conversationId)) {
        throw new Error('The source selection could not be saved. Review your sources and try again.')
      }
      if (!navigator.onLine) {
        throw new Error('You’re offline. Reconnect to send this message.')
      }

      const turn = session.isNew
        ? await conversationClient.createWithMessage(messageId, question, selectedSourceKeys)
        : await conversationClient.appendMessage(conversationId, messageId, question)
      if (dataGeneration.current !== generation) { return }
      if (turn.usage) { updateUsage(turn.usage) }
      else { void loadUsage() }
      const conversation = mergeConversationTurn(conversationBeforeSend, turn)

      const completedSession: ConversationSession = {
        ...session,
        conversation,
        isNew: false,
        error: turn.status === 'answered' ? null : turn.message ?? 'AskRabbi could not create a validated source-grounded answer. Please try again.',
      }
      conversationSessions.current.delete(conversationId)
      conversationSessions.current.set(conversation.id, completedSession)
      setConversations((current) => [toSummary(conversation), ...current.filter((value) => value.id !== conversationId && value.id !== conversation.id)])
      if (selectedIdRef.current === conversationId) {
        selectionRequestId.current += 1
        shouldScrollToLatestRef.current = true
        selectedIdRef.current = conversation.id
        setSelectedId(conversation.id)
        setSelectedConversation(conversation)
        setConversationError(completedSession.error)
        setIsLoadingConversation(false)
      }
    } catch (error) {
      if (dataGeneration.current !== generation) { return }
      if (error instanceof ApiError && error.usage) { updateUsage(error.usage) }
      else { void loadUsage() }
      const failedSession: ConversationSession = {
        ...session,
        draft: session.draft.length === 0 ? question : session.draft,
        error: getErrorMessage(error, 'Your message could not be saved.'),
      }
      const wasNotSaved = session.isNew && error instanceof ApiError && (error.code === 'usage_limit_reached' || error.code === 'chat_in_progress')
      if (wasNotSaved) {
        conversationSessions.current.delete(conversationId)
        setConversations((current) => current.filter((value) => value.id !== conversationId))
      } else {
        conversationSessions.current.set(conversationId, failedSession)
      }
      if (selectedIdRef.current === conversationId) {
        if (wasNotSaved) {
          selectedIdRef.current = null
          setSelectedId(null)
          setSelectedConversation(null)
        }
        setDraft(failedSession.draft)
        setConversationError(failedSession.error)
      }
    } finally {
      sendingConversationIds.current.delete(conversationId)
      setPendingQuestions((current) => {
        const remaining = new Map(current)
        remaining.delete(conversationId)
        return remaining
      })
    }
  }

  function rememberSelectedConversation() {
    if (selectedConversation === null) {
      return
    }

    const session = conversationSessions.current.get(selectedConversation.id)
    if (session !== undefined) {
      session.draft = draft
      session.error = conversationError
    } else {
      conversationSessions.current.set(selectedConversation.id, { conversation: selectedConversation, isNew: false, draft, error: conversationError })
    }
  }

  function handleDraftChange(value: string) {
    setDraft(value)
    const session = selectedId === null ? undefined : conversationSessions.current.get(selectedId)
    if (session !== undefined) {
      session.draft = value
    }
  }

  function handleSelectedSourceKeysChange(sourceKeys: string[]) {
    if (!navigator.onLine || isChatDisabled || isSending) {
      return
    }

    if (selectedId === null || selectedConversation === null) {
      setUnsavedSourceKeys(sourceKeys)
      return
    }

    const conversationId = selectedId
    const session = conversationSessions.current.get(conversationId)
    if (session !== undefined) {
      session.conversation = { ...session.conversation, enabledSourceKeys: sourceKeys }
    }
    setConversationError(null)
    setSelectedConversation((current) => current?.id === conversationId ? { ...current, enabledSourceKeys: sourceKeys } : current)
    setConversations((current) => current.map((conversation) => conversation.id === conversationId ? { ...conversation, enabledSourceKeys: sourceKeys } : conversation))
    if (sourceKeys.length === 0 || session?.isNew === true) {
      return
    }

    const previousUpdate = sourceUpdateQueues.current.get(conversationId) ?? Promise.resolve(true)
    const nextUpdate = previousUpdate
      .then(() => conversationClient.updateSources(conversationId, sourceKeys))
      .then(() => true)
      .catch((error: unknown) => {
        const message = getErrorMessage(error, 'The source selection could not be saved.')
        const currentSession = conversationSessions.current.get(conversationId)
        if (currentSession !== undefined) {
          currentSession.error = message
        }
        if (selectedIdRef.current === conversationId) {
          setConversationError(message)
        }
        return false
      })
    sourceUpdateQueues.current.set(conversationId, nextUpdate)
  }

  async function waitForSourceUpdates(conversationId: string) {
    let pendingUpdate = sourceUpdateQueues.current.get(conversationId)
    if (pendingUpdate === undefined) {
      return true
    }

    let didSave = true
    while (pendingUpdate !== undefined) {
      didSave = await pendingUpdate
      const latestUpdate = sourceUpdateQueues.current.get(conversationId)
      if (latestUpdate === pendingUpdate) {
        return didSave
      }
      pendingUpdate = latestUpdate
    }

    return didSave
  }

  return (
    <div className={`fixed inset-0 flex h-dvh min-h-0 w-full overflow-hidden overscroll-none bg-parchment ${focusedReading.target ? 'focused-reading' : ''}`}>
      {activeView === 'settings' ? <SettingsSidebar section={settingsSection} isMobileOpen={isMobileSidebarOpen} onClose={closeMobileSidebar} onNavigate={navigateSettings} onBack={() => { shouldScrollToLatestRef.current = true; navigateView(settingsReturnView.current === 'settings' ? 'conversation' : settingsReturnView.current); setIsMobileSidebarOpen(false) }} /> :
      <div className="reading-nonessential flex shrink-0">
      <ConversationSidebar
        conversations={conversations}
        selectedId={activeView === 'conversation' ? selectedId : null}
        isMobileOpen={isMobileSidebarOpen}
        isNewConversationDisabled={!isOnline || isLoadingConversations}
        isOffline={!isOnline}
        pendingConversationIds={new Set(pendingQuestions.keys())}
        isDvarTorahSelected={activeView === 'dvarTorah'}
        isCalendarSelected={activeView === 'calendar'}
        user={personalizedUser}
        usage={usage}
        isLoadingUsage={isLoadingUsage}
        usageError={usageError}
        onOpenUsage={() => handleOpenSettings('usage')}
        onCloseMobile={closeMobileSidebar}
        onNewConversation={handleNewConversation}
        onSelectConversation={(id) => void handleSelectConversation(id)}
        onRenameConversation={(id, title) => void handleRenameConversation(id, title)}
        onDeleteConversation={handleDeleteConversation}
        onOpenDvarTorah={handleOpenDvarTorah}
        onOpenCalendar={handleOpenCalendar}
        onOpenSettings={handleOpenSettings}
        onLogout={signOut}
      />
      </div>}

      {isMobileSidebarOpen ? <button type="button" aria-label="Close conversation navigation" onClick={() => setIsMobileSidebarOpen(false)} className="fixed inset-0 z-30 bg-ink/45 lg:hidden" /> : null}

      <main inert={isMobileSidebarOpen} className="relative flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden bg-parchment">
        <header className="reading-nonessential flex h-16 shrink-0 items-center border-b border-line px-4 sm:px-5 lg:hidden">
          <button type="button" onClick={() => { setSourceReaderSelection(null); setIsMobileSidebarOpen(true) }} className="flex size-11 items-center justify-center rounded-lg text-ink transition hover:bg-stone lg:hidden" aria-label={activeView === 'settings' ? 'Open settings navigation' : 'Open conversation navigation'}>
            <Menu aria-hidden="true" className="size-5" strokeWidth={1.75} />
          </button>
          <div className="mx-auto lg:hidden"><Brand compact /></div>
          <div className="size-11 lg:hidden" aria-hidden="true" />
        </header>

        <FocusedReadingToolbar />

        <span className="reading-nonessential pointer-events-none absolute right-4 top-20 hidden size-8 border-r border-t border-brass/60 sm:block lg:top-4" aria-hidden="true" />
        <span className="reading-nonessential pointer-events-none absolute bottom-4 left-4 hidden size-8 border-b border-l border-brass/60 sm:block" aria-hidden="true" />

        {activeView === 'dvarTorah' ? (
          <Suspense fallback={<DvarTorahLoading />}>
            <WeeklyDvarTorahPage client={dvarTorahClient} />
          </Suspense>
        ) : activeView === 'calendar' ? (
          <Suspense fallback={<p role="status" className="p-8 text-muted">Loading calendar…</p>}>
            <CalendarPage client={calendarClient} onOpenDvarTorah={handleOpenDvarTorah} onBackToConversation={handleBackToConversation} onOpenPersonalization={handleOpenPersonalization} />
          </Suspense>
        ) : activeView === 'settings' ? (
          <UnifiedSettingsPage section={settingsSection} targetSetting={targetSetting} navigationKey={settingsNavigationKey} profile={personalizationProfile} client={conversationSettingsClient} onSavePersonalization={handleSavePersonalization} user={personalizedUser} settings={userSettings} usage={usage} usageError={usageError} isLoadingUsage={isLoadingUsage} isDataBusy={pendingQuestions.size > 0 || isLoadingConversations} onDeleteChats={handleDeleteAllChats} onDeleteAccount={deleteAccount} onRetryUsage={() => void loadUsage()} onBack={handleBackToConversation} onSave={handleSaveSettings} onRequestPasswordReset={() => requestPasswordReset(user.email)} />
        ) : (
          <div className="flex min-h-0 flex-1 overflow-hidden overscroll-none">
            <div className="flex min-h-0 min-w-0 flex-1 flex-col">
              <section ref={conversationScrollRef} data-reading-scroll className="flex min-h-0 min-w-0 flex-1 touch-pan-y flex-col overflow-y-auto overscroll-y-contain px-4 pb-4 sm:px-8 sm:pb-6" aria-label="Current conversation">
                <div className="mx-auto flex w-full max-w-[62rem] flex-1 flex-col">
                  {conversationError === null ? null : <p className="mx-auto mt-5 w-full max-w-[46rem] rounded-lg border border-pomegranate/25 bg-pomegranate/5 px-4 py-3 text-sm text-pomegranate" role="alert">{conversationError}</p>}
                  {isLoadingConversations || isLoadingConversation ? (
                    <div className="flex flex-1 items-center justify-center"><p className="text-lg text-muted" role="status">Loading conversation…</p></div>
                  ) : displayedMessages.length === 0 ? (
                    <div className="enter-softly flex flex-1 flex-col items-center justify-center px-2 pb-6 pt-10 text-center sm:pb-10">
                      <h1 className="max-w-[50rem] font-display text-[clamp(2.65rem,5vw,4.15rem)] leading-[1.02] tracking-[-0.045em] text-ink">{conversationStarter.heading}</h1>
                      <p className="mt-6 max-w-[39rem] text-base leading-7 text-ink-soft sm:text-lg">{conversationStarter.supportingText}</p>
                    </div>
                  ) : (
                    <div className="flex-1 py-10 sm:py-14">
                      {displayedMessages.some(message => message.role === 'Assistant' && message.content.trim()) ? <div className="reading-nonessential mx-auto mb-5 flex max-w-[46rem] justify-end"><PrintAction label="Print answers" getRequest={getAnswerPrintRequest} /></div> : null}
                      <article className="reading-column mx-auto max-w-[46rem] space-y-7 sm:space-y-9">
                        {displayedMessages.map((message) => (
                          message.role === 'Assistant'
                            ? <AssistantMessage key={message.id} message={message} autoFocusEligible={message.id === latestDisplayedMessageId} selectedSourceNumber={sourceReaderSelection?.messageId === message.id ? sourceReaderSelection.sourceNumber : null} onSelectSource={handleOpenSourceReader} getPrintRequest={getAnswerPrintRequest} />
                            : <UserMessage key={message.id} message={message} />
                        ))}
                        {isSending && isOnline ? <AnswerProgress sourceDescription={formatSourceSelection(selectedSourceKeys)} /> : null}
                      </article>
                    </div>
                  )}
                </div>
              </section>

              <div className="relative z-10 shrink-0 border-t border-line/60 bg-parchment px-4 pb-2 pt-2 sm:px-8 sm:pb-3" data-chat-composer>
                <ChatUsageNotice usage={usage} error={usageError} isOnline={isOnline} onRetry={() => void loadUsage()} onOpenDvarTorah={handleOpenDvarTorah} />
                <div className="mx-auto flex w-full max-w-[62rem] justify-center">
                  <MessageComposer draft={draft} selectedSourceKeys={selectedSourceKeys} conversationLanguage={personalizationProfile.conversationLanguage} quotationLanguage={personalizationProfile.quotationLanguage} isChatDisabled={isChatDisabled} isSending={pendingQuestions.size > 0 || isLoadingConversation || isLoadingConversations} onDraftChange={handleDraftChange} onSelectedSourceKeysChange={handleSelectedSourceKeysChange} onSubmit={() => void handleSubmit()} />
                </div>
              </div>
            </div>
            {activeSourceReader === null ? null : <SourceReader messageId={activeSourceReader.messageId} sources={activeSourceReader.sources} selectedIndex={activeSourceReader.selectedIndex} showSourceContextByDefault={userSettings.showSourceContextByDefault} onSelectSourceNumber={handleSelectReaderSource} onClose={handleCloseSourceReader} />}
          </div>
        )}
      </main>
    </div>
  )
}

function readSettingHash() {
  const id = window.location.hash.slice(1)
  return SettingsRegistry.some(value => value.id === id && value.section === readSettingsRoute()) ? id : null
}

function DvarTorahLoading() {
  return (
    <section className="flex min-h-0 flex-1 items-center justify-center px-6" aria-label="Weekly Dvar Torah">
      <p className="text-lg text-muted" role="status">Opening this week’s Dvar Torah…</p>
    </section>
  )
}

function resolveActiveSourceReader(messages: readonly ConversationMessage[], selection: SourceReaderSelection | null): ActiveSourceReader | null {
  if (selection === null) {
    return null
  }

  const message = messages.find((value) => value.id === selection.messageId && value.role === 'Assistant')
  if (message === undefined || message.sources === undefined) {
    return null
  }

  const sources = message.sources
  const selectedIndex = sources.findIndex((source) => source.number === selection.sourceNumber)
  return selectedIndex < 0 ? null : { messageId: message.id, sources, selectedIndex }
}

function mergeConversationTurn(current: ConversationDetails | null, turn: ConversationTurn): ConversationDetails {
  if (!('messages' in turn)) {
    return turn.conversation
  }

  const messages = current?.id === turn.conversation.id ? [...current.messages] : []
  for (const message of turn.messages) {
    const existingIndex = messages.findIndex((value) => value.id === message.id)
    if (existingIndex >= 0) {
      messages[existingIndex] = message
    } else {
      messages.push(message)
    }
  }

  return {
    ...turn.conversation,
    messages,
    createdAtUtc: current?.id === turn.conversation.id ? current.createdAtUtc : turn.createdAtUtc,
    updatedAtUtc: turn.conversation.updatedAtUtc ?? current?.updatedAtUtc ?? turn.createdAtUtc,
  }
}

function toSummary(conversation: ConversationDetails): ConversationSummary {
  return {
    id: conversation.id,
    title: conversation.title,
    enabledSourceKeys: conversation.enabledSourceKeys,
    updatedAtUtc: conversation.updatedAtUtc,
  }
}

function reconcileConversationSummary(current: readonly ConversationSummary[], conversation: ConversationDetails): ConversationSummary[] {
  const summary = toSummary(conversation)
  let didFindConversation = false
  const reconciled = current.map((value) => {
    if (value.id !== conversation.id) {
      return value
    }

    didFindConversation = true
    return summary
  })

  return didFindConversation ? reconciled : [summary, ...reconciled]
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
