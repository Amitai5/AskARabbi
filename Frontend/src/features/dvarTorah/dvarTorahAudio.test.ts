import { describe, expect, it } from 'vitest'
import { createNarratedParagraphs, findAudioWord, formatAudioTime, validateAudioTimings } from './dvarTorahAudio.ts'
import type { DvarTorahAudioTimings } from './dvarTorahTypes.ts'

const Timings: DvarTorahAudioTimings = {
  schemaVersion: 1, version: 'v1', title: 'A teaching', body: 'שלום [T1].\r\n\r\nLearn together.', durationMs: 5000,
  words: [
    { section: 'title', text: 'A', textOffset: 0, textLength: 1, audioOffsetMs: 0, durationMs: 150 },
    { section: 'body', text: 'שלום', textOffset: 0, textLength: 4, audioOffsetMs: 1000, durationMs: 500 },
    { section: 'body', text: 'Learn', textOffset: 14, textLength: 5, audioOffsetMs: 2000, durationMs: 500 },
  ],
}

describe('validateAudioTimings', () => {
  it('accepts exact normalized UTF16 text with Hebrew, citation gaps, and line breaks', () => {
    expect(validateAudioTimings(Timings, 'v1', Timings.title, Timings.body)).toBe(Timings)
  })

  it.each([
    null, {}, { ...Timings, schemaVersion: 2 }, { ...Timings, version: 'old' },
    { ...Timings, title: 'Changed' }, { ...Timings, body: 'Changed' }, { ...Timings, durationMs: -1 },
    { ...Timings, words: [] }, { ...Timings, words: [{ ...Timings.words[0], text: 'Wrong' }] },
    { ...Timings, words: [{ ...Timings.words[0], textOffset: -1 }] },
    { ...Timings, words: [{ ...Timings.words[0], textLength: 1.5 }] },
    { ...Timings, words: [{ ...Timings.words[0], section: 'other' }] },
    { ...Timings, words: [{ ...Timings.words[0], audioOffsetMs: Number.NaN }] },
    { ...Timings, words: [{ ...Timings.words[0], durationMs: 6000 }] },
    { ...Timings, words: [...Timings.words].reverse() },
    { ...Timings, words: [Timings.words[1], Timings.words[1]] },
  ])('disables highlighting for invalid or stale metadata %#', (value) => {
    expect(validateAudioTimings(value, 'v1', Timings.title, Timings.body)).toBeNull()
  })
})

describe('findAudioWord', () => {
  it.each([[-1, null], [0, 'A'], [149, 'A'], [150, null], [1100, 'שלום'], [2200, 'Learn'], [2600, null]])('selects the current word at %i ms', (time, text) => {
    expect(findAudioWord(Timings.words, time)?.text ?? null).toBe(text)
  })

  it('uses the next boundary for a zero-duration word and supports backward seeking', () => {
    const words = [{ ...Timings.words[1], durationMs: 0 }, Timings.words[2]]
    expect(findAudioWord(words, 2100)?.text).toBe('Learn')
    expect(findAudioWord(words, 1800)?.text).toBe('שלום')
    expect(findAudioWord([], 0)).toBeNull()
  })
})

describe('narration display helpers', () => {
  it('preserves paragraph offsets into the original normalized body', () => {
    expect(createNarratedParagraphs(Timings.body)).toEqual([
      { text: 'שלום [T1].', textOffset: 0 }, { text: 'Learn together.', textOffset: 14 },
    ])
  })

  it.each([[0, '0:00'], [61.9, '1:01'], [3661, '61:01'], [-1, '0:00'], [Number.NaN, '0:00']])('formats audio time %s', (value, result) => {
    expect(formatAudioTime(value)).toBe(result)
  })
})
