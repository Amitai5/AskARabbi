export interface CalendarLocation {
  kind: 'city' | 'zip'
  id: string
  label: string
  timeZone: string
  defaultCandleLightingMinutes: number
}

export interface CalendarPreferences {
  location: CalendarLocation | null
  inIsrael: boolean
  showLocalTimes: boolean
  candleLightingMinutes: number | null
  havdalah: 'nightfall' | 'fixed'
  havdalahMinutes: number
  majorHolidays: boolean
  minorHolidays: boolean
  fastDays: boolean
  roshChodesh: boolean
  specialShabbatot: boolean
  modernObservances: boolean
}

export interface CalendarPreferencesResponse {
  preferences: CalendarPreferences
  cities: CalendarLocation[]
}

export interface CalendarEvent {
  id: string
  kind: string
  title: string
  category: string
  startDate: string
  endDate: string
  beginningDate: string
  beginningRule: 'previousSunset' | 'sameEvening' | 'nightfall' | 'dawn' | 'civilDate'
  explanation: string
  sourceUrl: string
  isOngoing: boolean
  occurrences: { title: string; date: string }[]
}

export interface CalendarAvailability {
  isAvailable: boolean
  isStale: boolean
  fetchedAtUtc: string | null
  message: string | null
}

export interface CalendarOverview {
  preferences: CalendarPreferences
  today: { gregorianDate: string; hebrewDate: string; hebrewScript: string; timeZone: string; isAfterSunset: boolean; isDaytimeOnly: boolean; solarData?: CalendarAvailability | null }
  shabbat: { shabbatDate: string; parashah: string | null; holiday: string | null; hebrewDate: string; inIsrael: boolean; calculationNote: string }
  events: CalendarEvent[]
  highlight: CalendarEvent | null
  holidays: CalendarAvailability
  localTimes: { title: string; date: string; at: string; timeZone: string; location: string; context: string | null }[]
  timing: CalendarAvailability
  timingConvention: string
  generatedAtUtc: string
  nextRefreshAtUtc: string
}

export const CalendarCategories = [
  ['majorHolidays', 'Major holidays'], ['minorHolidays', 'Minor holidays'],
  ['fastDays', 'Fast days'], ['roshChodesh', 'Rosh Chodesh'],
  ['specialShabbatot', 'Special Shabbatot'], ['modernObservances', 'Modern observances'],
] as const
export type CalendarCategorySetting = typeof CalendarCategories[number][0]
export type CalendarRange = 90 | 180 | 360
export type CalendarFilters = Record<CalendarCategorySetting, boolean>
export const AllCalendarFilters: CalendarFilters = { majorHolidays: true, minorHolidays: true, fastDays: true, roshChodesh: true, specialShabbatot: true, modernObservances: true }
