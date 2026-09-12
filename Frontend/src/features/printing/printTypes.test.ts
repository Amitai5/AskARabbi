import { describe, expect, it } from 'vitest'
import { collectPrintAnswers, safePrintUrl, teachingPrintSources } from './printTypes.ts'
import { PrintMessages, PrintTeaching } from './printTestData.ts'

describe('print snapshots', () => {
  it('pairs completed answers with their real questions and retains source attribution without mutating the conversation', () => {
    const messages = [...PrintMessages, { ...PrintMessages[0], id: 'pending', content: 'Still waiting for an answer' }, { ...PrintMessages[1], id: 'blank', content: '  ' }]
    const answers = collectPrintAnswers(messages)
    expect(answers.map(answer => answer.id)).toEqual(['a1', 'a2'])
    expect(answers.map(answer => answer.question)).toEqual([PrintMessages[0].content, PrintMessages[2].content])
    expect(answers[0].sources[0]).toMatchObject({ key: '1', reference: 'Deuteronomy 30:19', license: 'Public Domain', attributionUrl: 'https://example.test/edition', context: 'Surrounding passage for further study.' })
    expect(messages).toHaveLength(6)
  })

  it('handles standalone answers and teaching source IDs', () => {
    expect(collectPrintAnswers([PrintMessages[1]])[0].question).toBeNull()
    expect(collectPrintAnswers([])).toEqual([])
    expect(teachingPrintSources(PrintTeaching)[0]).toMatchObject({ key: 'TA', number: 1, reference: 'Deuteronomy 30:19', excerpts: ['Choose life, that thou mayest live.'] })
  })

  it.each([undefined, '', 'javascript:alert(1)', 'data:text/html,hello', 'file:///private/file', 'https://name:secret@example.test/', 'not a url'])('does not make unsafe or credential-bearing sources clickable: %s', value => {
    expect(safePrintUrl(value)).toBeNull()
  })

  it.each(['https://example.test/torah#verse', 'http://example.test/?edition=1'])('keeps valid web source URLs: %s', value => {
    expect(safePrintUrl(value)).toBe(value)
  })
})
