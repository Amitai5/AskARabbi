import type { CalendarEvent } from '../calendar/calendarTypes.ts'
import type { ConversationSource } from './conversationData.ts'

export function sourceQuestion(source: ConversationSource) {
  return `Can you explain ${source.canonicalReference} and its context?\nSource: ${source.sourceUrl}`
}

export function holidayQuestion(event: CalendarEvent) {
  return `What is ${event.title} (${event.startDate}${event.endDate !== event.startDate ? ` to ${event.endDate}` : ''}) about, and how is it observed?\nCalendar reference: ${event.sourceUrl}`
}

export function appendLearningQuestion(draft: string, question: string) {
  return draft.trim().length === 0 ? question : draft.trimEnd() === question ? draft : `${draft}\n\n${question}`
}
