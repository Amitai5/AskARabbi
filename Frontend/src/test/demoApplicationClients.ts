import type { AuthClient, AuthenticatedUser } from '../features/auth/authTypes.ts'
import type { ConversationClient } from '../features/conversations/conversationClient.ts'
import { InitialConversations, type ConversationDetails, type ConversationSource, type ConversationSummary } from '../features/conversations/conversationData.ts'
import type { ConversationSettingsClient } from '../features/personalization/conversationSettingsClient.ts'
import type { PersonalizationProfile } from '../features/personalization/personalizationTypes.ts'
import { createDefaultUserSettings, type UserSettings } from '../features/settings/settingsTypes.ts'

const FixedTimestamp = '2026-08-25T12:30:00Z'
const DemoSources: ConversationSource[] = [
  {
    number: 1,
    title: 'Mishnah Chullin',
    hebrewTitle: 'משנה חולין',
    canonicalReference: 'Mishnah Chullin 8:1',
    edition: 'Mishnah Yomit by Dr. Joshua Kulp',
    language: 'English',
    collection: 'Mishnah',
    license: 'CC-BY',
    sourceUrl: 'https://www.sefaria.org/Mishnah_Chullin.8.1',
    attributionUrl: 'https://example.test/mishnah-yomit-attribution',
    quotations: ['Fowl may be placed upon the table together with cheese but may not be eaten with it, the words of Bet Shammai. Bet Hillel say: it may neither be placed upon the table together with cheese nor eaten with it.'],
    context: 'Every kind of flesh is forbidden to be cooked in milk, except for the flesh of fish and of locusts. Fowl may be placed upon the table together with cheese but may not be eaten with it, the words of Bet Shammai. Bet Hillel say: it may neither be placed upon the table together with cheese nor eaten with it.',
    isExcerpt: false,
  },
  {
    number: 2,
    title: 'Jerusalem Talmud Terumot',
    hebrewTitle: 'תלמוד ירושלמי תרומות',
    canonicalReference: 'Jerusalem Talmud Terumot 1:5:4',
    edition: 'The Jerusalem Talmud, translation and commentary by Heinrich W. Guggenheimer',
    language: 'English',
    collection: 'Talmud',
    license: 'CC-BY',
    sourceUrl: 'https://www.sefaria.org/Jerusalem_Talmud_Terumot.1.5.4',
    attributionUrl: 'https://example.test/guggenheimer-attribution',
    quotations: ['Everybody agrees that fowl meat is not meat in the biblical sense since birds have no milk. Nevertheless, as a rabbinic fence, one may not eat birds’ meat with any milk product.'],
    context: 'The passage records the dispute between the Houses of Shammai and Hillel and explains that poultry is outside the biblical category while remaining prohibited as a rabbinic safeguard.',
    isExcerpt: true,
  },
]

const DemoUser: AuthenticatedUser = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Amitai Erfanian',
  email: 'amitai@example.com',
  initials: 'AE',
  isEmailVerified: true,
}

const DemoProfile: PersonalizationProfile = {
  fullName: DemoUser.name,
  birthDateTime: '2001-12-17T09:30',
  birthTimeZone: 'America/Los_Angeles',
  conversationLanguage: 'English',
  quotationLanguage: 'English',
  religiousMovement: 'Conservadox',
  jewishHeritage: 'Mizrahi',
  additionalContext: 'My family is Iranian Jewish.',
}

export interface DemoApplicationClients {
  authClient: AuthClient
  conversationClient: ConversationClient
  conversationSettingsClient: ConversationSettingsClient
}

