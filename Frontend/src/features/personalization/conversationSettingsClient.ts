import { createApiClient, type ApiClient } from '../../api/apiClient.ts'
import type { UsageSummary, UserSettings } from '../settings/settingsTypes.ts'
import type { PersonalizationProfile } from './personalizationTypes.ts'

export interface PersonalizationEnvelope {
  isConfigured: boolean
  personalization: PersonalizationProfile | null
}

export interface ConversationSettingsClient {
  getPersonalization(): Promise<PersonalizationEnvelope>
  updatePersonalization(profile: PersonalizationProfile): Promise<PersonalizationProfile>
  getPreferences(): Promise<UserSettings>
  updatePreferences(settings: UserSettings): Promise<UserSettings>
  getUsage(): Promise<UsageSummary>
}

interface PersonalizationApiEnvelope {
  isConfigured: boolean
  personalization: PersonalizationApiResponse | null
}

interface PersonalizationApiResponse {
  fullName: string
  birthDateTime: string
  birthTimeZone: string
  conversationLanguage: string
  quotationLanguage: string
  religiousMovement: string
  jewishHeritage: string
  additionalContext?: string
}

export function createBackendConversationSettingsClient(apiClient: ApiClient = createApiClient()): ConversationSettingsClient {
  return {
    async getPersonalization() {
      return mapEnvelope(await apiClient.request<PersonalizationApiEnvelope>('/api/conversation-settings/personalization'))
    },
    async updatePersonalization(profile) {
      const response = await apiClient.request<PersonalizationApiEnvelope>('/api/conversation-settings/personalization', {
        method: 'PUT',
        body: JSON.stringify({
          ...profile,
          birthDateTime: normalizeBirthDateTimeForApi(profile.birthDateTime),
          additionalContext: profile.additionalContext.trim() || null,
        }),
      })
      if (response.personalization === null) {
        throw new Error('The AskRabbi API did not return the saved personalization.')
      }
      return mapProfile(response.personalization)
    },
    getPreferences() {
      return apiClient.request<UserSettings>('/api/conversation-settings/preferences')
    },
    updatePreferences(settings) {
      return apiClient.request<UserSettings>('/api/conversation-settings/preferences', {
        method: 'PUT',
        body: JSON.stringify(settings),
      })
    },
    getUsage() {
      return apiClient.request<UsageSummary>('/api/conversation-settings/usage')
    },
  }
}

function mapEnvelope(response: PersonalizationApiEnvelope): PersonalizationEnvelope {
  return {
    isConfigured: response.isConfigured,
    personalization: response.personalization === null ? null : mapProfile(response.personalization),
  }
}

function mapProfile(response: PersonalizationApiResponse): PersonalizationProfile {
  return {
    ...response,
    birthDateTime: response.birthDateTime.slice(0, 16),
    additionalContext: response.additionalContext ?? '',
  }
}

function normalizeBirthDateTimeForApi(value: string) {
  return value.length === 16 ? `${value}:00` : value
}
