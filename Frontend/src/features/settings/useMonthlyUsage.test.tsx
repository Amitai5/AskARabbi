import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createDemoApplicationClients } from '../../test/demoApplicationClients.ts'
import type { UsageSummary } from './settingsTypes.ts'
import { useMonthlyUsage } from './useMonthlyUsage.ts'

const Full: UsageSummary = {
  periodStartUtc: '2026-09-01T00:00:00Z', periodEndUtc: '2026-10-01T00:00:00Z',
  tokensUsed: 5_000_000, tokenLimit: 5_000_000, tokensRemaining: 0, usedPercent: 100, isLimitReached: true,
}

describe('Usage synchronization', () => {
  afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks() })

  it('does not fetch or stay loading when mounted offline and refreshes after reconnecting', async () => {
    const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    const client = createDemoApplicationClients().conversationSettingsClient
    client.getUsage = vi.fn().mockResolvedValue(Full)
    const { result } = renderHook(() => useMonthlyUsage(client, 'reader'))
    await act(async () => {
      window.dispatchEvent(new Event('focus'))
      await result.current.refresh()
    })
    expect(client.getUsage).not.toHaveBeenCalled()
    expect(result.current.isLoading).toBe(false)
    expect(result.current.usage).toBeNull()

    await act(async () => {
      online.mockReturnValue(true)
      window.dispatchEvent(new Event('online'))
    })

    expect(client.getUsage).toHaveBeenCalledTimes(1)
    expect(result.current.usage).toEqual(Full)
    expect(result.current.isLoading).toBe(false)
  })

  it('refreshes an exhausted allowance at the UTC month boundary without a reload', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-09-30T23:59:59.900Z'))
    const client = createDemoApplicationClients().conversationSettingsClient
    const reset: UsageSummary = { ...Full, periodStartUtc: Full.periodEndUtc, periodEndUtc: '2026-11-01T00:00:00Z', tokensUsed: 0, tokensRemaining: 5_000_000, usedPercent: 0, isLimitReached: false }
    client.getUsage = vi.fn().mockResolvedValueOnce(Full).mockResolvedValue(reset)
    const { result } = renderHook(() => useMonthlyUsage(client, 'reader'))
    await act(async () => {})
    expect(result.current.usage?.isLimitReached).toBe(true)

    await act(async () => vi.advanceTimersByTimeAsync(201))

    expect(result.current.usage).toEqual(reset)
    expect(client.getUsage).toHaveBeenCalledTimes(2)
  })

  it('leaves unknown usage unavailable and recovers after an explicit retry', async () => {
    const client = createDemoApplicationClients().conversationSettingsClient
    client.getUsage = vi.fn().mockRejectedValueOnce(new Error('Temporarily unavailable')).mockResolvedValue(Full)
    const { result } = renderHook(() => useMonthlyUsage(client, 'reader'))
    await act(async () => {})
    expect(result.current.usage).toBeNull()
    expect(result.current.error).toBe('Temporarily unavailable')

    await act(async () => result.current.refresh())

    expect(result.current.usage).toEqual(Full)
    expect(result.current.error).toBeNull()
    expect(result.current.isLoading).toBe(false)
  })

  it('refreshes the changed allowance when a mobile app becomes visible without a focus event', async () => {
    const visibility = vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('visible')
    const client = createDemoApplicationClients().conversationSettingsClient
    const oldLimit: UsageSummary = { ...Full, tokenLimit: 10_000_000, tokensUsed: 250_000, tokensRemaining: 9_750_000, usedPercent: 2.5, isLimitReached: false }
    const reducedLimit: UsageSummary = { ...oldLimit, tokenLimit: 5_000_000, tokensRemaining: 4_750_000, usedPercent: 5 }
    client.getUsage = vi.fn().mockResolvedValueOnce(oldLimit).mockResolvedValue(reducedLimit)
    const { result, unmount } = renderHook(() => useMonthlyUsage(client, 'reader'))
    await act(async () => {})

    await act(async () => {
      visibility.mockReturnValue('hidden')
      document.dispatchEvent(new Event('visibilitychange'))
    })
    expect(client.getUsage).toHaveBeenCalledTimes(1)
    await act(async () => {
      visibility.mockReturnValue('visible')
      document.dispatchEvent(new Event('visibilitychange'))
    })

    expect(client.getUsage).toHaveBeenCalledTimes(2)
    expect(result.current.usage).toEqual(reducedLimit)
    unmount()
    document.dispatchEvent(new Event('visibilitychange'))
    expect(client.getUsage).toHaveBeenCalledTimes(2)
  })

  it('does not let a delayed refresh hide tokens returned by a completed turn', async () => {
    const client = createDemoApplicationClients().conversationSettingsClient
    const before: UsageSummary = { ...Full, tokensUsed: 247_022, tokensRemaining: 4_752_978, usedPercent: 4.94044, isLimitReached: false }
    const after: UsageSummary = { ...before, tokensUsed: 300_412, tokensRemaining: 4_699_588, usedPercent: 6.00824 }
    let resolveRefresh: (value: UsageSummary) => void = () => { throw new Error('Refresh was not started') }
    client.getUsage = vi.fn().mockResolvedValueOnce(before).mockImplementation(() => new Promise<UsageSummary>(resolve => { resolveRefresh = resolve }))
    const { result } = renderHook(() => useMonthlyUsage(client, 'reader'))
    await act(async () => {})

    act(() => { void result.current.refresh() })
    act(() => result.current.update(after))
    await act(async () => resolveRefresh(before))

    expect(result.current.usage).toEqual(after)
    expect(result.current.isLoading).toBe(false)
  })
})
