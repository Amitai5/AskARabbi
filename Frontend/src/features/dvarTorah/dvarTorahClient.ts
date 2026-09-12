import { createApiClient, type ApiClient } from '../../api/apiClient.ts'
import { createSharedRequest } from '../../api/sharedRequest.ts'
import type { TeachingReadState, WeeklyDvarTorahArchiveQuery, WeeklyDvarTorahArchiveResponse, WeeklyDvarTorahArticle, WeeklyDvarTorahResponse } from './dvarTorahTypes.ts'

export interface DvarTorahClient {
  getReadState(signal?: AbortSignal): Promise<TeachingReadState>
  setReadState(weekKey: string, isRead: boolean): Promise<void>
  getCurrent(forceRefresh?: boolean, signal?: AbortSignal): Promise<WeeklyDvarTorahResponse>
  getCachedCurrent?(): WeeklyDvarTorahResponse | null
  getArchive(query?: WeeklyDvarTorahArchiveQuery): Promise<WeeklyDvarTorahArchiveResponse>
  getArchived(weekKey: string): Promise<WeeklyDvarTorahArticle>
  getAudioUrl(weekKey: string, version: string): string
  getAudioTimings(weekKey: string, version: string, signal?: AbortSignal): Promise<unknown>
}

interface CachedPublication {
  value: WeeklyDvarTorahResponse
  expiresAt: number
}

const PendingPublicationCacheMilliseconds = 5 * 60 * 1000
const OneDayMilliseconds = 24 * 60 * 60 * 1000

export function createBackendDvarTorahClient(apiClient: ApiClient = createApiClient()): DvarTorahClient {
  let cachedPublication: CachedPublication | null = null
  let currentRequest: ReturnType<typeof createSharedRequest<WeeklyDvarTorahResponse>> | null = null

  return {
    getReadState: (signal) => apiClient.request<TeachingReadState>('/api/dvar-torah/read-state', { signal, cache: 'no-store' }),
    setReadState: (weekKey, isRead) => apiClient.request<void>(`/api/dvar-torah/read-state/${encodeURIComponent(weekKey)}`, { method: 'PUT', body: JSON.stringify({ isRead }) }),
    getCachedCurrent: () => cachedPublication !== null && cachedPublication.expiresAt > Date.now() ? cachedPublication.value : null,
    getCurrent(forceRefresh = false, signal) {
      if (signal?.aborted) { return Promise.reject(signal.reason) }
      if (!forceRefresh && cachedPublication !== null && cachedPublication.expiresAt > Date.now()) {
        return Promise.resolve(cachedPublication.value)
      }
      if (currentRequest !== null && !currentRequest.signal.aborted) {
        return currentRequest.read(signal)
      }

      const request = createSharedRequest(async sharedSignal => {
        try {
          const value = await apiClient.request<WeeklyDvarTorahResponse>('/api/dvar-torah', { signal: sharedSignal })
          sharedSignal.throwIfAborted()
          cachedPublication = { value, expiresAt: getCacheExpiration(value) }
          return value
        } finally {
          if (currentRequest === request) { currentRequest = null }
        }
      })
      currentRequest = request
      return request.read(signal)
    },
    getArchive(query = {}) {
      const parameters = new URLSearchParams({
        page: String(query.page ?? 1),
        pageSize: String(query.pageSize ?? 10),
      })
      const search = query.search?.trim()
      if (query.readStatus && query.readStatus !== 'all') { parameters.set('readStatus', query.readStatus) }
      if (search !== undefined && search.length > 0) {
        parameters.set('search', search)
      }

      return apiClient.request<WeeklyDvarTorahArchiveResponse>(`/api/dvar-torah/archive?${parameters.toString()}`)
    },
    getArchived(weekKey) {
      return apiClient.request<WeeklyDvarTorahArticle>(`/api/dvar-torah/archive/${encodeURIComponent(weekKey)}`)
    },
    getAudioUrl(weekKey, version) {
      return `${apiClient.baseUrl}${getAudioPath(weekKey)}?version=${encodeURIComponent(version)}`
    },
    getAudioTimings(weekKey, version, signal) {
      return apiClient.request<unknown>(`${getAudioPath(weekKey)}/timings?version=${encodeURIComponent(version)}`, { signal })
    },
  }
}

function getAudioPath(weekKey: string) {
  return `/api/dvar-torah/archive/${encodeURIComponent(weekKey)}/audio`
}

function getCacheExpiration(response: WeeklyDvarTorahResponse) {
  if (!response.isCurrentWeek || response.dvarTorah === null || response.dvarTorah.audio == null) {
    return Date.now() + PendingPublicationCacheMilliseconds
  }

  const shabbatStartUtc = Date.parse(`${response.currentWeek.shabbatDate}T00:00:00Z`)
  return Number.isNaN(shabbatStartUtc) ? Date.now() + PendingPublicationCacheMilliseconds : shabbatStartUtc + OneDayMilliseconds
}
