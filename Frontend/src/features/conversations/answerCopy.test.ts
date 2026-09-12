import { describe, expect, it } from 'vitest'
import { formatAnswerForClipboard } from './answerCopy.ts'
import type { ConversationMessage, ConversationSource } from './conversationData.ts'

const Source: ConversationSource = {
  number: 4,
  title: 'Deuteronomy',
  hebrewTitle: 'דברים',
  canonicalReference: 'Deuteronomy 30:19',
  edition: 'English study edition',
  language: 'en',
  collection: 'Torah',
  license: 'CC-BY',
  sourceUrl: 'https://www.sefaria.org/Deuteronomy.30.19?lang=bi',
  attributionUrl: 'https://www.sefaria.org/texts/Tanakh',
  quotations: ['Choose life—so that you and your offspring would live.'],
  context: 'Surrounding context is not part of the copied quotation.',
  isExcerpt: true,
}
const Message: ConversationMessage = {
  id: 'answer',
  role: 'Assistant',
  content: 'Choose life. [4] Then study the Hebrew. [1] Return to [4].\n\nA second paragraph.',
  createdAtUtc: '2026-09-01T12:00:00Z',
  sources: [Source, {
    ...Source,
    number: 1,
    edition: 'Hebrew edition',
    language: 'he',
    license: 'CC-BY-SA',
    sourceUrl: 'https://www.sefaria.org/Deuteronomy.30.19?lang=he',
    quotations: ['וּבָחַרְתָּ בַּחַיִּים', 'A second exact quotation.'],
  }],
}

describe('answer clipboard formatting', () => {
  it('copies only the answer body in text mode, preserving citation numbers and paragraph breaks', () => {
    expect(formatAnswerForClipboard(Message, 'text')).toBe(Message.content)
  })

  it('keeps original numbers, editions, URLs, attribution, and exact quotations in a portable source list', () => {
    expect(formatAnswerForClipboard(Message, 'sources')).toBe(`${Message.content}

Sources

[4] Deuteronomy 30:19
Deuteronomy · דברים
Edition: English study edition
Language: en
Collection: Torah
License: CC-BY
https://www.sefaria.org/Deuteronomy.30.19?lang=bi
Attribution: https://www.sefaria.org/texts/Tanakh
“Choose life—so that you and your offspring would live.”

[1] Deuteronomy 30:19
Deuteronomy · דברים
Edition: Hebrew edition
Language: he
Collection: Torah
License: CC-BY-SA
https://www.sefaria.org/Deuteronomy.30.19?lang=he
Attribution: https://www.sefaria.org/texts/Tanakh
“וּבָחַרְתָּ בַּחַיִּים”
“A second exact quotation.”`)
  })

  it.each([undefined, []])('does not invent a source list for older answers with sources=%s', sources => {
    const message = { ...Message, content: 'An older answer [9].\n\n', sources }
    expect(formatAnswerForClipboard(message, 'sources')).toBe(message.content)
  })

  it('handles a source without optional details or quotations without copying surrounding context', () => {
    const source = { ...Source, title: '', hebrewTitle: '', edition: '', language: '', collection: '', license: '', attributionUrl: '', quotations: [] }
    expect(formatAnswerForClipboard({ ...Message, sources: [source] }, 'sources')).toBe(`${Message.content}\n\nSources\n\n[4] Deuteronomy 30:19\nhttps://www.sefaria.org/Deuteronomy.30.19?lang=bi`)
  })

  it('repairs display typography in the body without changing the stored source snapshot or quotations', () => {
    const quotation = "A witness\u0019s exact wording.\n  שָׁלוֹם"
    const source = { ...Source, quotations: [quotation] }
    const message = { ...Message, content: "A witness\u0019s testimony. [4]", sources: [source] }
    const before = structuredClone(message)

    const result = formatAnswerForClipboard(message, 'sources')

    expect(result).toContain('A witness’s testimony. [4]')
    expect(result).toContain(`“${quotation}”`)
    expect(message).toEqual(before)
  })
})
