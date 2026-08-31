import { createApiClient, type ApiClient } from '../../api/apiClient.ts'
import { normalizeConversationTitle, type ConversationDetails, type ConversationMessage, type ConversationSummary } from './conversationData.ts'

export interface ConversationClient {
  list(): Promise<ConversationSummary[]>
  createWithMessage(messageId: string, content: string, enabledSourceKeys: readonly string[]): Promise<ConversationTurn>
  get(conversationId: string): Promise<ConversationDetails>
  appendMessage(conversationId: string, messageId: string, content: string): Promise<ConversationTurn>
  rename(conversationId: string, title: string): Promise<void>
  updateSources(conversationId: string, enabledSourceKeys: readonly string[]): Promise<void>
  delete(conversationId: string): Promise<void>
}

interface ConversationTurnBase {
  status: string
  message: string | null
}

export interface CompactConversationTurn extends ConversationTurnBase {
  conversation: ConversationSummary
  messages: ConversationMessage[]
  createdAtUtc: string
}

export interface FullConversationTurn extends ConversationTurnBase {
  conversation: ConversationDetails
}

export type ConversationTurn = CompactConversationTurn | FullConversationTurn

export function createBackendConversationClient(apiClient: ApiClient = createApiClient()): ConversationClient {
  return {
    async list() {
      const conversations = await apiClient.request<ConversationSummary[]>('/api/conversations')
      return conversations.map(normalizeConversationSummary)
    },
    async createWithMessage(messageId, content, enabledSourceKeys) {
      const turn = await apiClient.request<ConversationTurn>('/api/conversations?compact=true', {
        method: 'POST',
        body: JSON.stringify({ messageId, content, enabledSourceKeys }),
      })
      return normalizeConversationTurn(turn)
    },
    async get(conversationId) {
      const conversation = await apiClient.request<ConversationDetails>(`/api/conversations/${encodeURIComponent(conversationId)}`)
      return normalizeConversationDetails(conversation)
    },
    async appendMessage(conversationId, messageId, content) {
      const turn = await apiClient.request<ConversationTurn>(`/api/conversations/${encodeURIComponent(conversationId)}/messages?compact=true`, {
        method: 'POST',
        body: JSON.stringify({ messageId, content }),
      })
      return normalizeConversationTurn(turn)
    },
    rename(conversationId, title) {
      return apiClient.request<void>(`/api/conversations/${encodeURIComponent(conversationId)}/title`, {
        method: 'PUT',
        body: JSON.stringify({ title }),
      })
    },
    updateSources(conversationId, enabledSourceKeys) {
      return apiClient.request<void>(`/api/conversations/${encodeURIComponent(conversationId)}/sources`, {
        method: 'PUT',
        body: JSON.stringify({ enabledSourceKeys }),
      })
    },
    delete(conversationId) {
      return apiClient.request<void>(`/api/conversations/${encodeURIComponent(conversationId)}`, { method: 'DELETE' })
    },
  }
}

function normalizeConversationSummary(conversation: ConversationSummary): ConversationSummary {
  return { ...conversation, title: normalizeConversationTitle(conversation.title) }
}

function normalizeConversationDetails(conversation: ConversationDetails): ConversationDetails {
  return { ...conversation, title: normalizeConversationTitle(conversation.title) }
}

function normalizeConversationTurn(turn: ConversationTurn): ConversationTurn {
  return 'messages' in turn
    ? { ...turn, conversation: normalizeConversationSummary(turn.conversation) }
    : { ...turn, conversation: normalizeConversationDetails(turn.conversation) }
}
