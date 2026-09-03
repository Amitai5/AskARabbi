import { createApiClient, type ApiClient } from '../../api/apiClient.ts'
import type { WeeklyDvarTorahArchiveQuery, WeeklyDvarTorahArchiveResponse, WeeklyDvarTorahArticle, WeeklyDvarTorahResponse } from './dvarTorahTypes.ts'

export interface DvarTorahClient {
  getCurrent(forceRefresh?: boolean): Promise<WeeklyDvarTorahResponse>
  getArchive(query?: WeeklyDvarTorahArchiveQuery): Promise<WeeklyDvarTorahArchiveResponse>
  getArchived(weekKey: string): Promise<WeeklyDvarTorahArticle>
}

interface CachedPublication {
  value: WeeklyDvarTorahResponse
  expiresAt: number
}

const PendingPublicationCacheMilliseconds = 5 * 60 * 1000
const OneDayMilliseconds = 24 * 60 * 60 * 1000

export function createBackendDvarTorahClient(apiClient: ApiClient = createApiClient()): DvarTorahClient {
  let cachedPublication: CachedPublication | null = null
  let currentRequest: Promise<WeeklyDvarTorahResponse> | null = null

  return {
    getCurrent(forceRefresh = false) {
      if (!forceRefresh && cachedPublication !== null && cachedPublication.expiresAt > Date.now()) {
        return Promise.resolve(cachedPublication.value)
      }
      if (currentRequest !== null) {
        return currentRequest
      }

      currentRequest = apiClient.request<WeeklyDvarTorahResponse>('/api/dvar-torah')
        .then((value) => {
          cachedPublication = { value, expiresAt: getCacheExpiration(value) }
          return value
        })
        .finally(() => {
          currentRequest = null
        })
      return currentRequest
    },
    getArchive(query = {}) {
      const parameters = new URLSearchParams({
        page: String(query.page ?? 1),
        pageSize: String(query.pageSize ?? 10),
      })
      const search = query.search?.trim()
      if (search !== undefined && search.length > 0) {
        parameters.set('search', search)
      }

      return apiClient.request<WeeklyDvarTorahArchiveResponse>(`/api/dvar-torah/archive?${parameters.toString()}`)
    },
    getArchived(weekKey) {
      return apiClient.request<WeeklyDvarTorahArticle>(`/api/dvar-torah/archive/${encodeURIComponent(weekKey)}`)
    },
  }
}

function getCacheExpiration(response: WeeklyDvarTorahResponse) {
  if (!response.isCurrentWeek || response.dvarTorah === null) {
    return Date.now() + PendingPublicationCacheMilliseconds
  }

  const shabbatStartUtc = Date.parse(`${response.currentWeek.shabbatDate}T00:00:00Z`)
  return Number.isNaN(shabbatStartUtc) ? Date.now() + PendingPublicationCacheMilliseconds : shabbatStartUtc + OneDayMilliseconds
}
