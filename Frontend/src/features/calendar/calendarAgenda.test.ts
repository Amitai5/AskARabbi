import { describe, expect, it } from 'vitest'
import { addCivilDays, selectHolidayAgenda } from './calendarAgenda.ts'
import { AllCalendarFilters } from './calendarTypes.ts'
import { RoshHashanah } from './calendarTestData.ts'

const Today = '2026-09-10'
function holidayAt(offset: number) {
  const date = addCivilDays(Today, offset)
  return { ...RoshHashanah, id: `holiday-${offset}`, title: 'A matching holiday', beginningDate: date, startDate: date, endDate: date, occurrences: [] }
}

describe('holiday search range', () => {
  it.each([[0, 90], [89, 90], [90, 180], [179, 180], [180, 360], [359, 360]])('includes an event beginning on day %i in the %i-day range', (offset, expected) => {
    const event = holidayAt(offset)
    const result = selectHolidayAgenda([event], Today, 90, AllCalendarFilters, 'matching')
    expect(result.days).toBe(expected)
    expect(result.events).toEqual([event])
  })

  it('excludes past events and events beyond 360 days, without expanding for no matches', () => {
    const result = selectHolidayAgenda([holidayAt(-1), holidayAt(360)], Today, 90, AllCalendarFilters, 'matching')
    expect(result.events).toEqual([])
    expect(result.days).toBe(90)
  })

  it('includes ongoing holidays and their sunset beginning rather than only the civil start date', () => {
    const ongoing = { ...holidayAt(-1), endDate: Today }
    const sunset = { ...holidayAt(90), beginningDate: addCivilDays(Today, 89) }
    expect(selectHolidayAgenda([ongoing, sunset], Today, 90, AllCalendarFilters, 'matching').days).toBe(90)
    expect(selectHolidayAgenda([ongoing, sunset], Today, 90, AllCalendarFilters, 'matching').events).toEqual([ongoing, sunset])
  })

  it('includes every match and does not narrow an explicitly chosen larger range', () => {
    const events = [holidayAt(10), holidayAt(100), holidayAt(300)]
    expect(selectHolidayAgenda(events, Today, 90, AllCalendarFilters, 'matching').days).toBe(360)
    expect(selectHolidayAgenda(events, Today, 90, AllCalendarFilters, 'matching').events).toEqual(events)
    expect(selectHolidayAgenda([events[0]], Today, 180, AllCalendarFilters, 'matching').days).toBe(180)
  })

  it('matches names, descriptions, and occurrence names with case, accents, and Hebrew marks normalized', () => {
    const event = { ...RoshHashanah, title: 'Rósh Hashanah', occurrences: [{ title: 'רֹאשׁ הַשָּׁנָה', date: RoshHashanah.startDate }] }
    for (const query of ['  ROSH-hashanah ', 'renewal shofar', 'ראש השנה']) {
      expect(selectHolidayAgenda([event], Today, 90, AllCalendarFilters, query).events).toEqual([event])
    }
  })

  it('respects category filters while searching the entire year', () => {
    expect(selectHolidayAgenda([holidayAt(300)], Today, 90, { ...AllCalendarFilters, majorHolidays: false }, 'matching').events).toEqual([])
  })

  it.each(['', '   ', ' — '])('retains normal range filtering for an empty search: %s', query => {
    const near = holidayAt(10)
    const result = selectHolidayAgenda([near, holidayAt(300)], Today, 90, AllCalendarFilters, query)
    expect(result.events).toEqual([near])
    expect(result.isSearching).toBe(false)
  })
})
