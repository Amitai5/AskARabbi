import { createApiClient, type ApiClient } from '../../api/apiClient.ts'
import { createDefaultUserSettings, type UsageSummary, type UserSettings } from '../settings/settingsTypes.ts'
import type { PersonalizationProfile } from './personalizationTypes.ts'
import type { CalendarLocation } from '../calendar/calendarTypes.ts'
import type { ReadingPreferences } from '../reading/readingPreferences.ts'

export interface PersonalizationEnvelope {
  isConfigured: boolean
  personalization: PersonalizationProfile | null
}

export interface ConversationSettingsClient {
  getReadingPreferences(signal?: AbortSignal): Promise<ReadingPreferences>
  updateReadingPreferences(preferences: ReadingPreferences): Promise<ReadingPreferences>
  getLocations(signal?: AbortSignal): Promise<CalendarLocation[]>
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
  birthLocation?: CalendarLocation | null
  currentLocation?: CalendarLocation | null
  conversationLanguage: string
  quotationLanguage: string
  religiousMovement: string
  jewishHeritage: string
  additionalContext?: string
}

export function createBackendConversationSettingsClient(apiClient: ApiClient = createApiClient()): ConversationSettingsClient {
  return {
    getReadingPreferences: (signal) => apiClient.request<ReadingPreferences>('/api/conversation-settings/reading', { signal }),
    updateReadingPreferences: (preferences) => apiClient.request<ReadingPreferences>('/api/conversation-settings/reading', { method: 'PUT', body: JSON.stringify(preferences) }),
    getLocations: (signal) => apiClient.request<CalendarLocation[]>('/api/conversation-settings/locations', { signal }),
    async getPersonalization() {
      return mapEnvelope(await apiClient.request<PersonalizationApiEnvelope>('/api/conversation-settings/personalization'))
    },
    async updatePersonalization(profile) {
      const response = await apiClient.request<PersonalizationApiEnvelope>('/api/conversation-settings/personalization', {
        method: 'PUT',
        body: JSON.stringify({
          ...profile,
          birthDateTime: normalizeBirthDateTimeForApi(profile.birthDateTime),
          birthLocation: profile.birthLocation ? { kind: profile.birthLocation.kind, id: profile.birthLocation.id } : null,
          currentLocation: profile.currentLocation ? { kind: profile.currentLocation.kind, id: profile.currentLocation.id } : null,
          additionalContext: profile.additionalContext.trim() || null,
        }),
      })
      if (response.personalization === null) {
        throw new Error('The AskRabbi API did not return the saved personalization.')
      }
      return mapProfile(response.personalization)
    },
    async getPreferences() {
      return normalizePreferences(await apiClient.request<UserSettings>('/api/conversation-settings/preferences'))
    },
    async updatePreferences(settings) {
      return normalizePreferences(await apiClient.request<UserSettings>('/api/conversation-settings/preferences', {
        method: 'PUT',
        body: JSON.stringify(settings),
      }))
    },
    getUsage() {
      return apiClient.request<UsageSummary>('/api/conversation-settings/usage', { cache: 'no-store' })
    },
  }
}

function normalizePreferences(value: UserSettings): UserSettings {
  return { ...createDefaultUserSettings(), ...value, enterSendsMessage: value.enterSendsMessage === true }
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
