import { vi } from 'vitest'
import type { CalendarClient } from './calendarClient.ts'
import type { CalendarEvent, CalendarOverview, CalendarPreferences } from './calendarTypes.ts'

export const Jerusalem = { kind: 'city' as const, id: '281184', label: 'Jerusalem, Israel', timeZone: 'Asia/Jerusalem', defaultCandleLightingMinutes: 40 }
export const NewYork = { kind: 'city' as const, id: '5128581', label: 'New York, United States', timeZone: 'America/New_York', defaultCandleLightingMinutes: 18 }
export const DefaultCalendarPreferences: CalendarPreferences = {
  location: null, inIsrael: false, showLocalTimes: true, candleLightingMinutes: null,
  havdalah: 'nightfall', havdalahMinutes: 42, majorHolidays: true, minorHolidays: true,
  fastDays: true, roshChodesh: true, specialShabbatot: true, modernObservances: true,
}
export const RoshHashanah: CalendarEvent = {
  id: 'rosh-hashanah:2026-09-12', kind: 'rosh-hashanah', title: 'Rosh Hashanah', category: 'major',
  startDate: '2026-09-12', endDate: '2026-09-13', beginningDate: '2026-09-11', beginningRule: 'previousSunset',
  explanation: 'The Jewish new year invites reflection, renewal, and the sound of the shofar.',
  sourceUrl: 'https://www.hebcal.com/holidays/rosh-hashana', isOngoing: false,
  occurrences: [{ title: 'Rosh Hashana 5787', date: '2026-09-12' }, { title: 'Rosh Hashana II', date: '2026-09-13' }],
}
export function calendarOverview(overrides: Partial<CalendarOverview> = {}): CalendarOverview {
  return {
    preferences: { ...DefaultCalendarPreferences },
    today: { gregorianDate: '2026-09-10', hebrewDate: '28 Elul, 5786', hebrewScript: 'כ״ח אלול תשפ״ו', timeZone: 'UTC', isAfterSunset: false, isDaytimeOnly: true },
    shabbat: { shabbatDate: '2026-09-12', parashah: null, holiday: 'Rosh Hashanah', hebrewDate: '1 Tishrei, 5787', inIsrael: false, calculationNote: '' },
    events: [{ ...RoshHashanah }], highlight: { ...RoshHashanah },
    holidays: { isAvailable: true, isStale: false, fetchedAtUtc: '2026-09-10T12:00:00Z', message: null },
    localTimes: [], timing: { isAvailable: false, isStale: false, fetchedAtUtc: null, message: 'Choose a location to see local times.' },
    timingConvention: '', generatedAtUtc: '2026-09-10T12:00:00Z', nextRefreshAtUtc: '2099-09-11T00:00:00Z', ...overrides,
  }
}
export function fakeCalendarClient(initial = calendarOverview()): CalendarClient {
  let preferences = initial.preferences
  return {
    getOverview: vi.fn(async () => ({ ...initial, preferences })),
    getPreferences: vi.fn(async () => ({ preferences, cities: [NewYork, Jerusalem] })),
    updatePreferences: vi.fn(async (value) => { preferences = value; return value }),
  }
}
