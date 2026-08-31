import { CoreSourceKeys } from './sourceOptions.ts'

export const DefaultConversationTitle = 'New Conversation'

export function normalizeConversationTitle(title: string | null | undefined) {
  const normalized = title?.trim() ?? ''
  return normalized.length === 0 ? DefaultConversationTitle : normalized
}

export interface ConversationSummary {
  id: string
  title: string
  enabledSourceKeys: string[]
  updatedAtUtc?: string
}

export interface ConversationMessage {
  id: string
  role: 'User' | 'Assistant'
  content: string
  createdAtUtc: string
  sources?: ConversationSource[]
}

export interface ConversationSource {
  number: number
  title: string
  hebrewTitle: string
  canonicalReference: string
  edition: string
  language: string
  collection: string
  license: string
  sourceUrl: string
  attributionUrl: string
  quotations: string[]
  context: string
  isExcerpt: boolean
}

export interface ConversationDetails extends ConversationSummary {
  messages: ConversationMessage[]
  createdAtUtc: string
  updatedAtUtc: string
}

export const InitialConversations: ConversationSummary[] = [
  { id: 'chicken-dairy', title: 'Chicken and dairy', enabledSourceKeys: [...CoreSourceKeys] },
  { id: 'shabbat-automation', title: 'Shabbat and automation', enabledSourceKeys: [...CoreSourceKeys] },
  { id: 'customs-differ', title: 'Why customs differ', enabledSourceKeys: [...CoreSourceKeys] },
  { id: 'amidah', title: 'Understanding the Amidah', enabledSourceKeys: [...CoreSourceKeys] },
  { id: 'mezuzah', title: 'A question about mezuzah', enabledSourceKeys: [...CoreSourceKeys] },
]
