import { afterEach, describe, expect, it, vi } from 'vitest'
import type { ApiClient } from '../../api/apiClient.ts'
import { createBackendDvarTorahClient } from './dvarTorahClient.ts'
import type { WeeklyDvarTorahArchiveResponse, WeeklyDvarTorahArticle, WeeklyDvarTorahResponse } from './dvarTorahTypes.ts'

describe('createBackendDvarTorahClient', () => {
  afterEach(() => vi.useRealTimers())

  it('shares preloading with navigation and keeps navigation alive when the background consumer cancels', async () => {
    let complete!: (value: WeeklyDvarTorahResponse) => void
    const request = vi.fn().mockImplementation(() => new Promise<WeeklyDvarTorahResponse>(resolve => { complete = resolve }))
    const client = createBackendDvarTorahClient(createApiClient(request))
    const background = new AbortController()
    const preload = client.getCurrent(false, background.signal)
    const cancelled = expect(preload).rejects.toMatchObject({ name: 'AbortError' })
    const navigation = client.getCurrent()
    await Promise.resolve()

    background.abort()
    expect(request.mock.calls[0][1].signal.aborted).toBe(false)
    const publication = currentPublication()
    complete(publication)

    await cancelled
    expect(await navigation).toBe(publication)
    expect(client.getCachedCurrent?.()).toBe(publication)
    expect(await client.getCurrent()).toBe(publication)
    expect(request).toHaveBeenCalledOnce()
  })

  it('does not cache a cancelled preload and can retry after the connection improves', async () => {
    let complete!: (value: WeeklyDvarTorahResponse) => void
    const publication = currentPublication()
    const request = vi.fn().mockImplementationOnce(() => new Promise<WeeklyDvarTorahResponse>(resolve => { complete = resolve })).mockResolvedValueOnce(publication)
    const client = createBackendDvarTorahClient(createApiClient(request))
    const background = new AbortController()
    const preload = client.getCurrent(false, background.signal)
    const cancelled = expect(preload).rejects.toMatchObject({ name: 'AbortError' })
    await Promise.resolve()

    background.abort()
    expect(request.mock.calls[0][1].signal.aborted).toBe(true)
    complete(publication)
    await cancelled

    expect(client.getCachedCurrent?.()).toBeNull()
    expect(await client.getCurrent()).toBe(publication)
    expect(request).toHaveBeenCalledTimes(2)
  })

  it('expires its synchronous page snapshot and fetches fresh publication data', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime('2026-09-11T12:00:00Z')
    const publication = currentPublication()
    const request = vi.fn().mockResolvedValue(publication)
    const client = createBackendDvarTorahClient(createApiClient(request))
    expect(client.getCachedCurrent?.()).toBeNull()
    await client.getCurrent()
    expect(client.getCachedCurrent?.()).toBe(publication)

    vi.setSystemTime('2026-09-11T12:05:00Z')

    expect(client.getCachedCurrent?.()).toBeNull()
    await client.getCurrent()
    expect(request).toHaveBeenCalledTimes(2)
  })

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

  it('requests filtered archive results and persists only the selected teaching state', async () => {
    const request = vi.fn().mockResolvedValue({ readWeekKeys: [] })
    const client = createBackendDvarTorahClient(createApiClient(request))
    const controller = new AbortController()
    await client.getReadState(controller.signal)
    expect(request).toHaveBeenLastCalledWith('/api/dvar-torah/read-state', { signal: controller.signal, cache: 'no-store' })
    await client.setReadState('diaspora:2026-08-29', true)
    expect(request).toHaveBeenLastCalledWith('/api/dvar-torah/read-state/diaspora%3A2026-08-29', { method: 'PUT', body: '{"isRead":true}' })
    await client.setReadState('diaspora:2026-08-29', false)
    expect(request).toHaveBeenLastCalledWith('/api/dvar-torah/read-state/diaspora%3A2026-08-29', { method: 'PUT', body: '{"isRead":false}' })
    await client.getArchive({ search: 'community', readStatus: 'unread', page: 2 })
    expect(request).toHaveBeenLastCalledWith('/api/dvar-torah/archive?page=2&pageSize=10&readStatus=unread&search=community')
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

function currentPublication(): WeeklyDvarTorahResponse {
  const week = { weekKey: 'diaspora:2026-09-12', shabbatDate: '2026-09-12', hebrewDate: '1 Tishrei', parashah: null, holiday: 'Rosh Hashanah', inIsrael: false }
  return {
    currentWeek: week, isCurrentWeek: true,
    dvarTorah: { week, title: 'A new beginning', body: 'Choose kindness this week.', centralTeaching: null, tags: [], sources: [], torahGroundingPercent: 100, generatedAtUtc: '2026-09-10T12:00:00Z', publishedAtUtc: '2026-09-10T12:00:00Z', audio: null },
  }
}
