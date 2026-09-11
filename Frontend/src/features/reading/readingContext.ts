import { createContext, useContext } from 'react'
import { DefaultReadingPreferences, type ReadingPreferences } from './readingPreferences.ts'

export interface ReadingContextValue {
  preferences: ReadingPreferences
  status: 'loading' | 'saved' | 'saving' | 'error'
  error: string | null
  update(preferences: Partial<ReadingPreferences>): void
  retry(): void
}

export const ReadingContext = createContext<ReadingContextValue>({ preferences: DefaultReadingPreferences, status: 'saved', error: null, update() {}, retry() {} })
export function useReadingPreferences() { return useContext(ReadingContext) }
