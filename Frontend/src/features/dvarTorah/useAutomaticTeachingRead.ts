import { useCallback, useEffect, useEffectEvent, useRef } from 'react'
import type { TeachingReadProgress } from './useTeachingReadState.ts'

interface ReadingVisit {
  weekKey: string
  elapsedMs: number
  stopped: boolean
}

export function useAutomaticTeachingRead(weekKey: string, readingMinutes: number | null, progress: TeachingReadProgress) {
  const visitRef = useRef<ReadingVisit | null>(null)
  const timerRef = useRef<number | null>(null)
  const isRead = progress.keys?.has(weekKey) ?? false
  const canSave = !progress.offline && progress.keys !== null && progress.pendingKey === null
  const requiredMs = readingMinutes !== null && Number.isFinite(readingMinutes) && readingMinutes > 0
    ? Math.floor(readingMinutes * 60_000 * 0.33) + 1
    : null
  const markRead = useEffectEvent(() => { void progress.markRead(weekKey) })

  useEffect(() => {
    if (visitRef.current?.weekKey !== weekKey) { visitRef.current = { weekKey, elapsedMs: 0, stopped: false } }
    const visit = visitRef.current
    let startedAt: number | null = null

    function pause() {
      if (startedAt !== null) {
        visit.elapsedMs += Math.max(0, performance.now() - startedAt)
        startedAt = null
      }
      if (timerRef.current !== null) { window.clearTimeout(timerRef.current); timerRef.current = null }
    }

    function resume() {
      pause()
      if (document.visibilityState !== 'visible' || visit.stopped || isRead || requiredMs === null) { return }
      if (visit.elapsedMs >= requiredMs) {
        if (canSave) {
          // One automatic attempt per visit; a failed save stays retryable with the manual control.
          visit.stopped = true
          markRead()
        }
        return
      }
      startedAt = performance.now()
      timerRef.current = window.setTimeout(resume, Math.ceil(requiredMs - visit.elapsedMs))
    }

    document.addEventListener('visibilitychange', resume)
    resume()
    return () => { pause(); document.removeEventListener('visibilitychange', resume) }
  }, [weekKey, requiredMs, canSave, isRead])

  return useCallback(() => {
    // A reader's explicit choice takes precedence for the rest of this article visit.
    if (visitRef.current) { visitRef.current.stopped = true }
    if (timerRef.current !== null) { window.clearTimeout(timerRef.current); timerRef.current = null }
  }, [])
}
