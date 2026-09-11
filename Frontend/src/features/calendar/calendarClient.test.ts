import { describe, expect, it, vi } from 'vitest'
import { createBackendCalendarClient } from './calendarClient.ts'
import { DefaultCalendarPreferences, RoshHashanah } from './calendarTestData.ts'
import { formatBeginning, formatCivilDate, formatEventRange } from './calendarFormatting.ts'

describe('calendar transport and date presentation', () => {
  it('uses authenticated API transport, cancellation and no-store for every endpoint', async () => {
    const request = vi.fn().mockResolvedValue({})
    const client = createBackendCalendarClient({ baseUrl: 'https://api.example.test', request })
    const signal = new AbortController().signal
    await client.getOverview(360, signal)
    await client.getPreferences(signal)
    await client.updatePreferences(DefaultCalendarPreferences, signal)
    expect(request.mock.calls).toEqual([
      ['/api/calendar/overview?days=360&includeAllCategories=true', { signal, cache: 'no-store' }],
      ['/api/calendar/preferences', { signal, cache: 'no-store' }],
      ['/api/calendar/preferences', { method: 'PUT', body: JSON.stringify(DefaultCalendarPreferences), signal, cache: 'no-store' }],
    ])
  })
  it('preserves civil dates including DST and leap days regardless of browser timezone', () => {
    expect(formatCivilDate('2026-03-08')).toBe('March 8, 2026')
    expect(formatCivilDate('2028-02-29')).toBe('February 29, 2028')
    expect(formatEventRange(RoshHashanah)).toBe('Sep 12, 2026 – Sep 13, 2026')
  })
  it.each([
    ['previousSunset', 'Begins at sunset'], ['dawn', 'Fast begins at dawn'], ['sameEvening', 'First candle evening'],
    ['nightfall', 'Begins after nightfall'], ['civilDate', 'Observed on'],
  ] as const)('describes the %s beginning without a universal evening rule', (beginningRule, expected) => {
    expect(formatBeginning({ ...RoshHashanah, beginningRule })).toContain(expected)
  })
})
