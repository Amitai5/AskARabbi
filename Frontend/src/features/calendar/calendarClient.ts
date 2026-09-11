import { createApiClient, type ApiClient } from '../../api/apiClient.ts'
import type { CalendarOverview, CalendarPreferences, CalendarPreferencesResponse, CalendarRange } from './calendarTypes.ts'

export const CalendarPreferencesChanged = 'askarabbi-calendar-preferences-changed'

export interface CalendarClient {
  getOverview(days: CalendarRange, signal?: AbortSignal): Promise<CalendarOverview>
  getPreferences(signal?: AbortSignal): Promise<CalendarPreferencesResponse>
  updatePreferences(preferences: CalendarPreferences, signal?: AbortSignal): Promise<CalendarPreferences>
}

// No module-level account cache: changing users must never expose another user's location.
export function createBackendCalendarClient(apiClient: ApiClient = createApiClient()): CalendarClient {
  return {
    getOverview: (days, signal) => apiClient.request(`/api/calendar/overview?days=${days}`, { signal, cache: 'no-store' }),
    getPreferences: (signal) => apiClient.request('/api/calendar/preferences', { signal, cache: 'no-store' }),
    updatePreferences: (preferences, signal) => apiClient.request('/api/calendar/preferences', { method: 'PUT', body: JSON.stringify(preferences), signal, cache: 'no-store' }),
  }
}
