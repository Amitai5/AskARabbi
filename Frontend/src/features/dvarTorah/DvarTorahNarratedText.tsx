import { memo, type ReactNode } from 'react'
import type { DvarTorahAudioWord } from './dvarTorahTypes.ts'

interface DvarTorahNarratedTextProps {
  text: string
  textOffset: number
  activeWord: DvarTorahAudioWord | null
  sourceNumbersById: ReadonlyMap<string, number>
  selectedSourceNumber: number | null
  onSelectSource(sourceNumber: number, trigger: HTMLButtonElement): void
}

export const DvarTorahNarratedText = memo(function DvarTorahNarratedText({ text, textOffset, activeWord, sourceNumbersById, selectedSourceNumber, onSelectSource }: DvarTorahNarratedTextProps) {
  const parts: ReactNode[] = []
  let offset = 0
  for (const match of text.matchAll(/\[([A-Za-z][A-Za-z0-9_-]*)\]/g)) {
    const sourceNumber = sourceNumbersById.get(match[1])
    if (sourceNumber === undefined) {
      continue
    }
    parts.push(<HighlightedText key={`text-${offset}`} text={text.slice(offset, match.index)} textOffset={textOffset + offset} activeWord={activeWord} />)
    const isSelected = sourceNumber === selectedSourceNumber
    parts.push(
      <button key={`source-${match.index}`} type="button" aria-label={`View source ${sourceNumber}`} aria-expanded={isSelected} onClick={(event) => onSelectSource(sourceNumber, event.currentTarget)} className={`relative mx-0.5 inline-flex rounded-sm px-0.5 font-semibold text-pomegranate underline decoration-pomegranate/35 underline-offset-4 transition hover:bg-pomegranate/8 hover:decoration-pomegranate ${isSelected ? 'bg-pomegranate/10 ring-1 ring-pomegranate/45' : ''}`}>
        [{sourceNumber}]
      </button>,
    )
    offset = match.index + match[0].length
  }
  parts.push(<HighlightedText key={`text-${offset}`} text={text.slice(offset)} textOffset={textOffset + offset} activeWord={activeWord} />)
  return parts
})

export const HighlightedText = memo(function HighlightedText({ text, textOffset = 0, activeWord }: { text: string; textOffset?: number; activeWord: DvarTorahAudioWord | null }) {
  if (activeWord === null || activeWord.textOffset < textOffset || activeWord.textOffset + activeWord.textLength > textOffset + text.length) {
    return text
  }
  const start = activeWord.textOffset - textOffset
  return <>{text.slice(0, start)}<mark className="rounded-sm bg-brass/30 text-ink shadow-[0_0_0_2px_var(--color-brass)]">{text.slice(start, start + activeWord.textLength)}</mark>{text.slice(start + activeWord.textLength)}</>
})
