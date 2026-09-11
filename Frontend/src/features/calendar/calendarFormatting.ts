import type { CalendarEvent } from './calendarTypes.ts'

export function formatCivilDate(value: string, options: Intl.DateTimeFormatOptions = { month: 'long', day: 'numeric', year: 'numeric' }): string {
  // Date-only values aren't instants. Always format in the same zone they are constructed in.
  return new Intl.DateTimeFormat('en-US', { ...options, timeZone: 'UTC' }).format(new Date(`${value}T12:00:00Z`))
}

export function formatEventRange(event: CalendarEvent): string {
  return event.startDate === event.endDate ? formatCivilDate(event.startDate) : `${formatCivilDate(event.startDate, { month: 'short', day: 'numeric', year: 'numeric' })} – ${formatCivilDate(event.endDate, { month: 'short', day: 'numeric', year: 'numeric' })}`
}

export function formatBeginning(event: CalendarEvent): string {
  const date = formatCivilDate(event.beginningDate, { month: 'long', day: 'numeric' })
  switch (event.beginningRule) {
    case 'previousSunset': return `Begins at sunset on ${date}`
    case 'sameEvening': return `First candle evening: ${date}; lighting customs vary`
    case 'nightfall': return `Begins after nightfall on ${date}`
    case 'dawn': return `Fast begins at dawn on ${date}`
    case 'civilDate': return `Observed on ${date}; timing and customs vary by community`
  }
}
