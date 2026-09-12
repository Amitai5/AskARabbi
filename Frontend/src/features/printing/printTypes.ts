import type { CalendarEvent, CalendarFilters, CalendarOverview, CalendarRange } from '../calendar/calendarTypes.ts'
import type { ConversationMessage, ConversationSource } from '../conversations/conversationData.ts'
import type { WeeklyDvarTorahArticle } from '../dvarTorah/dvarTorahTypes.ts'

export interface PrintSource {
  key: string
  number: number
  title: string
  reference: string
  hebrewTitle?: string
  details: string
  license: string
  url: string
  attributionUrl?: string
  excerpts: readonly string[]
  context?: string
}

export interface PrintAnswer {
  id: string
  question: string | null
  content: string
  sources: readonly PrintSource[]
}

export interface PrintCalendar {
  startDate: string
  days: CalendarRange
  events: CalendarEvent[]
  filters: CalendarFilters
  search: string
  inIsrael: boolean
  overview?: CalendarOverview
  savedNote?: string
}

export type PrintRequest =
  | { kind: 'answers'; title: string; answers: readonly PrintAnswer[]; initialAnswerId?: string }
  | { kind: 'teaching'; article: WeeklyDvarTorahArticle }
  | { kind: 'calendar'; calendar: PrintCalendar }

export interface PrintOptions {
  paper: 'letter' | 'a4'
  textSize: 'standard' | 'large'
  includeQuestions: boolean
  includeExcerpts: boolean
  includeContext: boolean
  includeNotes: boolean
  separateAnswers: boolean
  includeCalendarSummary: boolean
  includeLocalTimes: boolean
}

export const DefaultPrintOptions: PrintOptions = { paper: 'letter', textSize: 'standard', includeQuestions: true, includeExcerpts: true, includeContext: false, includeNotes: false, separateAnswers: false, includeCalendarSummary: true, includeLocalTimes: true }

export function collectPrintAnswers(messages: readonly ConversationMessage[]): PrintAnswer[] {
  let question: string | null = null
  return messages.flatMap(message => {
    if (message.role === 'User') { question = message.content; return [] }
    if (!message.content.trim()) { return [] }
    return [{ id: message.id, question, content: message.content, sources: (message.sources ?? []).map(toPrintSource) }]
  })
}

function toPrintSource(source: ConversationSource): PrintSource {
  return { key: String(source.number), number: source.number, title: source.title, reference: source.canonicalReference, hebrewTitle: source.hebrewTitle, details: [source.edition, source.language, source.collection].filter(Boolean).join(' · '), license: source.license, url: source.sourceUrl, attributionUrl: source.attributionUrl, excerpts: source.quotations, context: source.context }
}

export function teachingPrintSources(article: WeeklyDvarTorahArticle): PrintSource[] {
  return article.sources.map((source, index) => ({ key: source.sourceId, number: index + 1, title: source.title, reference: source.canonicalReference ?? '', details: [source.publisher, source.kind].filter(Boolean).join(' · '), license: source.license ?? 'Source terms apply', url: source.sourceUrl, excerpts: [source.excerpt] }))
}

export function safePrintUrl(value: string | undefined): string | null {
  if (!value) { return null }
  try {
    const url = new URL(value)
    return (url.protocol === 'https:' || url.protocol === 'http:') && !url.username && !url.password ? url.href : null
  } catch { return null }
}
