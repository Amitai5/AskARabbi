import { createApiClient, type ApiClient } from '../../api/apiClient.ts'
import type { ConversationDetails, ConversationMessage, ConversationSummary } from './conversationData.ts'

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
    list() {
      return apiClient.request<ConversationSummary[]>('/api/conversations')
    },
    createWithMessage(messageId, content, enabledSourceKeys) {
      return apiClient.request<ConversationTurn>('/api/conversations?compact=true', {
        method: 'POST',
        body: JSON.stringify({ messageId, content, enabledSourceKeys }),
      })
    },
    get(conversationId) {
      return apiClient.request<ConversationDetails>(`/api/conversations/${encodeURIComponent(conversationId)}`)
    },
    appendMessage(conversationId, messageId, content) {
      return apiClient.request<ConversationTurn>(`/api/conversations/${encodeURIComponent(conversationId)}/messages?compact=true`, {
        method: 'POST',
        body: JSON.stringify({ messageId, content }),
      })
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
