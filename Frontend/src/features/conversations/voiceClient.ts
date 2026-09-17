import { ApiError, createApiClient } from '../../api/apiClient.ts'

export interface VoiceClient {
  isAvailable(signal: AbortSignal): Promise<boolean>
  transcribe(pcm: Blob, language: string, signal: AbortSignal): Promise<string>
  synthesize(conversationId: string, messageId: string, signal: AbortSignal): Promise<Blob>
}

export const SpokenLanguages: Readonly<Record<string, string>> = {
  English: 'en-US', Hebrew: 'he-IL', French: 'fr-FR', German: 'de-DE', Italian: 'it-IT',
  Persian: 'fa-IR', Polish: 'pl-PL', Russian: 'ru-RU', Spanish: 'es-ES',
}

export function createVoiceClient(baseUrl = createApiClient().baseUrl): VoiceClient {
  async function request(path: string, init: RequestInit) {
    const response = await fetch(`${baseUrl}${path}`, { ...init, credentials: 'include', cache: 'no-store' })
    if (!response.ok) {
      if (response.status === 401) { throw new Error('Sign in again to use voice. Your draft has been kept.') }
      if (response.status === 429) { throw new Error('Voice is busy. Wait a minute and try again, or keep typing.') }
      throw new ApiError(response.status, await response.json().catch(() => ({ detail: 'Voice is unavailable. You can still type and read the answer.' })))
    }
    return response
  }
  return {
    async isAvailable(signal) {
      const response = await request('/api/voice', { signal })
      return (await response.json() as { enabled: boolean }).enabled
    },
    async transcribe(pcm, language, signal) {
      const response = await request(`/api/voice/transcriptions?language=${encodeURIComponent(language)}`, {
        method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: pcm, signal,
      })
      return (await response.json() as { text: string }).text
    },
    async synthesize(conversationId, messageId, signal) {
      const response = await request(`/api/voice/conversations/${encodeURIComponent(conversationId)}/messages/${encodeURIComponent(messageId)}/audio`, { method: 'POST', signal })
      return response.blob()
    },
  }
}

export const VoiceClient = createVoiceClient()
// Each deliberate voice action stops prior playback or recording in this tab.
export const VoiceActivityEvent = 'askarabbi:voice-activity'
