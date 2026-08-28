import { AllSourceKeys } from './sourceOptions.ts'

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
}

export interface ConversationDetails extends ConversationSummary {
  messages: ConversationMessage[]
  createdAtUtc: string
  updatedAtUtc: string
}

export const InitialConversations: ConversationSummary[] = [
  { id: 'chicken-dairy', title: 'Chicken and dairy', enabledSourceKeys: [...AllSourceKeys] },
  { id: 'shabbat-automation', title: 'Shabbat and automation', enabledSourceKeys: [...AllSourceKeys] },
  { id: 'customs-differ', title: 'Why customs differ', enabledSourceKeys: [...AllSourceKeys] },
  { id: 'amidah', title: 'Understanding the Amidah', enabledSourceKeys: [...AllSourceKeys] },
  { id: 'mezuzah', title: 'A question about mezuzah', enabledSourceKeys: [...AllSourceKeys] },
]
