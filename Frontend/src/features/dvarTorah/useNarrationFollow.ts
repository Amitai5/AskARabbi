import { useEffect, useRef } from 'react'
import type { RefObject } from 'react'
import type { DvarTorahAudioWord } from './dvarTorahTypes.ts'

export function useNarrationFollow(activeWord: DvarTorahAudioWord | null, articleRef: RefObject<HTMLElement | null>, scrollAreaRef: RefObject<HTMLElement | null>, isPlaying: boolean, isSourceReaderOpen: boolean) {
  const scrollTargetRef = useRef<number | null>(null)

  useEffect(() => {
    const area = scrollAreaRef.current
    if (area === null) {
      return
    }

    function resetScrollTarget() {
      // Manual scrolling can interrupt a smooth scroll. Follow the next word without waiting for that old target.
      scrollTargetRef.current = null
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.target instanceof HTMLElement && event.target.closest('button, [role="button"], input, select, textarea, a, [contenteditable="true"]') !== null) {
        return
      }
      if (['ArrowDown', 'ArrowUp', 'PageDown', 'PageUp', 'Home', 'End', ' '].includes(event.key)) {
        resetScrollTarget()
      }
    }

    function onPointerDown(event: PointerEvent) {
      if (event.target === area) {
        resetScrollTarget()
      }
    }

    area.addEventListener('wheel', resetScrollTarget, { passive: true })
    area.addEventListener('touchmove', resetScrollTarget, { passive: true })
    area.addEventListener('keydown', onKeyDown)
    area.addEventListener('pointerdown', onPointerDown)
    return () => {
      area.removeEventListener('wheel', resetScrollTarget)
      area.removeEventListener('touchmove', resetScrollTarget)
      area.removeEventListener('keydown', onKeyDown)
      area.removeEventListener('pointerdown', onPointerDown)
    }
  }, [scrollAreaRef])

  useEffect(() => {
    if (!isPlaying || isSourceReaderOpen || activeWord === null) {
      scrollTargetRef.current = null
      return
    }
    const area = scrollAreaRef.current
    const activeWordElement = articleRef.current?.querySelector<HTMLElement>('[data-narration-word]')
    if (area === null || activeWordElement == null) {
      return
    }

    const viewport = area.getBoundingClientRect()
    const word = activeWordElement.getBoundingClientRect()
    if (viewport.height <= 0 || (word.top >= viewport.top + 32 && word.bottom <= viewport.bottom - 48)) {
      return
    }

    const top = Math.max(0, area.scrollTop + word.top - viewport.top - viewport.height * 0.35)
    const priorTarget = scrollTargetRef.current
    // Let an in-flight smooth scroll finish rather than restarting it for each spoken word.
    if (priorTarget !== null && Math.abs(area.scrollTop - priorTarget) > 2 && Math.abs(top - priorTarget) < viewport.height / 4) {
      return
    }
    scrollTargetRef.current = top
    const reducedMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true
    area.scrollTo?.({ top, behavior: reducedMotion ? 'instant' : 'smooth' })
  }, [activeWord, articleRef, isPlaying, isSourceReaderOpen, scrollAreaRef])
}
