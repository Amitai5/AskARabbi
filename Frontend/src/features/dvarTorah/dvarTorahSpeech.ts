import { normalizeDvarTorahText } from './dvarTorahText.ts'

const MaximumSpeechChunkCharacters = 240

export function createSpeechText(title: string, body: string) {
  const normalizedTitle = normalizeDvarTorahText(title).trim()
  const normalizedBody = normalizeDvarTorahText(body)
    .replace(/\[[A-Za-z][A-Za-z0-9_-]*\]/g, '')
    .replace(/\s+/g, ' ')
    .replace(/\s+([,.;:!?])/g, '$1')
    .trim()
  return `${normalizedTitle}. ${normalizedBody}`
}

export function createSpeechChunks(title: string, body: string) {
  const text = createSpeechText(title, body)
  const sentences = text.split(/(?<=[.!?])\s+/)
  const chunks: string[] = []
  let current = ''

  for (const sentence of sentences) {
    const parts = sentence.length <= MaximumSpeechChunkCharacters ? [sentence] : splitLongSpeechPart(sentence, MaximumSpeechChunkCharacters)
    for (const part of parts) {
      if (current.length === 0) {
        current = part
      } else if (current.length + part.length + 1 <= MaximumSpeechChunkCharacters) {
        current += ` ${part}`
      } else {
        chunks.push(current)
        current = part
      }
    }
  }
  if (current.length > 0) {
    chunks.push(current)
  }

  return chunks
}

function splitLongSpeechPart(value: string, maximumCharacters: number) {
  const parts: string[] = []
  let current = ''
  for (const word of value.split(/\s+/)) {
    if (current.length > 0 && current.length + word.length + 1 > maximumCharacters) {
      parts.push(current)
      current = ''
    }
    if (word.length > maximumCharacters) {
      if (current.length > 0) {
        parts.push(current)
        current = ''
      }
      for (let index = 0; index < word.length; index += maximumCharacters) {
        parts.push(word.slice(index, index + maximumCharacters))
      }
      continue
    }

    current = current.length === 0 ? word : `${current} ${word}`
  }
  if (current.length > 0) {
    parts.push(current)
  }

  return parts
}
