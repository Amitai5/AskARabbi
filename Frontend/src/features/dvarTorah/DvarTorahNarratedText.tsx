import { memo, useMemo, type KeyboardEvent, type ReactNode } from 'react'
import type { DvarTorahAudioWord } from './dvarTorahTypes.ts'

interface DvarTorahNarratedTextProps {
  text: string
  textOffset: number
  activeWord: DvarTorahAudioWord | null
  words?: readonly DvarTorahAudioWord[]
  onSelectWord?(word: DvarTorahAudioWord): void
  sourceNumbersById: ReadonlyMap<string, number>
  selectedSourceNumber: number | null
  onSelectSource(sourceNumber: number, trigger: HTMLButtonElement): void
}

export const DvarTorahNarratedText = memo(function DvarTorahNarratedText({ text, textOffset, activeWord, words, onSelectWord, sourceNumbersById, selectedSourceNumber, onSelectSource }: DvarTorahNarratedTextProps) {
  const parts: ReactNode[] = []
  let offset = 0
  for (const match of text.matchAll(/\[([A-Za-z][A-Za-z0-9_-]*)\]/g)) {
    const sourceNumber = sourceNumbersById.get(match[1])
    if (sourceNumber === undefined) {
      continue
    }
    parts.push(<HighlightedText key={`text-${offset}`} text={text.slice(offset, match.index)} textOffset={textOffset + offset} activeWord={activeWord} words={words} onSelectWord={onSelectWord} />)
    const isSelected = sourceNumber === selectedSourceNumber
    parts.push(
      <button key={`source-${match.index}`} type="button" aria-label={`View source ${sourceNumber}`} aria-expanded={isSelected} onClick={(event) => { if (event.detail === 0 || window.getSelection()?.isCollapsed !== false) { onSelectSource(sourceNumber, event.currentTarget) } }} className={`relative mx-0.5 inline-flex rounded-sm px-0.5 font-semibold text-pomegranate underline decoration-pomegranate/35 underline-offset-4 transition hover:bg-pomegranate/8 hover:decoration-pomegranate ${isSelected ? 'bg-pomegranate/10 ring-1 ring-pomegranate/45' : ''}`}>
        [{sourceNumber}]
      </button>,
    )
    offset = match.index + match[0].length
  }
  parts.push(<HighlightedText key={`text-${offset}`} text={text.slice(offset)} textOffset={textOffset + offset} activeWord={activeWord} words={words} onSelectWord={onSelectWord} />)
  return parts
})

const NoWords: readonly DvarTorahAudioWord[] = []

interface HighlightedTextProps {
  text: string
  textOffset?: number
  activeWord: DvarTorahAudioWord | null
  words?: readonly DvarTorahAudioWord[]
  onSelectWord?(word: DvarTorahAudioWord): void
}

export const HighlightedText = memo(function HighlightedText({ text, textOffset = 0, activeWord, words = NoWords, onSelectWord }: HighlightedTextProps) {
  const visibleWords = useMemo(() => words.filter((word) => word.textOffset >= textOffset && word.textOffset + word.textLength <= textOffset + text.length), [text.length, textOffset, words])

  if (onSelectWord !== undefined && visibleWords.length > 0) {
    const parts: ReactNode[] = []
    let offset = 0
    visibleWords.forEach((word, index) => {
      const start = word.textOffset - textOffset
      parts.push(text.slice(offset, start))
      parts.push(
        <span key={word.textOffset} role="button" data-narration-seek tabIndex={index === 0 ? 0 : -1} title={`Listen from “${word.text}”`} onKeyDown={(event) => {
          if (!event.shiftKey && !event.ctrlKey && !event.metaKey && !event.altKey && (event.key === 'Enter' || event.key === ' ')) {
            event.preventDefault()
            onSelectWord(word)
          } else {
            moveWordFocus(event, index)
          }
        }} onClick={(event) => {
          // Dragging across the teaching should still select text, not unexpectedly start audio.
          if (event.detail === 0 || window.getSelection()?.isCollapsed !== false) {
            onSelectWord(word)
          }
        }} data-narration-word={activeWord === word ? true : undefined} className={`inline cursor-pointer select-text rounded-sm text-inherit transition-colors hover:bg-brass/20 focus-visible:bg-brass/20 ${activeWord === word ? 'bg-brass/30 text-ink shadow-[0_0_0_2px_var(--color-brass)]' : ''}`}>
          {word.text}
        </span>,
      )
      offset = start + word.textLength
    })
    parts.push(text.slice(offset))
    return <span>{parts}</span>
  }

  if (activeWord === null || activeWord.textOffset < textOffset || activeWord.textOffset + activeWord.textLength > textOffset + text.length) {
    return text
  }
  const start = activeWord.textOffset - textOffset
  return <>{text.slice(0, start)}<mark data-narration-word className="rounded-sm bg-brass/30 text-ink shadow-[0_0_0_2px_var(--color-brass)]">{text.slice(start, start + activeWord.textLength)}</mark>{text.slice(start + activeWord.textLength)}</>
})

function moveWordFocus(event: KeyboardEvent<HTMLElement>, index: number) {
  if (event.shiftKey || event.ctrlKey || event.metaKey || event.altKey) { return }
  if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {
    return
  }
  const buttons = event.currentTarget.parentElement?.querySelectorAll<HTMLElement>('[data-narration-seek]')
  if (buttons === undefined || buttons.length === 0) {
    return
  }
  event.preventDefault()
  const next = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : index + (event.key === 'ArrowRight' ? 1 : -1)
  const nextWord = buttons[Math.max(0, Math.min(next, buttons.length - 1))]
  // Change tab stops during keyboard navigation, never during a native text-selection drag.
  buttons.forEach(word => { word.tabIndex = word === nextWord ? 0 : -1 })
  nextWord?.focus()
}
