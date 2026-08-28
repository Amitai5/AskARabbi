import { createApiClient, type ApiClient } from '../../api/apiClient.ts'
import type { ConversationDetails, ConversationSummary } from './conversationData.ts'

export interface ConversationClient {
  list(): Promise<ConversationSummary[]>
  create(title?: string, enabledSourceKeys?: readonly string[]): Promise<ConversationDetails>
  get(conversationId: string): Promise<ConversationDetails>
  appendMessage(conversationId: string, messageId: string, content: string): Promise<ConversationDetails>
  rename(conversationId: string, title: string): Promise<void>
  updateSources(conversationId: string, enabledSourceKeys: readonly string[]): Promise<void>
  delete(conversationId: string): Promise<void>
}

interface ConversationTurnResponse {
  status: string
  conversation: ConversationDetails
}

export function createBackendConversationClient(apiClient: ApiClient = createApiClient()): ConversationClient {
  return {
    list() {
      return apiClient.request<ConversationSummary[]>('/api/conversations')
    },
    create(title, enabledSourceKeys) {
      return apiClient.request<ConversationDetails>('/api/conversations', {
        method: 'POST',
        body: JSON.stringify({ title, enabledSourceKeys }),
      })
    },
    get(conversationId) {
      return apiClient.request<ConversationDetails>(`/api/conversations/${encodeURIComponent(conversationId)}`)
    },
    async appendMessage(conversationId, messageId, content) {
      const response = await apiClient.request<ConversationTurnResponse>(`/api/conversations/${encodeURIComponent(conversationId)}/messages`, {
        method: 'POST',
        body: JSON.stringify({ messageId, content }),
      })
      return response.conversation
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
