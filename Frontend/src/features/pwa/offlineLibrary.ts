import type { DvarTorahAudioTimings, WeeklyDvarTorahResponse } from '../dvarTorah/dvarTorahTypes.ts'
import type { CalendarEvent, CalendarOverview } from '../calendar/calendarTypes.ts'
import { addCivilDays } from '../calendar/calendarAgenda.ts'

export interface SavedHolidayCalendar {
  version: 1
  startDate: string
  endDate: string
  inIsrael: boolean
  savedAt: string
  events: CalendarEvent[]
}

export interface SavedTeaching {
  publication: WeeklyDvarTorahResponse
  savedAt: string
  audio: Blob | null
  timings: DvarTorahAudioTimings | null
}

export interface OfflineLibrary {
  audioEnabled: boolean
  revision: number
  teaching: SavedTeaching | null
  holidays?: SavedHolidayCalendar | null
}

export const OfflineLibraryChanged = 'askarabbi-offline-library-changed'
export const OfflineLibraryCleared = 'askarabbi-offline-library-cleared'
const DatabaseName = 'askarabbi-weekly-offline'
const StoreName = 'library'

function accessLibrary(update?: (library: OfflineLibrary) => OfflineLibrary): Promise<OfflineLibrary> {
  return new Promise((resolve, reject) => {
    if (!('indexedDB' in window)) {
      reject(new Error('Offline storage is unavailable in this browser.'))
      return
    }
    const open = indexedDB.open(DatabaseName, 1)
    open.onupgradeneeded = () => open.result.createObjectStore(StoreName)
    open.onerror = () => reject(open.error)
    open.onblocked = () => reject(new Error('Close other AskRabbi windows and try again.'))
    open.onsuccess = () => {
      const database = open.result
      database.onversionchange = () => database.close()
      const transaction = database.transaction(StoreName, update ? 'readwrite' : 'readonly')
      const store = transaction.objectStore(StoreName)
      const request = store.get('current')
      let result: OfflineLibrary
      request.onsuccess = () => {
        const library: OfflineLibrary = request.result ?? { audioEnabled: true, revision: 0, teaching: null }
        result = update ? update(library) : library
        if (update) { store.put(result, 'current') }
      }
      transaction.oncomplete = () => { database.close(); resolve(result) }
      transaction.onabort = () => { database.close(); reject(transaction.error ?? new Error('Offline storage could not be updated.')) }
      transaction.onerror = () => { /* The transaction's abort event reports the failure. */ }
    }
  })
}

export function readOfflineLibrary() {
  return accessLibrary()
}

export function setOfflineAudioEnabled(enabled: boolean) {
  return accessLibrary(library => ({
    ...library,
    audioEnabled: enabled,
    teaching: !enabled && library.teaching ? { ...library.teaching, audio: null, timings: null } : library.teaching,
  }))
}

export function saveOfflinePublication(publication: WeeklyDvarTorahResponse, revision: number, signal: AbortSignal) {
  return accessLibrary(library => {
    const article = publication.dvarTorah
    if (signal.aborted || library.revision !== revision || !publication.isCurrentWeek || !article || article.week.weekKey !== publication.currentWeek.weekKey) {
      return library
    }
    const previous = library.teaching
    const previousArticle = previous?.publication.dvarTorah
    const sameRecording = previousArticle?.week.weekKey === article.week.weekKey && previousArticle?.audio?.version === article.audio?.version
      && previousArticle?.title === article.title && previousArticle?.body === article.body
    return {
      ...library,
      teaching: { publication, savedAt: new Date().toISOString(), audio: sameRecording && library.audioEnabled ? previous?.audio ?? null : null, timings: sameRecording && library.audioEnabled ? previous?.timings ?? null : null },
    }
  })
}

export function saveOfflineRecording(teaching: SavedTeaching, revision: number, audio: Blob, timings: DvarTorahAudioTimings | null, signal: AbortSignal) {
  return accessLibrary(library => {
    const current = library.teaching
    // Recheck inside the write transaction: logout, a newer publication, or another tab's opt-out wins.
    if (signal.aborted || library.revision !== revision || !library.audioEnabled || current?.savedAt !== teaching.savedAt) {
      return library
    }
    return { ...library, teaching: { ...current, audio, timings } }
  })
}

// Only public holiday fields are saved: never the overview's location, solar times or profile.
export function saveOfflineHolidays(overview: CalendarOverview, revision: number, signal: AbortSignal) {
  return accessLibrary(library => {
    if (signal.aborted || library.revision !== revision || !overview.holidays.isAvailable || overview.holidays.isStale) { return library }
    return { ...library, holidays: {
      version: 1, startDate: overview.today.gregorianDate, endDate: addCivilDays(overview.today.gregorianDate, 359),
      inIsrael: overview.preferences.inIsrael, savedAt: new Date().toISOString(),
      events: overview.events.map(({ id, kind, title, category, startDate, endDate, beginningDate, beginningRule, explanation, sourceUrl, occurrences }) => ({ id, kind, title, category, startDate, endDate, beginningDate, beginningRule, explanation, sourceUrl, occurrences, isOngoing: false })),
    } }
  })
}

export async function clearOfflineHolidays() {
  try {
    await accessLibrary(library => ({ ...library, revision: library.revision + 1, holidays: null }))
    window.dispatchEvent(new Event(OfflineLibraryChanged))
  } catch { /* Unavailable browser storage must not prevent saving account preferences. */ }
}

export async function clearOfflineTeaching() {
  window.dispatchEvent(new Event(OfflineLibraryCleared))
  try {
    await accessLibrary(library => ({ ...library, revision: library.revision + 1, teaching: null, holidays: null }))
    window.dispatchEvent(new Event(OfflineLibraryChanged))
  } catch {
    // An unavailable offline store must not prevent account logout/deletion.
    console.warn('AskRabbi could not clear offline storage. Clear this site’s browser data on a shared device.')
  }
}
