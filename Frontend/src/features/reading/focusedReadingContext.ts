import { createContext, useContext, useEffect, useMemo, useRef } from 'react'
import { useReadingPreferences } from './readingContext.ts'
import { isLongReading } from './readingPreferences.ts'

interface FocusState {
  target: string | null
  available: boolean
  enter(id: string, trigger?: HTMLElement): void
  exit(id?: string): void
}

export const FocusContext = createContext<FocusState>({ target: null, available: false, enter() {}, exit() {} })
export function useFocusedReading() { return useContext(FocusContext) }

export function useReadingTarget(id: string, text: string, autoEligible = true) {
  const focus = useFocusedReading()
  const { preferences } = useReadingPreferences()
  const attempted = useRef(false)
  const isLong = useMemo(() => isLongReading(text), [text])
  const { enter, exit } = focus
  useEffect(() => {
    if (autoEligible && isLong && preferences.focusLongContent && !attempted.current) {
      attempted.current = true
      enter(id)
    }
  }, [autoEligible, enter, id, isLong, preferences.focusLongContent])
  // A new target (or Strict Mode's mount replay) needs a fresh automatic entry.
  useEffect(() => () => { attempted.current = false; exit(id) }, [exit, id])
  return { ...focus, isLong, isFocused: focus.target === id }
}
