import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createCachedCalendarClient } from './cachedCalendarClient.ts'
import { addCivilDays } from './calendarAgenda.ts'
import { calendarOverview, fakeCalendarClient, RoshHashanah } from './calendarTestData.ts'

describe('account-scoped calendar cache', () => {
  beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime('2026-09-10T12:00:00Z') })
  afterEach(() => vi.useRealTimers())

  it('preloads once and serves shorter ranges including the end date without another request', async () => {
    const later = { ...RoshHashanah, id: 'late', beginningDate: addCivilDays('2026-09-10', 179), startDate: addCivilDays('2026-09-10', 180), endDate: addCivilDays('2026-09-10', 181) }
    const source = fakeCalendarClient(calendarOverview({ events: [RoshHashanah, later] }))
    const client = createCachedCalendarClient(source)

    await client.getOverview(360)
    expect((await client.getOverview(90)).events).toHaveLength(1)
    expect(client.getCachedOverview?.(180)?.events).toHaveLength(2)
    expect((await client.getOverview(180)).events[1].occurrences).toEqual(later.occurrences)
    expect(source.getOverview).toHaveBeenCalledTimes(1)
  })

  it('shares a preload with navigation and does not cancel a reader still using it', async () => {
    let complete!: (value: ReturnType<typeof calendarOverview>) => void
    const source = fakeCalendarClient()
    vi.mocked(source.getOverview).mockImplementation(() => new Promise(resolve => { complete = resolve }))
    const client = createCachedCalendarClient(source)
    const background = new AbortController()
    const preload = client.getOverview(360, background.signal)
    const preloadOutcome = expect(preload).rejects.toMatchObject({ name: 'AbortError' })
    const navigation = client.getOverview(90)
    await Promise.resolve()
    background.abort()
    expect(vi.mocked(source.getOverview).mock.calls[0][1]?.aborted).toBe(false)
    complete(calendarOverview())

    await preloadOutcome
    expect((await navigation).events).toHaveLength(1)
    expect(source.getOverview).toHaveBeenCalledOnce()
  })

  it('refreshes at sunset or midnight and isolates different account instances', async () => {
    const source = fakeCalendarClient(calendarOverview({ nextRefreshAtUtc: '2026-09-10T12:05:00Z' }))
    const first = createCachedCalendarClient(source)
    const second = createCachedCalendarClient(source)
    await first.getOverview(360)
    expect(second.getCachedOverview?.(90)).toBeNull()
    vi.setSystemTime('2026-09-10T12:05:00Z')
    expect(first.getCachedOverview?.(90)).toBeNull()
    await first.getOverview(90)
    expect(source.getOverview).toHaveBeenCalledTimes(2)
    first.invalidate?.()
    expect(first.getCachedOverview?.(90)).toBeNull()
  })

  it('preserves the full schedule when filters change and does not require another lookup', async () => {
    const source = fakeCalendarClient()
    const client = createCachedCalendarClient(source)
    const overview = await client.getOverview(360)
    await client.updatePreferences({ ...overview.preferences, majorHolidays: false })
    const updated = await client.getOverview(90)
    expect(updated.preferences.majorHolidays).toBe(false)
    expect(updated.events).toHaveLength(1)
    expect(source.getOverview).toHaveBeenCalledOnce()
  })

  it('retries a failed fetch and cancels discarded work without populating the cache', async () => {
    const source = fakeCalendarClient()
    vi.mocked(source.getOverview).mockRejectedValueOnce(new Error('unavailable'))
    const client = createCachedCalendarClient(source)
    await expect(client.getOverview(360)).rejects.toThrow('unavailable')
    expect(client.getCachedOverview?.(90)).toBeNull()
    await client.getOverview(360)
    expect(source.getOverview).toHaveBeenCalledTimes(2)
    client.invalidate?.()
    const controller = new AbortController()
    const pending = client.getOverview(90, controller.signal)
    controller.abort()
    await expect(pending).rejects.toMatchObject({ name: 'AbortError' })
    expect(client.getCachedOverview?.(90)).toBeNull()
  })
})
