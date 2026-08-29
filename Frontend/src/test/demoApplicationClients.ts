import type { AuthClient, AuthenticatedUser } from '../features/auth/authTypes.ts'
import type { ConversationClient } from '../features/conversations/conversationClient.ts'
import { InitialConversations, type ConversationDetails, type ConversationSummary } from '../features/conversations/conversationData.ts'
import type { ConversationSettingsClient } from '../features/personalization/conversationSettingsClient.ts'
import type { PersonalizationProfile } from '../features/personalization/personalizationTypes.ts'
import { createDefaultUserSettings, type UserSettings } from '../features/settings/settingsTypes.ts'

const FixedTimestamp = '2026-08-25T12:30:00Z'

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
    create: (title = 'New conversation', enabledSourceKeys = []) => {
      const conversation: ConversationDetails = {
        id: crypto.randomUUID(),
        title,
        enabledSourceKeys: [...enabledSourceKeys],
        messages: [],
        createdAtUtc: FixedTimestamp,
        updatedAtUtc: FixedTimestamp,
      }
      conversations.set(conversation.id, conversation)
      return Promise.resolve(cloneConversation(conversation))
    },
    get: (conversationId) => Promise.resolve(cloneConversation(getConversation(conversations, conversationId))),
    appendMessage: (conversationId, messageId, content) => {
      const current = getConversation(conversations, conversationId)
      const conversation: ConversationDetails = {
        ...current,
        messages: [
          ...current.messages,
          { id: messageId, role: 'User', content, createdAtUtc: FixedTimestamp },
          { id: crypto.randomUUID(), role: 'Assistant', content: 'This local demo answer represents the validated grounded response returned by the production API.', createdAtUtc: FixedTimestamp },
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
    messages: conversation.messages.map((message) => ({ ...message })),
  }
}

function toSummary(conversation: ConversationDetails): ConversationSummary {
  return {
    id: conversation.id,
    title: conversation.title,
    enabledSourceKeys: [...conversation.enabledSourceKeys],
    updatedAtUtc: conversation.updatedAtUtc,
  }
}
