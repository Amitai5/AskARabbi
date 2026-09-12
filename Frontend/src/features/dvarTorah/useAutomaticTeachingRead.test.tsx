import { act, cleanup, fireEvent, renderHook } from '@testing-library/react'
import { StrictMode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAutomaticTeachingRead } from './useAutomaticTeachingRead.ts'
import type { TeachingReadProgress } from './useTeachingReadState.ts'

const WeekKey = 'diaspora:2026-09-12'
const ThresholdMs = 118_801
let visibility: DocumentVisibilityState

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'performance'] })
  visibility = 'visible'
  vi.spyOn(document, 'visibilityState', 'get').mockImplementation(() => visibility)
})

afterEach(() => {
  cleanup()
  vi.useRealTimers()
  vi.restoreAllMocks()
})

describe('useAutomaticTeachingRead', () => {
  it('marks read once strictly after 33% of the displayed estimate, including in Strict Mode', async () => {
    const progress = createProgress()
    renderHook(() => useAutomaticTeachingRead(WeekKey, 6, progress), { wrapper: StrictMode })
    await advance(118_800)
    expect(progress.markRead).not.toHaveBeenCalled()
    await advance(1)
    expect(progress.markRead).toHaveBeenCalledExactlyOnceWith(WeekKey)
    await advance(600_000)
    expect(progress.markRead).toHaveBeenCalledTimes(1)
    expect(progress.toggle).not.toHaveBeenCalled()
  })

  it('accumulates visible time while excluding time in hidden tabs', async () => {
    const progress = createProgress()
    renderHook(() => useAutomaticTeachingRead(WeekKey, 6, progress))
    await advance(60_000)
    setVisibility('hidden')
    await advance(600_000)
    expect(progress.markRead).not.toHaveBeenCalled()
    setVisibility('visible')
    await advance(58_800)
    expect(progress.markRead).not.toHaveBeenCalled()
    await advance(1)
    expect(progress.markRead).toHaveBeenCalledExactlyOnceWith(WeekKey)
  })

  it('does not start counting until an initially hidden article becomes visible', async () => {
    visibility = 'hidden'
    const progress = createProgress()
    renderHook(() => useAutomaticTeachingRead(WeekKey, 6, progress))
    await advance(600_000)
    setVisibility('visible')
    await advance(ThresholdMs - 1)
    expect(progress.markRead).not.toHaveBeenCalled()
    await advance(1)
    expect(progress.markRead).toHaveBeenCalledExactlyOnceWith(WeekKey)
  })

  it('keeps elapsed time across rerenders and uses the latest save callback', async () => {
    const initial = createProgress()
    const { rerender } = renderHook(({ progress }) => useAutomaticTeachingRead(WeekKey, 6, progress), { initialProps: { progress: initial } })
    await advance(60_000)
    const latest = createProgress()
    rerender({ progress: latest })
    await advance(58_801)
    expect(initial.markRead).not.toHaveBeenCalled()
    expect(latest.markRead).toHaveBeenCalledExactlyOnceWith(WeekKey)
  })

  it('starts a new timer for another teaching and cancels outstanding work on unmount', async () => {
    const progress = createProgress()
    const { rerender, unmount } = renderHook(({ key }) => useAutomaticTeachingRead(key, 6, progress), { initialProps: { key: WeekKey } })
    await advance(60_000)
    rerender({ key: 'diaspora:2026-09-05' })
    await advance(ThresholdMs - 1)
    expect(progress.markRead).not.toHaveBeenCalled()
    await advance(1)
    expect(progress.markRead).toHaveBeenCalledExactlyOnceWith('diaspora:2026-09-05')
    rerender({ key: 'diaspora:2026-08-29' })
    unmount()
    await advance(600_000)
    expect(progress.markRead).toHaveBeenCalledTimes(1)
  })

  it.each(['unknown', 'offline', 'pending'] as const)('waits to save when progress is %s, then saves accumulated reading time once eligible', async (condition) => {
    const progress = createProgress({ keys: condition === 'unknown' ? null : new Set(), offline: condition === 'offline', pendingKey: condition === 'pending' ? WeekKey : null })
    const { rerender } = renderHook(({ value }) => useAutomaticTeachingRead(WeekKey, 6, value), { initialProps: { value: progress } })
    await advance(ThresholdMs)
    expect(progress.markRead).not.toHaveBeenCalled()
    rerender({ value: { ...progress, keys: new Set(), offline: false, pendingKey: null } })
    expect(progress.markRead).toHaveBeenCalledExactlyOnceWith(WeekKey)
  })

  it('waits for a visible tab if progress finishes loading after the threshold', async () => {
    const progress = createProgress({ keys: null })
    const { rerender } = renderHook(({ value }) => useAutomaticTeachingRead(WeekKey, 6, value), { initialProps: { value: progress } })
    await advance(ThresholdMs)
    setVisibility('hidden')
    rerender({ value: { ...progress, keys: new Set() } })
    expect(progress.markRead).not.toHaveBeenCalled()
    setVisibility('visible')
    expect(progress.markRead).toHaveBeenCalledExactlyOnceWith(WeekKey)
  })

  it('does not resave an already-read teaching', async () => {
    const progress = createProgress({ keys: new Set([WeekKey]) })
    renderHook(() => useAutomaticTeachingRead(WeekKey, 6, progress))
    await advance(600_000)
    expect(progress.markRead).not.toHaveBeenCalled()
  })

  it('does not race a manual choice or override it after another render', async () => {
    const progress = createProgress()
    const { result, rerender } = renderHook(({ value }) => useAutomaticTeachingRead(WeekKey, 6, value), { initialProps: { value: progress } })
    await advance(60_000)
    act(() => { result.current() })
    rerender({ value: { ...progress, pendingKey: WeekKey } })
    rerender({ value: { ...progress, keys: new Set([WeekKey]) } })
    rerender({ value: { ...progress, keys: new Set() } })
    setVisibility('hidden')
    setVisibility('visible')
    await advance(600_000)
    expect(progress.markRead).not.toHaveBeenCalled()
  })

  it.each([null, 0, -1, Number.NaN, Number.POSITIVE_INFINITY])('does not start a timer for invalid reading time %s', async (minutes) => {
    const progress = createProgress()
    renderHook(() => useAutomaticTeachingRead(WeekKey, minutes, progress))
    await advance(600_000)
    expect(progress.markRead).not.toHaveBeenCalled()
    expect(vi.getTimerCount()).toBe(0)
  })
})

function createProgress(overrides: Partial<TeachingReadProgress> = {}): TeachingReadProgress {
  return { keys: new Set(), error: null, pendingKey: null, offline: false, toggle: vi.fn().mockResolvedValue(undefined), markRead: vi.fn().mockResolvedValue(undefined), retry: vi.fn(), ...overrides }
}

async function advance(milliseconds: number) {
  await act(async () => { await vi.advanceTimersByTimeAsync(milliseconds) })
}

function setVisibility(value: DocumentVisibilityState) {
  visibility = value
  fireEvent(document, new Event('visibilitychange'))
}
