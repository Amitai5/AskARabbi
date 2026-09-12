import { createContext, useContext } from 'react'
import type { OfflineLibrary } from './offlineLibrary.ts'

interface OfflineLearningState {
  library: OfflineLibrary | null
  isSaving: boolean
  error: string | null
  changeAudio(enabled: boolean): Promise<void>
  refresh(): void
}

export const OfflineLearningContext = createContext<OfflineLearningState | null>(null)
export function useOfflineLearningLibrary() { return useContext(OfflineLearningContext)?.library ?? null }
