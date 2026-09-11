import { afterEach, describe, expect, it, vi } from 'vitest'
import { canPreloadLearning, scheduleIdlePreload } from './backgroundConnection.ts'

describe('background learning connection policy', () => {
  afterEach(() => { vi.restoreAllMocks(); vi.useRealTimers(); Reflect.deleteProperty(navigator, 'connection') })
  it.each([
    [undefined, false], [{ effectiveType: '4g', downlink: 10, rtt: 50 }, true],
    [{ effectiveType: '4g', saveData: true }, false], [{ effectiveType: '3g' }, false],
    [{ effectiveType: '2g' }, false], [{ effectiveType: '4g', downlink: 1 }, false],
    [{ effectiveType: '4g', rtt: 500 }, false], [{ effectiveType: '4g', downlink: 1.5, rtt: 300 }, true],
  ])('allows only explicitly good, non-data-saving connections: %j', (connection, expected) => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('visible')
    Object.defineProperty(navigator, 'connection', { configurable: true, value: connection })
    expect(canPreloadLearning()).toBe(expected)
  })
  it('rejects offline or hidden pages even with good connection information', () => {
    Object.defineProperty(navigator, 'connection', { configurable: true, value: { effectiveType: '4g' } })
    const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    expect(canPreloadLearning()).toBe(false)
    online.mockReturnValue(true)
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden')
    expect(canPreloadLearning()).toBe(false)
  })
  it('defers work and cancels the timer fallback', () => {
    vi.useFakeTimers()
    const work = vi.fn()
    const cancel = scheduleIdlePreload(work)
    expect(work).not.toHaveBeenCalled()
    cancel()
    vi.runAllTimers()
    expect(work).not.toHaveBeenCalled()
    scheduleIdlePreload(work)
    vi.advanceTimersByTime(2500)
    expect(work).toHaveBeenCalledOnce()
  })
})
