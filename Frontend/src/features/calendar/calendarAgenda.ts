import { CalendarRanges, type CalendarEvent, type CalendarFilters, type CalendarRange } from './calendarTypes.ts'

const CategoryKeys = { major: 'majorHolidays', minor: 'minorHolidays', fast: 'fastDays', roshChodesh: 'roshChodesh', specialShabbat: 'specialShabbatot', modern: 'modernObservances' } as const

export function addCivilDays(date: string, days: number) {
  const value = new Date(`${date}T12:00:00Z`)
  value.setUTCDate(value.getUTCDate() + days)
  return value.toISOString().slice(0, 10)
}

export function selectCalendarEvents(events: CalendarEvent[], startDate: string, days: CalendarRange, filters?: CalendarFilters) {
  const endDate = addCivilDays(startDate, days - 1)
  return events.filter(event => {
    const category = CategoryKeys[event.category as keyof typeof CategoryKeys]
    return event.endDate >= startDate && event.beginningDate <= endDate && (!filters || !category || filters[category])
  })
}

export function selectHolidayAgenda(events: CalendarEvent[], startDate: string, days: CalendarRange, filters: CalendarFilters, query: string) {
  const normalizedQuery = normalizeSearchText(query)
  if (!normalizedQuery) {
    return { events: selectCalendarEvents(events, startDate, days, filters), days, minimumDays: 90 as CalendarRange, isSearching: false }
  }
  const terms = normalizedQuery.split(' ')
  const matches = selectCalendarEvents(events, startDate, 360, filters).filter(event => {
    const text = normalizeSearchText([event.title, event.explanation, ...event.occurrences.map(occurrence => occurrence.title)].join(' '))
    return terms.every(term => text.includes(term))
  })
  // Include every match, not just the first holiday that happens to fit the current range.
  const minimumDays = CalendarRanges.find(range => selectCalendarEvents(matches, startDate, range).length === matches.length) ?? 360
  return { events: matches, days: Math.max(days, minimumDays) as CalendarRange, minimumDays, isSearching: true }
}

function normalizeSearchText(value: string) {
  return value.normalize('NFKD').replace(/\p{M}/gu, '').toLocaleLowerCase().replace(/['’״׳]/g, '').replace(/[^\p{L}\p{N}]+/gu, ' ').trim()
}