export function createDemoApplicationClients(): DemoApplicationClients {
  let profile: PersonalizationProfile | null = { ...DemoProfile }
  let settings: UserSettings = createDefaultUserSettings()
  const conversations = new Map<string, ConversationDetails>(InitialConversations.map((summary) => [summary.id, {
    ...summary,
    enabledSourceKeys: [...summary.enabledSourceKeys],
    messages: [],
    createdAtUtc: FixedTimestamp,
    updatedAtUtc: FixedTimestamp,
  }]))

  const authClient: AuthClient = {
    getSession: () => Promise.resolve(null),
    signInWithEmail: (email) => Promise.resolve({ ...DemoUser, email }),
    signInWithSocialProvider: () => Promise.resolve(DemoUser),
    signUp: () => {
      profile = null
      return Promise.resolve(DemoUser)
    },
    requestPasswordReset: () => Promise.resolve(),
    confirmPasswordReset: () => Promise.resolve(),
    signOut: () => Promise.resolve(),
  }

  const conversationClient: ConversationClient = {
    list: () => Promise.resolve([...conversations.values()].map(toSummary)),
    createWithMessage: (messageId, content, enabledSourceKeys) => {
      const conversation: ConversationDetails = {
        id: crypto.randomUUID(),
        title: createDemoConversationTitle(content),
        enabledSourceKeys: [...enabledSourceKeys],
        messages: [
          { id: messageId, role: 'User', content, createdAtUtc: FixedTimestamp },
          { id: crypto.randomUUID(), role: 'Assistant', content: 'The short answer is that this local demo represents a validated grounded response. [1] A second source preserves the surrounding discussion. [2]', sources: cloneSources(DemoSources), createdAtUtc: FixedTimestamp },
        ],
        createdAtUtc: FixedTimestamp,
        updatedAtUtc: FixedTimestamp,
      }
      conversations.set(conversation.id, conversation)
      return Promise.resolve({ status: 'answered', conversation: cloneConversation(conversation), message: null })
    },
    get: (conversationId) => Promise.resolve(cloneConversation(getConversation(conversations, conversationId))),
    appendMessage: (conversationId, messageId, content) => {
      const current = getConversation(conversations, conversationId)
      const conversation: ConversationDetails = {
        ...current,
        messages: [
          ...current.messages,
          { id: messageId, role: 'User', content, createdAtUtc: FixedTimestamp },
          { id: crypto.randomUUID(), role: 'Assistant', content: 'This local demo follow-up remains grounded in the cited sources. [1] [2]', sources: cloneSources(DemoSources), createdAtUtc: FixedTimestamp },
        ],
      }
      conversations.set(conversationId, conversation)
      return Promise.resolve({ status: 'answered', conversation: cloneConversation(conversation), message: null })
    },
    rename: (conversationId, title) => {
      const current = getConversation(conversations, conversationId)
      conversations.set(conversationId, { ...current, title })
      return Promise.resolve()
    },
    updateSources: (conversationId, enabledSourceKeys) => {
      const current = getConversation(conversations, conversationId)
      conversations.set(conversationId, { ...current, enabledSourceKeys: [...enabledSourceKeys] })
      return Promise.resolve()
    },
    delete: (conversationId) => {
      conversations.delete(conversationId)
      return Promise.resolve()
    },
  }

  const conversationSettingsClient: ConversationSettingsClient = {
    getPersonalization: () => Promise.resolve({ isConfigured: profile !== null, personalization: profile === null ? null : { ...profile } }),
    updatePersonalization: (value) => {
      profile = { ...value }
      return Promise.resolve({ ...profile })
    },
    getPreferences: () => Promise.resolve({ ...settings }),
    updatePreferences: (value) => {
      settings = { ...value }
      return Promise.resolve({ ...settings })
    },
    getUsage: () => Promise.resolve({
      periodStartUtc: '2026-08-01T00:00:00Z',
      periodEndUtc: '2026-09-01T00:00:00Z',
      answersUsed: 0,
      answerLimit: 50,
      answersRemaining: 50,
    }),
  }

  return { authClient, conversationClient, conversationSettingsClient }
}

function createDemoConversationTitle(content: string) {
  const normalized = content.trim().replace(/[?!.]+$/, '')
  return normalized.length <= 60 ? normalized : `${normalized.slice(0, 59).trimEnd()}…`
}

function getConversation(conversations: Map<string, ConversationDetails>, conversationId: string) {
  const conversation = conversations.get(conversationId)
  if (conversation === undefined) {
    throw new Error('Conversation not found.')
  }
  return conversation
}

function cloneConversation(conversation: ConversationDetails): ConversationDetails {
  return {
    ...conversation,
    enabledSourceKeys: [...conversation.enabledSourceKeys],
    messages: conversation.messages.map((message) => ({ ...message, sources: message.sources === undefined ? undefined : cloneSources(message.sources) })),
  }
}

function cloneSources(sources: readonly ConversationSource[]): ConversationSource[] {
  return sources.map((source) => ({ ...source, quotations: [...source.quotations] }))
}

function toSummary(conversation: ConversationDetails): ConversationSummary {
  return {
    id: conversation.id,
    title: conversation.title,
    enabledSourceKeys: [...conversation.enabledSourceKeys],
    updatedAtUtc: conversation.updatedAtUtc,
  }
}
