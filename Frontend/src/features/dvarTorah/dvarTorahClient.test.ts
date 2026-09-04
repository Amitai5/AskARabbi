import { describe, expect, it, vi } from 'vitest'
import type { ApiClient } from '../../api/apiClient.ts'
import { createBackendDvarTorahClient } from './dvarTorahClient.ts'
import type { WeeklyDvarTorahArchiveResponse, WeeklyDvarTorahArticle } from './dvarTorahTypes.ts'

describe('createBackendDvarTorahClient', () => {
  it('requests a normalized searchable archive page', async () => {
    const response: WeeklyDvarTorahArchiveResponse = { items: [], page: 2, pageSize: 10, totalCount: 0, totalPages: 0 }
    const request = vi.fn().mockResolvedValue(response)
    const client = createBackendDvarTorahClient(createApiClient(request))

    const result = await client.getArchive({ search: '  community & care  ', page: 2 })

    expect(result).toBe(response)
    expect(request).toHaveBeenCalledWith('/api/dvar-torah/archive?page=2&pageSize=10&search=community+%26+care')
  })

  it('URL-encodes the weekly key when requesting an archived article', async () => {
    const article = {} as WeeklyDvarTorahArticle
    const request = vi.fn().mockResolvedValue(article)
    const client = createBackendDvarTorahClient(createApiClient(request))

    const result = await client.getArchived('diaspora:2026-08-29')

    expect(result).toBe(article)
    expect(request).toHaveBeenCalledWith('/api/dvar-torah/archive/diaspora%3A2026-08-29')
  })

  it('constructs audio paths only on the configured API and version-binds both requests', async () => {
    const request = vi.fn().mockResolvedValue({ words: [] })
    const client = createBackendDvarTorahClient(createApiClient(request))
    const controller = new AbortController()

    const url = client.getAudioUrl('diaspora:2026-08-29', 'version+1')
    await client.getAudioTimings('diaspora:2026-08-29', 'version+1', controller.signal)

    expect(url).toBe('https://api.askarabbi.test/api/dvar-torah/archive/diaspora%3A2026-08-29/audio?version=version%2B1')
    expect(request).toHaveBeenCalledWith('/api/dvar-torah/archive/diaspora%3A2026-08-29/audio/timings?version=version%2B1', { signal: controller.signal })
  })
})

function createApiClient(request: ApiClient['request']): ApiClient {
  return { baseUrl: 'https://api.askarabbi.test', request }
}
