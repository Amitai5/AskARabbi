import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DvarTorahNarratedText, NarratedText } from './DvarTorahNarratedText.tsx'
import type { DvarTorahAudioWord } from './dvarTorahTypes.ts'

const Text = 'שלום [TA] again again.'
const Words: DvarTorahAudioWord[] = [
  { section: 'body', text: 'שלום', textOffset: 0, textLength: 4, audioOffsetMs: 0, durationMs: 500 },
  { section: 'body', text: 'again', textOffset: 10, textLength: 5, audioOffsetMs: 600, durationMs: 500 },
  { section: 'body', text: 'again', textOffset: 16, textLength: 5, audioOffsetMs: 1200, durationMs: 500 },
]

afterEach(() => window.getSelection()?.removeAllRanges())

describe('DvarTorahNarratedText', () => {
  it('preserves Hebrew, punctuation, and citations while seeking by exact word offset', async () => {
    const user = userEvent.setup()
    const onSelectWord = vi.fn()
    const onSelectSource = vi.fn()
    const { container } = render(<p><DvarTorahNarratedText text={Text} textOffset={0} activeWord={Words[2]} words={Words} onSelectWord={onSelectWord} sourceNumbersById={new Map([['TA', 1]])} selectedSourceNumber={null} onSelectSource={onSelectSource} /></p>)

    expect(container.textContent).toBe('שלום [1] again again.')
    expect(container.querySelector('mark')).toBeNull()
    expect(container.querySelector('[data-narration-word]')).toHaveTextContent('again')
    expect(container.querySelector('[data-narration-word]')).not.toHaveClass('bg-brass/30', 'hover:bg-brass/20')
    await user.click(screen.getAllByRole('button', { name: 'again' })[1])
    expect(onSelectWord).toHaveBeenCalledWith(Words[2])
    await user.click(screen.getByRole('button', { name: 'View source 1' }))
    expect(onSelectSource).toHaveBeenCalledWith(1, expect.any(HTMLButtonElement))
    expect(onSelectWord).toHaveBeenCalledTimes(1)
  })

  it('keeps the spoken text unhighlighted when word seeking is unavailable', () => {
    const { container } = render(<NarratedText text={Text} activeWord={Words[2]} />)

    expect(container.textContent).toBe(Text)
    expect(container.querySelector('mark')).toBeNull()
    expect(container.querySelector('[data-narration-word]')).toHaveTextContent('again')
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('supports arrow-key navigation and Enter without making every word a tab stop', async () => {
    const user = userEvent.setup()
    const onSelectWord = vi.fn()
    render(<DvarTorahNarratedText text={Text} textOffset={0} activeWord={null} words={Words} onSelectWord={onSelectWord} sourceNumbersById={new Map([['TA', 1]])} selectedSourceNumber={null} onSelectSource={vi.fn()} />)
    const repeatedWords = screen.getAllByRole('button', { name: 'again' })
    expect(repeatedWords[0]).toHaveAttribute('tabindex', '0')
    expect(repeatedWords[1]).toHaveAttribute('tabindex', '-1')

    repeatedWords[0].focus()
    await user.keyboard('{ArrowRight}{Enter}')

    expect(repeatedWords[1]).toHaveFocus()
    expect(repeatedWords[0]).toHaveAttribute('tabindex', '-1')
    expect(onSelectWord).toHaveBeenCalledWith(Words[2])
  })

  it('does not start audio while the reader selects text', () => {
    const onSelectWord = vi.fn()
    render(<DvarTorahNarratedText text={Text} textOffset={0} activeWord={null} words={Words} onSelectWord={onSelectWord} sourceNumbersById={new Map()} selectedSourceNumber={null} onSelectSource={vi.fn()} />)
    const word = screen.getByRole('button', { name: 'שלום' })
    const range = document.createRange()
    range.selectNodeContents(word)
    window.getSelection()?.addRange(range)

    fireEvent.click(word, { detail: 1 })

    expect(onSelectWord).not.toHaveBeenCalled()
  })

  it('preserves the native selection as the spoken word advances', () => {
    const props = { text: Text, textOffset: 0, words: Words, onSelectWord: vi.fn(), sourceNumbersById: new Map([['TA', 1]]), selectedSourceNumber: null, onSelectSource: vi.fn() }
    const { rerender } = render(<DvarTorahNarratedText {...props} activeWord={Words[1]} />)
    const words = screen.getAllByRole('button', { name: 'again' })
    const range = document.createRange()
    range.setStart(words[0].firstChild!, 1)
    range.setEnd(words[1].firstChild!, 4)
    window.getSelection()?.addRange(range)
    const selectedText = window.getSelection()?.toString()

    rerender(<DvarTorahNarratedText {...props} activeWord={Words[2]} />)

    expect(window.getSelection()?.toString()).toBe(selectedText)
    expect(selectedText).toBe('gain agai')
  })

  it('keeps text-selection shortcuts available and supports Space to play', async () => {
    const user = userEvent.setup()
    const onSelectWord = vi.fn()
    render(<DvarTorahNarratedText text={Text} textOffset={0} activeWord={null} words={Words} onSelectWord={onSelectWord} sourceNumbersById={new Map([['TA', 1]])} selectedSourceNumber={null} onSelectSource={vi.fn()} />)
    const word = screen.getAllByRole('button', { name: 'again' })[0]
    word.focus()

    expect(fireEvent.keyDown(word, { key: 'ArrowRight', shiftKey: true })).toBe(true)
    expect(word).toHaveFocus()
    await user.keyboard(' ')

    expect(onSelectWord).toHaveBeenCalledExactlyOnceWith(Words[1])
  })

  it('does not open a citation during text selection', () => {
    const onSelectSource = vi.fn()
    const { container } = render(<p><DvarTorahNarratedText text={Text} textOffset={0} activeWord={null} words={Words} onSelectWord={vi.fn()} sourceNumbersById={new Map([['TA', 1]])} selectedSourceNumber={null} onSelectSource={onSelectSource} /></p>)
    const range = document.createRange()
    range.selectNodeContents(container.querySelector('p')!)
    window.getSelection()?.addRange(range)

    fireEvent.click(screen.getByRole('button', { name: 'View source 1' }), { detail: 1 })

    expect(onSelectSource).not.toHaveBeenCalled()
  })
})
