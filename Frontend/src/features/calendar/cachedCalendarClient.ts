import { createSharedRequest } from '../../api/sharedRequest.ts'
import type { CalendarClient } from './calendarClient.ts'
import type { CalendarOverview, CalendarRange } from './calendarTypes.ts'
import { selectCalendarEvents } from './calendarAgenda.ts'

// Construct once per signed-in account, never at module scope: overviews contain location data.
export function createCachedCalendarClient(client: CalendarClient): CalendarClient {
  const cache = new Map<CalendarRange, CalendarOverview>()
  const pending = new Map<CalendarRange, ReturnType<typeof createSharedRequest<CalendarOverview>>>()
  let generation = 0

  function forRange(value: CalendarOverview, days: CalendarRange) {
    const events = selectCalendarEvents(value.events, value.today.gregorianDate, days)
    return { ...value, events, highlight: events.find(event => event.isOngoing) ?? events[0] ?? null }
  }
  function getCachedOverview(days: CalendarRange) {
    for (const [range, value] of cache) {
      if (range >= days && Date.parse(value.nextRefreshAtUtc) > Date.now()) { return forRange(value, days) }
    }
    return null
  }
  function invalidate() {
    generation++
    cache.clear()
    for (const request of pending.values()) { request.abort() }
    pending.clear()
  }
  return {
    getCachedOverview,
    invalidate,
    getOverview(days, signal) {
      if (signal?.aborted) { return Promise.reject(signal.reason) }
      const saved = getCachedOverview(days)
      if (saved) { return Promise.resolve(saved) }
      for (const [range, request] of pending) {
        if (range >= days && !request.signal.aborted) { return request.read(signal).then(value => forRange(value, days)) }
      }
      const version = generation
      const request = createSharedRequest(async sharedSignal => {
        try {
          const value = await client.getOverview(days, sharedSignal)
          sharedSignal.throwIfAborted()
          if (generation === version) { cache.set(days, value) }
          return value
        } finally {
          if (pending.get(days) === request) { pending.delete(days) }
        }
      })
      pending.set(days, request)
      return request.read(signal)
    },
    getPreferences: signal => client.getPreferences(signal),
    async updatePreferences(preferences, signal) {
      const saved = await client.updatePreferences(preferences, signal)
      const contextKey = (value: typeof saved) => JSON.stringify([value.location, value.inIsrael, value.showLocalTimes, value.candleLightingMinutes, value.havdalah, value.havdalahMinutes])
      if ([...cache.values()].some(value => contextKey(value.preferences) !== contextKey(saved))) { invalidate(); return saved }
      // Keep the complete event schedule; filtering it never needs another provider request.
      generation++
      for (const request of pending.values()) { request.abort() }
      pending.clear()
      for (const [range, value] of cache) { cache.set(range, { ...value, preferences: saved }) }
      return saved
    },
  }
}
