import { useRef } from 'react'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useNarrationFollow } from './useNarrationFollow.ts'
import type { DvarTorahAudioWord } from './dvarTorahTypes.ts'

const Word: DvarTorahAudioWord = { section: 'body', text: 'Learn', textOffset: 0, textLength: 5, audioOffsetMs: 500, durationMs: 500 }

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

function Harness({ word = null, sourceOpen = false }: { word?: DvarTorahAudioWord | null; sourceOpen?: boolean }) {
  const articleRef = useRef<HTMLElement | null>(null)
  const scrollAreaRef = useRef<HTMLElement | null>(null)
  const { isFollowing, toggleFollowing } = useNarrationFollow(word, articleRef, scrollAreaRef, sourceOpen)
  return <><section ref={scrollAreaRef} aria-label="Reading area"><article ref={articleRef}><mark data-narration-word>{word?.text}</mark><button type="button">A citation</button></article></section><button type="button" aria-pressed={isFollowing} onClick={toggleFollowing}>Follow text</button></>
}

function setup(wordTop = 900, reducedMotion = false) {
  vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: reducedMotion })))
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    return this.tagName === 'MARK' ? new DOMRect(0, wordTop, 60, 24) : new DOMRect(0, 100, 800, 600)
  })
  const view = render(<Harness />)
  const area = screen.getByRole('region', { name: 'Reading area' })
  const scrollTo = vi.fn()
  area.scrollTo = scrollTo
  return { ...view, area, scrollTo }
}

describe('narration following', () => {
  it.each([false, true])('keeps the spoken word above the bottom dock (reduced motion: %s)', (reducedMotion) => {
    const { rerender, scrollTo } = setup(900, reducedMotion)

    rerender(<Harness word={Word} />)

    expect(scrollTo).toHaveBeenCalledExactlyOnceWith({ top: 590, behavior: reducedMotion ? 'instant' : 'smooth' })
  })

  it('does not scroll when the highlight is already comfortably visible', () => {
    const { rerender, scrollTo } = setup(300)
    rerender(<Harness word={Word} />)

    expect(scrollTo).not.toHaveBeenCalled()
  })

  it('does not restart the same smooth scroll for every word', () => {
    const { rerender, scrollTo } = setup()
    rerender(<Harness word={Word} />)
    rerender(<Harness word={{ ...Word, text: 'again', textOffset: 6 }} />)

    expect(scrollTo).toHaveBeenCalledTimes(1)
  })

  it.each(['wheel', 'touchMove', 'keyDown', 'pointerDown'] as const)('pauses after manual %s and resumes explicitly', (event) => {
    const { rerender, area, scrollTo } = setup()
    fireEvent[event](area, { key: 'PageDown' })
    rerender(<Harness word={Word} />)

    expect(screen.getByRole('button', { name: 'Follow text' })).toHaveAttribute('aria-pressed', 'false')
    expect(scrollTo).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Follow text' }))
    expect(scrollTo).toHaveBeenCalledTimes(1)
  })

  it('does not scroll while the source reader is open, and resumes after closing', () => {
    const { rerender, scrollTo } = setup()
    rerender(<Harness word={Word} sourceOpen />)
    expect(scrollTo).not.toHaveBeenCalled()

    rerender(<Harness word={Word} />)
    expect(scrollTo).toHaveBeenCalledTimes(1)
  })

  it('does not disable following when a keyboard user activates a citation', () => {
    const { rerender, scrollTo } = setup()
    fireEvent.keyDown(screen.getByRole('button', { name: 'A citation' }), { key: ' ' })
    rerender(<Harness word={Word} />)

    expect(screen.getByRole('button', { name: 'Follow text' })).toHaveAttribute('aria-pressed', 'true')
    expect(scrollTo).toHaveBeenCalledTimes(1)
  })
})
