import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createDemoApplicationClients } from '../../test/demoApplicationClients.ts'
import type { UsageSummary } from './settingsTypes.ts'
import { useMonthlyUsage } from './useMonthlyUsage.ts'

const Full: UsageSummary = {
  periodStartUtc: '2026-09-01T00:00:00Z', periodEndUtc: '2026-10-01T00:00:00Z',
  tokensUsed: 10_000_000, tokenLimit: 10_000_000, tokensRemaining: 0, usedPercent: 100, isLimitReached: true,
}

describe('Usage synchronization', () => {
  afterEach(() => vi.useRealTimers())

  it('refreshes an exhausted allowance at the UTC month boundary without a reload', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-09-30T23:59:59.900Z'))
    const client = createDemoApplicationClients().conversationSettingsClient
    const reset: UsageSummary = { ...Full, periodStartUtc: Full.periodEndUtc, periodEndUtc: '2026-11-01T00:00:00Z', tokensUsed: 0, tokensRemaining: 10_000_000, usedPercent: 0, isLimitReached: false }
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
})
