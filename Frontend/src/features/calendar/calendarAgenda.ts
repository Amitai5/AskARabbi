import type { CalendarEvent, CalendarFilters, CalendarRange } from './calendarTypes.ts'

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
