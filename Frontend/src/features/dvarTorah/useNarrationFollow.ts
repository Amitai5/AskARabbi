import { useEffect, useRef, useState } from 'react'
import type { RefObject } from 'react'
import type { DvarTorahAudioWord } from './dvarTorahTypes.ts'

export function useNarrationFollow(activeWord: DvarTorahAudioWord | null, articleRef: RefObject<HTMLElement | null>, scrollAreaRef: RefObject<HTMLElement | null>, isSourceReaderOpen: boolean) {
  const [isFollowing, setIsFollowing] = useState(true)
  const scrollTargetRef = useRef<number | null>(null)

  useEffect(() => {
    const area = scrollAreaRef.current
    if (area === null) {
      return
    }

    function pauseFollowing() {
      setIsFollowing(false)
      scrollTargetRef.current = null
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.target instanceof HTMLElement && event.target.closest('button, input, select, textarea, a, [contenteditable="true"]') !== null) {
        return
      }
      if (['ArrowDown', 'ArrowUp', 'PageDown', 'PageUp', 'Home', 'End', ' '].includes(event.key)) {
        pauseFollowing()
      }
    }

    function onPointerDown(event: PointerEvent) {
      // Scrollbar interaction should release following, but selecting a citation should not.
      if (event.target === area) {
        pauseFollowing()
      }
    }

    area.addEventListener('wheel', pauseFollowing, { passive: true })
    area.addEventListener('touchmove', pauseFollowing, { passive: true })
    area.addEventListener('keydown', onKeyDown)
    area.addEventListener('pointerdown', onPointerDown)
    return () => {
      area.removeEventListener('wheel', pauseFollowing)
      area.removeEventListener('touchmove', pauseFollowing)
      area.removeEventListener('keydown', onKeyDown)
      area.removeEventListener('pointerdown', onPointerDown)
    }
  }, [scrollAreaRef])

  useEffect(() => {
    if (!isFollowing || isSourceReaderOpen || activeWord === null) {
      return
    }
    const area = scrollAreaRef.current
    const mark = articleRef.current?.querySelector<HTMLElement>('[data-narration-word]')
    if (area === null || mark == null) {
      return
    }

    const viewport = area.getBoundingClientRect()
    const word = mark.getBoundingClientRect()
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
  }, [activeWord, articleRef, isFollowing, isSourceReaderOpen, scrollAreaRef])

  function toggleFollowing() {
    scrollTargetRef.current = null
    setIsFollowing((current) => !current)
  }

  return { isFollowing, toggleFollowing }
}
