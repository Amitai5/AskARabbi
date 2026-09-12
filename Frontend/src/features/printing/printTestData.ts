import type { ConversationMessage, ConversationSource } from '../conversations/conversationData.ts'
import type { WeeklyDvarTorahArticle } from '../dvarTorah/dvarTorahTypes.ts'
import { collectPrintAnswers, type PrintRequest } from './printTypes.ts'

export const PrintSourceFixture: ConversationSource = {
  number: 1, title: 'Deuteronomy', hebrewTitle: 'דברים', canonicalReference: 'Deuteronomy 30:19',
  edition: 'JPS 1917', language: 'English', collection: 'Torah', license: 'Public Domain',
  sourceUrl: 'https://example.test/torah', attributionUrl: 'https://example.test/edition',
  quotations: ['Choose life, that thou mayest live.'], context: 'Surrounding passage for further study.', isExcerpt: true,
}
export const PrintMessages: ConversationMessage[] = [
  { id: 'q1', role: 'User', content: 'What does choosing life mean?', createdAtUtc: '2026-09-11T12:00:00Z' },
  { id: 'a1', role: 'Assistant', content: 'Our choices matter [1].', sources: [PrintSourceFixture], createdAtUtc: '2026-09-11T12:01:00Z' },
  { id: 'q2', role: 'User', content: 'How can we put this into practice?', createdAtUtc: '2026-09-11T12:02:00Z' },
  { id: 'a2', role: 'Assistant', content: 'Make room for kindness [1].', sources: [PrintSourceFixture], createdAtUtc: '2026-09-11T12:03:00Z' },
]
export const PrintAnswers: Extract<PrintRequest, { kind: 'answers' }> = { kind: 'answers', title: 'Choosing life together', answers: collectPrintAnswers(PrintMessages) }
export const PrintTeaching: WeeklyDvarTorahArticle = {
  week: { weekKey: 'diaspora:2026-09-05', shabbatDate: '2026-09-05', hebrewDate: '23 Elul, 5786', parashah: 'Nitzavim', holiday: null, inIsrael: false },
  title: 'Choosing Life', body: 'God\u0019s invitation to choose life [TA].\n\n**Study together.**\n\n- Listen\n- Reflect',
  centralTeaching: 'Choose life [TA].', tags: ['community'], torahGroundingPercent: 100,
  sources: [{ sourceId: 'TA', kind: 'Torah', title: 'Deuteronomy', publisher: 'JPS 1917', sourceUrl: 'https://example.test/torah', excerpt: 'Choose life, that thou mayest live.', retrievedAtUtc: '2026-09-01T12:00:00Z', canonicalReference: 'Deuteronomy 30:19', publishedAtUtc: null, license: 'Public Domain' }],
  generatedAtUtc: '2026-09-01T12:00:00Z', publishedAtUtc: '2026-09-01T12:00:00Z',
}
