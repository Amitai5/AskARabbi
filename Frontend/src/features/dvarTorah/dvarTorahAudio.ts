import type { DvarTorahAudioTimings, DvarTorahAudioWord } from './dvarTorahTypes.ts'

export function validateAudioTimings(value: unknown, version: string, title: string, body: string): DvarTorahAudioTimings | null {
  if (!isRecord(value) || value.schemaVersion !== 1 || value.version !== version || value.title !== title || value.body !== body
    || !isNonNegativeNumber(value.durationMs) || value.durationMs === 0 || !Array.isArray(value.words) || value.words.length === 0 || value.words.length > 30_000) {
    return null
  }

  let previousAudioOffset = -1
  const previousTextEnd = { title: 0, body: 0 }
  for (const word of value.words) {
    if (!isRecord(word) || word.section !== 'title' && word.section !== 'body' || typeof word.text !== 'string' || word.text.length === 0
      || !Number.isSafeInteger(word.textOffset) || !Number.isSafeInteger(word.textLength)
      || !isNonNegativeNumber(word.audioOffsetMs) || !isNonNegativeNumber(word.durationMs)) {
      return null
    }

    const text = word.section === 'title' ? title : body
    const start = word.textOffset as number
    const length = word.textLength as number
    if (start < previousTextEnd[word.section] || length <= 0 || text.slice(start, start + length) !== word.text
      || start + length > text.length || word.audioOffsetMs < previousAudioOffset
      || word.audioOffsetMs + word.durationMs > value.durationMs + 100) {
      return null
    }

    previousAudioOffset = word.audioOffsetMs
    previousTextEnd[word.section] = start + length
  }

  return value as unknown as DvarTorahAudioTimings
}

export function findAudioWord(words: readonly DvarTorahAudioWord[], timeMs: number): DvarTorahAudioWord | null {
  let low = 0
  let high = words.length - 1
  let index = -1
  while (low <= high) {
    const middle = Math.floor((low + high) / 2)
    if (words[middle].audioOffsetMs <= timeMs) {
      index = middle
      low = middle + 1
    } else {
      high = middle - 1
    }
  }

  const word = words[index]
  if (word === undefined) {
    return null
  }

  // Some providers report a zero-length boundary; the next boundary still gives a safe interval.
  const end = word.durationMs > 0 ? word.audioOffsetMs + word.durationMs : words[index + 1]?.audioOffsetMs ?? word.audioOffsetMs + 200
  return timeMs < end ? word : null
}

export function formatAudioTime(seconds: number) {
  const wholeSeconds = Math.max(0, Math.floor(Number.isFinite(seconds) ? seconds : 0))
  return `${Math.floor(wholeSeconds / 60)}:${String(wholeSeconds % 60).padStart(2, '0')}`
}

export function createNarratedParagraphs(body: string) {
  const paragraphs: { text: string; textOffset: number }[] = []
  let textOffset = 0
  const parts = body.split(/(\r?\n\s*\r?\n)/)
  parts.forEach((text, index) => {
    if (index % 2 === 0 && text.trim().length > 0) {
      paragraphs.push({ text, textOffset })
    }
    textOffset += text.length
  })
  return paragraphs
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object'
}

function isNonNegativeNumber(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0
}
