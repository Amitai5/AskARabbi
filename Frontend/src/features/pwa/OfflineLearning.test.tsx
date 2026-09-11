import { StrictMode } from 'react'
import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { OfflineLearningProvider } from './OfflineLearning.tsx'
import { readOfflineLibrary, saveOfflineHolidays, OfflineLibraryCleared } from './offlineLibrary.ts'
import { syncOfflineTeaching } from './syncOfflineTeaching.ts'
import { fakeCalendarClient } from '../calendar/calendarTestData.ts'
import type { DvarTorahClient } from '../dvarTorah/dvarTorahClient.ts'

vi.mock('./offlineLibrary.ts', async original => ({ ...await original<typeof import('./offlineLibrary.ts')>(), readOfflineLibrary: vi.fn(), saveOfflineHolidays: vi.fn() }))
vi.mock('./syncOfflineTeaching.ts', () => ({ syncOfflineTeaching: vi.fn() }))

describe('signed-in background learning', () => {
  let connection: EventTarget & { effectiveType: string; saveData: boolean }
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime('2026-09-10T12:00:00Z')
    connection = Object.assign(new EventTarget(), { effectiveType: '4g', saveData: false })
    Object.defineProperty(navigator, 'connection', { configurable: true, value: connection })
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('visible')
    vi.mocked(readOfflineLibrary).mockResolvedValue({ teaching: null, revision: 3, audioEnabled: true })
    vi.mocked(syncOfflineTeaching).mockResolvedValue(undefined)
    vi.mocked(saveOfflineHolidays).mockResolvedValue({ teaching: null, revision: 3, audioEnabled: true })
  })
  afterEach(() => { vi.restoreAllMocks(); vi.clearAllMocks(); vi.useRealTimers(); Reflect.deleteProperty(navigator, 'connection') })

  it('renders immediately and later preloads teaching and 360 days once, including Strict Mode', async () => {
    const calendar = fakeCalendarClient()
    const dvarTorahClient = fakeDvarClient()
    render(<StrictMode><OfflineLearningProvider client={dvarTorahClient} calendarClient={calendar}><p>Chat is ready</p></OfflineLearningProvider></StrictMode>)
    expect(screen.getByText('Chat is ready')).toBeVisible()
    expect(calendar.getOverview).not.toHaveBeenCalled()
    await act(async () => { await vi.advanceTimersByTimeAsync(2500) })
    expect(calendar.getOverview).toHaveBeenCalledExactlyOnceWith(360, expect.any(AbortSignal))
    expect(syncOfflineTeaching).toHaveBeenCalledExactlyOnceWith(dvarTorahClient, expect.any(AbortSignal), expect.any(Function))
    expect(saveOfflineHolidays).toHaveBeenCalledWith(expect.objectContaining({ events: expect.any(Array) }), 3, expect.any(AbortSignal))
    fireEvent.focus(window)
    await act(async () => { await vi.advanceTimersByTimeAsync(2500) })
    expect(calendar.getOverview).toHaveBeenCalledOnce()
  })

  it('skips slow connections and starts later only after a good connection returns', async () => {
    connection.effectiveType = '3g'
    const calendar = fakeCalendarClient()
    render(<OfflineLearningProvider client={fakeDvarClient()} calendarClient={calendar}>Ready</OfflineLearningProvider>)
    await act(async () => { await vi.advanceTimersByTimeAsync(5000) })
    expect(calendar.getOverview).not.toHaveBeenCalled()
    expect(syncOfflineTeaching).not.toHaveBeenCalled()
    connection.effectiveType = '4g'
    act(() => connection.dispatchEvent(new Event('change')))
    await act(async () => { await vi.advanceTimersByTimeAsync(2500) })
    expect(calendar.getOverview).toHaveBeenCalledOnce()
  })

  it('aborts background work when Data Saver is enabled and never saves the late result', async () => {
    const calendar = fakeCalendarClient()
    let complete!: (value: Awaited<ReturnType<typeof calendar.getOverview>>) => void
    const original = await calendar.getOverview(360)
    vi.mocked(calendar.getOverview).mockClear().mockImplementation(() => new Promise(resolve => { complete = resolve }))
    render(<OfflineLearningProvider client={fakeDvarClient()} calendarClient={calendar}>Ready</OfflineLearningProvider>)
    await act(async () => { await vi.advanceTimersByTimeAsync(2500) })
    const signal = vi.mocked(calendar.getOverview).mock.calls[0][1]
    connection.saveData = true
    act(() => connection.dispatchEvent(new Event('change')))
    expect(signal?.aborted).toBe(true)
    await act(async () => complete(original))
    expect(saveOfflineHolidays).not.toHaveBeenCalled()
  })

  it('cancels scheduled preloading on logout and unmount', async () => {
    const calendar = fakeCalendarClient()
    const { unmount } = render(<OfflineLearningProvider client={fakeDvarClient()} calendarClient={calendar}>Ready</OfflineLearningProvider>)
    act(() => window.dispatchEvent(new Event(OfflineLibraryCleared)))
    fireEvent.focus(window)
    await act(async () => { await vi.advanceTimersByTimeAsync(5000) })
    expect(calendar.getOverview).not.toHaveBeenCalled()
    unmount()
    expect(syncOfflineTeaching).not.toHaveBeenCalled()
  })

  it('still preloads into memory when this browser cannot use offline storage', async () => {
    vi.mocked(readOfflineLibrary).mockRejectedValue(new Error('Storage unavailable'))
    const calendar = fakeCalendarClient()
    const client = fakeDvarClient()
    const getCurrent = vi.spyOn(client, 'getCurrent')
    render(<OfflineLearningProvider client={client} calendarClient={calendar}>Ready</OfflineLearningProvider>)
    await act(async () => { await vi.advanceTimersByTimeAsync(2500) })
    expect(getCurrent).toHaveBeenCalledOnce()
    expect(calendar.getOverview).toHaveBeenCalledOnce()
    expect(saveOfflineHolidays).not.toHaveBeenCalled()
  })
})

function fakeDvarClient(): DvarTorahClient {
  return {
    getCurrent: vi.fn(async () => ({ currentWeek: { weekKey: 'diaspora:2026-09-12', shabbatDate: '2026-09-12', hebrewDate: '1 Tishrei', parashah: null, holiday: 'Rosh Hashanah', inIsrael: false }, dvarTorah: null, isCurrentWeek: false })),
    getArchive: vi.fn(), getArchived: vi.fn(), getAudioUrl: vi.fn(), getAudioTimings: vi.fn(),
  }
}
