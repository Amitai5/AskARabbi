import { lazy, Suspense, useCallback, useEffect, useEffectEvent, useLayoutEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/apiClient.ts'
import { ChatUsageNotice } from './ChatUsageNotice.tsx'
import { useMonthlyUsage } from '../settings/useMonthlyUsage.ts'
import { Menu, X } from 'lucide-react'
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
import type { ConversationDetails, ConversationMessage, ConversationSummary, ConversationTeachingContext } from './conversationData.ts'
import { normalizeConversationTitle } from './conversationData.ts'
import { AnswerProgress } from './AnswerProgress.tsx'
import { AssistantMessage } from './AssistantMessage.tsx'
import { ConversationSidebar } from './ConversationSidebar.tsx'
import { advanceConversationStarterIndex, ConversationStarters, getInitialConversationStarterIndex } from './conversationStarters.ts'
import { MessageComposer } from './MessageComposer.tsx'
import { ConversationQuestionNavigation } from './ConversationQuestionNavigation.tsx'
import { MinimumNavigationQuestions } from './questionNavigation.ts'
import { AllSourceKeys, formatSourceSelection } from './sourceOptions.ts'
import { SourceReader } from './SourceReader.tsx'
import { UserMessage } from './UserMessage.tsx'
import { useOnlineStatus } from '../pwa/useOnlineStatus.ts'
import { calendarPath, conversationPath, readPageRoute, teachingPath, writePageUrl, type ActiveView, type TeachingRoute } from './pageRoutes.ts'
import { StarterQuestions } from './StarterQuestions.tsx'
import { appendLearningQuestion } from './learningQuestions.ts'
import { collectPrintAnswers, type PrintRequest } from '../printing/printTypes.ts'
import { TeachingContextCard } from './TeachingContextCard.tsx'

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
  const voiceDraftFor = useRef<string | null | undefined>(undefined)
  const [autoReadAnswerId, setAutoReadAnswerId] = useState<string | null>(null)
  const [draft, setDraft] = useState('')
  const [draftTeachingContext, setDraftTeachingContext] = useState<ConversationTeachingContext | null>(null)
  const activeTeachingContext = selectedConversation === null ? draftTeachingContext : selectedConversation.teachingContext
  const newDraftTeachingContext = useRef<ConversationTeachingContext | null>(null)
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false)
  const closeMobileSidebar = useCallback(() => setIsMobileSidebarOpen(false), [])
  const [activeView, setActiveView] = useState<ActiveView>(() => readPageRoute().view)
  const [restoredRoute, setRestoredRoute] = useState(readPageRoute)
  const [restoreKey, setRestoreKey] = useState(0)
  const [settingsSection, setSettingsSection] = useState<SettingsSectionId>(() => readSettingsRoute() ?? 'account')
  const [targetSetting, setTargetSetting] = useState<string | null>(readSettingHash)
  const [settingsNavigationKey, setSettingsNavigationKey] = useState(0)
  const settingsReturnPath = useRef('/conversations/new')
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
  const [unreadConversationIds, setUnreadConversationIds] = useState<ReadonlySet<string>>(() => new Set())
  const [readyNotice, setReadyNotice] = useState<ConversationSummary | null>(null)
  const [draftNotice, setDraftNotice] = useState<string | null>(null)
  const [composerFocusKey, setComposerFocusKey] = useState(0)
  const newDraft = useRef('')
  const [conversationError, setConversationError] = useState<string | null>(null)
  const [sourceReaderSelection, setSourceReaderSelection] = useState<SourceReaderSelection | null>(null)
  const selectionRequestId = useRef(0)
  const dataGeneration = useRef(0)
  const selectedIdRef = useRef<string | null>(null)
  // Keep in-flight and completed turns in the dashboard, not the currently visible page.
  const conversationSessions = useRef(new Map<string, ConversationSession>())
  const completedPendingIds = useRef(new Map<string, string>())
  const sendingConversationIds = useRef(new Set<string>())
  const sourceUpdateQueues = useRef(new Map<string, Promise<boolean>>())
  const sourceReaderTriggerRef = useRef<HTMLButtonElement | null>(null)
  const conversationScrollRef = useRef<HTMLElement | null>(null)
  const shouldScrollToLatestRef = useRef(true)
  const isBrowsingEarlierQuestionRef = useRef(false)

  useEffect(() => {
    const desktop = window.matchMedia?.('(min-width: 64rem)')
    const closeDrawerOnDesktop = () => { if (desktop?.matches) { setIsMobileSidebarOpen(false) } }
    desktop?.addEventListener('change', closeDrawerOnDesktop)
    return () => desktop?.removeEventListener('change', closeDrawerOnDesktop)
  }, [])

  const restorePage = useEffectEvent(restoreLocation)

  useEffect(() => {
    const section = readSettingsRoute()
    if (section) { window.history.replaceState(window.history.state, '', `/settings/${section}${window.location.hash}`) }
    const route = readPageRoute()
    if (route.view === 'conversation' && route.isNew) { writePageUrl('/conversations/new', true) }
    function navigateHistory() { restorePage() }
    window.addEventListener('popstate', navigateHistory)
    return () => window.removeEventListener('popstate', navigateHistory)
  }, [exitFocusedReading])

  function restoreLocation() {
    setAutoReadAnswerId(null)
    exitFocusedReading()
    const route = readPageRoute()
    setRestoredRoute(route)
    setRestoreKey(key => key + 1)
    setSourceReaderSelection(null)
    if (route.view === 'settings') {
      setSettingsSection(readSettingsRoute() ?? 'account')
      setTargetSetting(readSettingHash())
      setSettingsNavigationKey(key => key + 1)
    } else if (route.view === 'conversation') {
      const pendingId: unknown = route.isNew ? window.history.state?.askarabbiPendingId : null
      const restoredId = route.conversationId ?? (typeof pendingId === 'string' ? completedPendingIds.current.get(pendingId) ?? (conversationSessions.current.has(pendingId) ? pendingId : undefined) : undefined)
      if (restoredId) {
        if (restoredId !== selectedIdRef.current) { void handleSelectConversation(restoredId, false) }
        if (!restoredId.startsWith('pending:')) { writePageUrl(conversationPath(restoredId), true) }
      } else if (route.isNew && selectedIdRef.current !== null) {
        rememberSelectedConversation()
        selectionRequestId.current += 1
        selectedIdRef.current = null
        setSelectedId(null)
        setSelectedConversation(null)
        setDraft(newDraft.current)
        setDraftTeachingContext(newDraftTeachingContext.current)
        setConversationError(null)
        setIsLoadingConversation(false)
      }
      shouldScrollToLatestRef.current = true
      isBrowsingEarlierQuestionRef.current = false
    }
    setActiveView(route.view)
    setIsMobileSidebarOpen(false)
  }

  function navigateView(view: Exclude<ActiveView, 'settings'>, replaceUrl = false) {
    setAutoReadAnswerId(null)
    const path = view === 'conversation' ? conversationPath(selectedIdRef.current) : view === 'calendar' ? '/calendar' : '/teachings'
    const isNewDestination = `${window.location.pathname}${window.location.search}` !== path
    writePageUrl(path, replaceUrl)
    if (view === 'conversation' && selectedIdRef.current?.startsWith('pending:')) { window.history.replaceState({ ...window.history.state, askarabbiPendingId: selectedIdRef.current }, '', path) }
    if (view !== activeView || isNewDestination) { setRestoredRoute(readPageRoute()); setRestoreKey(key => key + 1) }
    focusedReading.exit()
    setActiveView(view)
  }

  function navigateSettings(section: SettingsSectionId, settingId?: string, keepNavigationOpen = false) {
    setAutoReadAnswerId(null)
    if (activeView !== 'settings') { settingsReturnPath.current = `${window.location.pathname}${window.location.search}` }
    focusedReading.exit()
    window.history.pushState(window.history.state, '', `/settings/${section}${settingId ? `#${settingId}` : ''}`)
    setSettingsSection(section)
    setTargetSetting(settingId ?? null)
    setSettingsNavigationKey(key => key + 1)
    setActiveView('settings')
    // Recheck the current allowance even when this tab still has a cached value.
    if (section === 'account') { void loadUsage() }
    if (!keepNavigationOpen) { setIsMobileSidebarOpen(false) }
    setSourceReaderSelection(null)
  }

  function returnFromSettings() {
    if (settingsReturnPath.current.startsWith('/conversations/')) {
      // A temporary conversation receives its real ID while settings remain open.
      shouldScrollToLatestRef.current = true
      isBrowsingEarlierQuestionRef.current = false
      navigateView('conversation')
      setIsMobileSidebarOpen(false)
      return
    }
    writePageUrl(settingsReturnPath.current)
    restoreLocation()
  }

  useEffect(() => {
    if (!readyNotice) { return }
    const timer = window.setTimeout(() => setReadyNotice(null), 10_000)
    return () => window.clearTimeout(timer)
  }, [readyNotice])

  useEffect(() => {
    function markVisibleAnswerRead() {
      if (activeView !== 'conversation' || !selectedConversation || isLoadingConversation || document.visibilityState === 'hidden') { return }
      const id = selectedConversation.id
      setUnreadConversationIds(current => {
        if (!current.has(id)) { return current }
        const next = new Set(current); next.delete(id); return next
      })
      setReadyNotice(current => current?.id === id ? null : current)
    }
    markVisibleAnswerRead()
    document.addEventListener('visibilitychange', markVisibleAnswerRead)
    return () => document.removeEventListener('visibilitychange', markVisibleAnswerRead)
  }, [activeView, selectedConversation, isLoadingConversation])

  const clearChatState = useCallback(() => {
    dataGeneration.current += 1
    selectionRequestId.current += 1
    selectedIdRef.current = null
    conversationSessions.current.clear()
    completedPendingIds.current.clear()
    sourceUpdateQueues.current.clear()
    sendingConversationIds.current.clear()
    setConversations([])
    setSelectedId(null)
    setSelectedConversation(null)
    setPendingQuestions(new Map())
    setUnreadConversationIds(new Set())
    setReadyNotice(null)
    newDraft.current = ''
    newDraftTeachingContext.current = null
    setDraftTeachingContext(null)
    if (readPageRoute().view === 'conversation') { writePageUrl('/conversations/new', true) }
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
        if (selectionRequestId.current !== requestId) { return }
        const route = readPageRoute()
        if (route.view !== 'conversation' || route.isNew) { return }
        const id = route.conversationId
        if (!id) { return }
        shouldScrollToLatestRef.current = true
        isBrowsingEarlierQuestionRef.current = false
        selectedIdRef.current = id
        setSelectedId(id)
        const details = await conversationClient.get(id)
        if (isCurrent && selectionRequestId.current === requestId) {
          setSelectedConversation(details)
          setConversations((current) => reconcileConversationSummary(current, details))
          if (readPageRoute().view === 'conversation') { writePageUrl(conversationPath(id), true) }
        }
      })
      .catch((error: unknown) => {
        if (isCurrent && selectionRequestId.current === requestId) {
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
  const showQuestionNavigation = !isLoadingConversation && !isLoadingConversations && !focusedReading.target && activeSourceReader === null && displayedMessages.filter(message => message.role === 'User').length >= MinimumNavigationQuestions
  const getAnswerPrintRequest = useCallback((initialAnswerId?: string): PrintRequest => ({ kind: 'answers', title: normalizeConversationTitle(selectedConversation?.title), answers: collectPrintAnswers(selectedConversation?.messages ?? []), initialAnswerId }), [selectedConversation])
  const getConversationPrintRequest = useCallback(async (id: string): Promise<PrintRequest> => {
    const conversation = (selectedConversation?.id === id ? selectedConversation : conversationSessions.current.get(id)?.conversation) ?? await conversationClient.get(id)
    return { kind: 'answers', title: normalizeConversationTitle(conversation.title), answers: collectPrintAnswers(conversation.messages) }
  }, [conversationClient, selectedConversation])

  useLayoutEffect(() => {
    if (!shouldScrollToLatestRef.current || focusedReading.target !== null || activeView !== 'conversation' || isLoadingConversations || isLoadingConversation) {
      return
    }

    const scrollContainer = conversationScrollRef.current
    if (scrollContainer === null) {
      return
    }

    shouldScrollToLatestRef.current = false
    const scrollToLatest = () => {
      if (!isBrowsingEarlierQuestionRef.current) { scrollContainer.scrollTo({ top: latestDisplayedMessageId === null ? 0 : scrollContainer.scrollHeight, behavior: 'auto' }) }
    }
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
    voiceDraftFor.current = undefined
    if (!navigator.onLine || isLoadingConversations) {
      return
    }

    openNewConversation('')
  }

  function openNewConversation(initialDraft: string, replaceUrl = false, teachingContext: ConversationTeachingContext | null = null) {
    setAutoReadAnswerId(null)
    rememberSelectedConversation()
    selectionRequestId.current += 1
    shouldScrollToLatestRef.current = true
    isBrowsingEarlierQuestionRef.current = false
    setConversationError(null)
    selectedIdRef.current = null
    setSelectedId(null)
    setSelectedConversation(null)
    setIsLoadingConversation(false)
    setSourceReaderSelection(null)
    setUnsavedSourceKeys([...AllSourceKeys])
    setDraft(initialDraft)
    newDraft.current = initialDraft
    newDraftTeachingContext.current = teachingContext
    setDraftTeachingContext(teachingContext)
    setDraftNotice(null)
    setIsMobileSidebarOpen(false)
    navigateView('conversation', replaceUrl)
    setConversationStarterIndex((current) => advanceConversationStarterIndex(current))
  }

  async function handleSelectConversation(id: string, updateUrl = true) {
    if (!navigator.onLine) { return }
    setAutoReadAnswerId(null)
    rememberSelectedConversation()
    const requestId = selectionRequestId.current + 1
    selectionRequestId.current = requestId
    shouldScrollToLatestRef.current = true
    isBrowsingEarlierQuestionRef.current = false
    selectedIdRef.current = id
    setSelectedId(id)
    setSelectedConversation(null)
    setSourceReaderSelection(null)
    setConversationError(null)
    setIsLoadingConversation(true)
    setDraft('')
    setIsMobileSidebarOpen(false)
    if (updateUrl) { navigateView('conversation') }
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
  }

  function handleOpenCalendar() {
    setIsMobileSidebarOpen(false)
    setSourceReaderSelection(null)
    navigateView('calendar')
  }

  function handleBackToConversation() {
    shouldScrollToLatestRef.current = true
    isBrowsingEarlierQuestionRef.current = false
    navigateView('conversation')
  }

  function handleQuestionNavigation(isLatest: boolean) {
    isBrowsingEarlierQuestionRef.current = !isLatest
    shouldScrollToLatestRef.current = false
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
      openNewConversation('', readPageRoute().conversationId === id)
      setIsLoadingConversations(false)
      // Opening the new page first preserves other drafts without re-caching the deleted chat.
      conversationSessions.current.delete(id)
      for (const [pendingId, completedId] of completedPendingIds.current) {
        if (completedId === id) { completedPendingIds.current.delete(pendingId) }
      }
      setUnreadConversationIds(current => { const next = new Set(current); next.delete(id); return next })
      setReadyNotice(current => current?.id === id ? null : current)
      sourceUpdateQueues.current.delete(id)
      setConversations((current) => current.filter((conversation) => conversation.id !== id))
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

    const readSpokenAnswer = voiceDraftFor.current === selectedId
    voiceDraftFor.current = undefined
    setAutoReadAnswerId(null)
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
        teachingContext: draftTeachingContext,
        createdAtUtc: timestamp,
        updatedAtUtc: timestamp,
      },
      isNew: selectedConversation === null || conversationSessions.current.get(conversationId)?.isNew === true,
      draft: '',
      error: null,
    }
    const pendingMessage: ConversationMessage = { id: messageId, role: 'User', content: question, createdAtUtc: timestamp }
    conversationSessions.current.set(conversationId, session)
    if (session.isNew) { window.history.replaceState({ ...window.history.state, askarabbiPendingId: conversationId }, '', window.location.href) }
    sendingConversationIds.current.add(conversationId)
    shouldScrollToLatestRef.current = true
    isBrowsingEarlierQuestionRef.current = false
    selectedIdRef.current = conversationId
    setSelectedId(conversationId)
    setSelectedConversation(session.conversation)
    setConversations((current) => [toSummary(session.conversation), ...current.filter((value) => value.id !== conversationId)])
    setPendingQuestions((current) => new Map(current).set(conversationId, pendingMessage))
    setDraft('')
    newDraft.current = ''
    newDraftTeachingContext.current = null
    setDraftTeachingContext(null)
    setDraftNotice(null)
    setConversationError(null)
    try {
      if (!session.isNew && !await waitForSourceUpdates(conversationId)) {
        throw new Error('The source selection could not be saved. Review your sources and try again.')
      }
      if (!navigator.onLine) {
        throw new Error('You’re offline. Reconnect to send this message.')
      }

      const teaching = session.conversation.teachingContext
      const turn = session.isNew
        ? await (teaching ? conversationClient.createWithMessage(messageId, question, selectedSourceKeys, { weekKey: teaching.weekKey, selectedText: teaching.selectedText }) : conversationClient.createWithMessage(messageId, question, selectedSourceKeys))
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
      if (conversationId !== conversation.id) { completedPendingIds.current.set(conversationId, conversation.id) }
      setConversations((current) => [toSummary(conversation), ...current.filter((value) => value.id !== conversationId && value.id !== conversation.id)])
      const isAnswerVisible = readPageRoute().view === 'conversation' && selectedIdRef.current === conversationId && document.visibilityState !== 'hidden'
      if (turn.status === 'answered' && isAnswerVisible && readSpokenAnswer) {
        setAutoReadAnswerId(conversation.messages.filter(message => message.role === 'Assistant').at(-1)?.id ?? null)
      }
      if (turn.status === 'answered' && !isAnswerVisible) {
        setUnreadConversationIds(current => new Set(current).add(conversation.id))
        setReadyNotice(toSummary(conversation))
      }
      if (selectedIdRef.current === conversationId) {
        selectionRequestId.current += 1
        shouldScrollToLatestRef.current = !isBrowsingEarlierQuestionRef.current
        selectedIdRef.current = conversation.id
        setSelectedId(conversation.id)
        setSelectedConversation(conversation)
        setConversationError(completedSession.error)
        setIsLoadingConversation(false)
        if (readPageRoute().view === 'conversation') { writePageUrl(conversationPath(conversation.id), true) }
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
      const rejectedTeaching = session.conversation.teachingContext && error instanceof ApiError && (error.status === 400 || error.status === 404)
      const wasNotSaved = session.isNew && error instanceof ApiError && (rejectedTeaching || error.code === 'usage_limit_reached' || error.code === 'chat_in_progress')
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
          setDraftTeachingContext(session.conversation.teachingContext ?? null)
          newDraftTeachingContext.current = session.conversation.teachingContext ?? null
          newDraft.current = failedSession.draft
          if (readPageRoute().view === 'conversation') { writePageUrl(conversationPath(null), true) }
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
      newDraft.current = draft
      newDraftTeachingContext.current = draftTeachingContext
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
    if (!value.trim()) { voiceDraftFor.current = undefined }
    setDraft(value)
    setDraftNotice(null)
    if (selectedId === null) { newDraft.current = value }
    const session = selectedId === null ? undefined : conversationSessions.current.get(selectedId)
    if (session !== undefined) {
      session.draft = value
    }
  }

  function prepareQuestion(question: string) {
    if (!navigator.onLine || isChatDisabled || isLoadingConversation || isLoadingConversations) { return }
    const next = appendLearningQuestion(draft, question)
    navigateView('conversation')
    setSourceReaderSelection(null)
    setIsMobileSidebarOpen(false)
    if (next.length > 4000) {
      setDraftNotice('Your draft is too long to add this question. Shorten it first; your text has been kept.')
    } else {
      handleDraftChange(next)
      setDraftNotice(draft.trim() ? 'Question added below your draft. Review it before sending.' : 'Question prepared. Edit it or send when you’re ready.')
    }
    setComposerFocusKey(key => key + 1)
  }

  function prepareNewQuestion(question: string) {
    if (!navigator.onLine || isChatDisabled || isLoadingConversation || isLoadingConversations) { return }
    openNewConversation(question)
    setDraftNotice('Question prepared in a new conversation. Edit it or send when you’re ready.')
    setComposerFocusKey(key => key + 1)
  }

  function prepareTeachingQuestion(context: ConversationTeachingContext) {
    if (!navigator.onLine || isChatDisabled || isLoadingConversation || isLoadingConversations) { return }
    openNewConversation(context.selectedText ? 'Can you explain this passage in the context of the whole teaching?' : 'Can we explore the main idea of this teaching?', false, context)
    setDraftNotice('Teaching attached to a new conversation. Edit the question, then send when you’re ready.')
    setComposerFocusKey(key => key + 1)
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
      {activeView === 'settings' ? <SettingsSidebar section={settingsSection} isMobileOpen={isMobileSidebarOpen} onClose={closeMobileSidebar} onNavigate={navigateSettings} onBack={returnFromSettings} /> :
      <div className="reading-nonessential flex shrink-0">
      <ConversationSidebar
        getPrintRequest={getConversationPrintRequest}
        conversations={conversations}
        selectedId={activeView === 'conversation' ? selectedId : null}
        isMobileOpen={isMobileSidebarOpen}
        isNewConversationDisabled={!isOnline || isLoadingConversations}
        isOffline={!isOnline}
        pendingConversationIds={new Set(pendingQuestions.keys())}
        unreadConversationIds={unreadConversationIds}
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

        {readyNotice ? <div className="absolute inset-x-3 top-3 z-30 mx-auto flex w-fit max-w-[calc(100%-1.5rem)] items-center gap-3 rounded-xl border border-line-strong bg-paper px-4 py-2 shadow-menu lg:top-4" role="status" aria-live="polite">
          <span className="size-2 shrink-0 rounded-full bg-pomegranate" aria-hidden="true" />
          <button type="button" onClick={() => void handleSelectConversation(readyNotice.id)} className="min-h-10 min-w-0 text-left text-sm text-ink"><strong>Answer ready</strong><span className="block max-w-64 truncate text-xs text-muted">{readyNotice.title}</span><span className="sr-only"> — View answer</span></button>
          <button type="button" aria-label="Dismiss answer ready notification" onClick={() => setReadyNotice(null)} className="flex size-10 shrink-0 items-center justify-center rounded-lg text-muted hover:bg-stone"><X className="size-4" aria-hidden="true" /></button>
        </div> : null}

        <span className="reading-nonessential pointer-events-none absolute right-4 top-20 hidden size-8 border-r border-t border-brass/60 sm:block lg:top-4" aria-hidden="true" />
        <span className="reading-nonessential pointer-events-none absolute bottom-4 left-4 hidden size-8 border-b border-l border-brass/60 sm:block" aria-hidden="true" />

        {activeView === 'dvarTorah' ? (
          <Suspense fallback={<DvarTorahLoading />}>
            <WeeklyDvarTorahPage key={restoreKey} client={dvarTorahClient} initialRoute={restoredRoute.teaching} onNavigate={(route: TeachingRoute) => writePageUrl(teachingPath(route))} onAsk={prepareNewQuestion} onAskTeaching={prepareTeachingQuestion} isAskDisabled={isChatDisabled || isLoadingConversations || isLoadingConversation} />
          </Suspense>
        ) : activeView === 'calendar' ? (
          <Suspense fallback={<p role="status" className="p-8 text-muted">Loading calendar…</p>}>
            <CalendarPage key={restoreKey} client={calendarClient} initialDays={restoredRoute.days} initialSearch={restoredRoute.search} onNavigate={(days, search) => writePageUrl(calendarPath(days, search), true)} onAsk={prepareNewQuestion} isAskDisabled={isChatDisabled || isLoadingConversations || isLoadingConversation} onOpenDvarTorah={handleOpenDvarTorah} onBackToConversation={handleBackToConversation} onOpenPersonalization={handleOpenPersonalization} />
          </Suspense>
        ) : activeView === 'settings' ? (
          <UnifiedSettingsPage section={settingsSection} targetSetting={targetSetting} navigationKey={settingsNavigationKey} profile={personalizationProfile} client={conversationSettingsClient} onSavePersonalization={handleSavePersonalization} user={personalizedUser} settings={userSettings} usage={usage} usageError={usageError} isLoadingUsage={isLoadingUsage} isDataBusy={pendingQuestions.size > 0 || isLoadingConversations} onDeleteChats={handleDeleteAllChats} onDeleteAccount={deleteAccount} onRetryUsage={() => void loadUsage()} onBack={handleBackToConversation} onSave={handleSaveSettings} onRequestPasswordReset={() => requestPasswordReset(user.email)} />
        ) : (
          <div className="flex min-h-0 flex-1 overflow-hidden overscroll-none">
            <div className="flex min-h-0 min-w-0 flex-1 flex-col">
              <div className="relative flex min-h-0 flex-1">
              <section ref={conversationScrollRef} data-reading-scroll className={`flex min-h-0 min-w-0 flex-1 touch-pan-y flex-col overflow-y-auto overscroll-y-contain px-4 pb-4 sm:px-8 sm:pb-6 ${showQuestionNavigation ? 'md:pr-16' : ''}`} aria-label="Current conversation">
                <div className="mx-auto flex w-full max-w-[62rem] flex-1 flex-col">
                  {conversationError === null ? null : <p className="mx-auto mt-5 w-full max-w-[46rem] rounded-lg border border-pomegranate/25 bg-pomegranate/5 px-4 py-3 text-sm text-pomegranate" role="alert">{conversationError}</p>}
                  {isLoadingConversations || isLoadingConversation ? (
                    <div className="flex flex-1 items-center justify-center"><p className="text-lg text-muted" role="status">Loading conversation…</p></div>
                  ) : displayedMessages.length === 0 ? (
                    <div className="enter-softly flex flex-1 flex-col items-center justify-center px-2 pb-6 pt-10 text-center sm:pb-10">
                      <h1 className="max-w-[50rem] font-display text-[clamp(2.65rem,5vw,4.15rem)] leading-[1.02] tracking-[-0.045em] text-ink">{draftTeachingContext ? 'Let’s explore this teaching.' : conversationStarter.heading}</h1>
                      <p className="mt-6 max-w-[39rem] text-base leading-7 text-ink-soft sm:text-lg">{draftTeachingContext ? 'Ask about a passage, its sources, or what the teaching means for you.' : conversationStarter.supportingText}</p>
                      {draftTeachingContext ? null : <StarterQuestions disabled={isChatDisabled} onChoose={prepareQuestion} />}
                    </div>
                  ) : (
                    <div className="flex-1 py-10 sm:py-14">
                      <article className="reading-column mx-auto max-w-[46rem] space-y-7 sm:space-y-9">
                        {displayedMessages.map((message) => (
                          message.role === 'Assistant'
                            ? <AssistantMessage key={message.id} message={message} conversationId={selectedConversation?.id} autoPlay={message.id === autoReadAnswerId} autoFocusEligible={message.id === latestDisplayedMessageId} selectedSourceNumber={sourceReaderSelection?.messageId === message.id ? sourceReaderSelection.sourceNumber : null} onSelectSource={handleOpenSourceReader} getPrintRequest={getAnswerPrintRequest} />
                            : <UserMessage key={message.id} message={message} />
                        ))}
                        {isSending && isOnline ? <AnswerProgress sourceDescription={formatSourceSelection(selectedSourceKeys)} /> : null}
                      </article>
                    </div>
                  )}
                </div>
              </section>
              {showQuestionNavigation ? <ConversationQuestionNavigation key={selectedId} messages={displayedMessages} scrollRef={conversationScrollRef} onNavigate={handleQuestionNavigation} /> : null}
              </div>

              <div className="relative z-10 shrink-0 border-t border-line/60 bg-parchment px-4 pb-2 pt-2 sm:px-8 sm:pb-3" data-chat-composer>
                <ChatUsageNotice usage={usage} error={usageError} isOnline={isOnline} onRetry={() => void loadUsage()} onOpenDvarTorah={handleOpenDvarTorah} />
                {draftNotice ? <p role="status" className="mx-auto mb-2 max-w-[50rem] text-sm text-ink-soft">{draftNotice}</p> : null}
                {activeTeachingContext ? <TeachingContextCard context={activeTeachingContext} onRemove={selectedConversation === null ? () => { setDraftTeachingContext(null); newDraftTeachingContext.current = null } : undefined} /> : null}
                <div className="mx-auto flex w-full max-w-[62rem] justify-center">
                  <MessageComposer voiceScope={`${selectedId ?? 'new'}:${conversationStarterIndex}`} onVoiceDraft={() => { voiceDraftFor.current = selectedId }} focusKey={composerFocusKey} draft={draft} selectedSourceKeys={selectedSourceKeys} conversationLanguage={personalizationProfile.conversationLanguage} quotationLanguage={personalizationProfile.quotationLanguage} enterSendsMessage={userSettings.enterSendsMessage} isChatDisabled={isChatDisabled} isSending={pendingQuestions.size > 0 || isLoadingConversation || isLoadingConversations} onDraftChange={handleDraftChange} onSelectedSourceKeysChange={handleSelectedSourceKeysChange} onSubmit={() => void handleSubmit()} />
                </div>
              </div>
            </div>
            {activeSourceReader === null ? null : <SourceReader messageId={activeSourceReader.messageId} sources={activeSourceReader.sources} selectedIndex={activeSourceReader.selectedIndex} showSourceContextByDefault={userSettings.showSourceContextByDefault} onSelectSourceNumber={handleSelectReaderSource} onClose={handleCloseSourceReader} onAsk={prepareQuestion} isAskDisabled={isChatDisabled} />}
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
    teachingContext: turn.teachingContext ?? (current?.id === turn.conversation.id ? current.teachingContext : null),
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
