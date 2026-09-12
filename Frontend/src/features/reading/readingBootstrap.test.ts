import { describe, expect, it, vi } from 'vitest'
import bootstrap from '../../../public/reading-bootstrap.js?raw'
import { ActiveReadingUserKey, DefaultReadingPreferences, ReadingCachePrefix, type ReadingPreferences } from './readingPreferences.ts'

function runBootstrap({ path = '/', dark = true, preferences, cached = preferences ? JSON.stringify({ preferences, pending: false }) : null, restrictedStorage = false }: { path?: string; dark?: boolean; preferences?: ReadingPreferences; cached?: string | null; restrictedStorage?: boolean } = {}) {
  const root = { dataset: {} as Record<string, string>, style: {} as Record<string, string> }
  const storage = new Map<string, string>([[ActiveReadingUserKey, 'reader']])
  if (cached !== null) { storage.set(`${ReadingCachePrefix}reader`, cached) }
  const matchMedia = vi.fn(() => ({ matches: dark }))
  // Execute the actual static startup asset against isolated browser fixtures.
  const initialize = new Function('document', 'location', 'localStorage', 'matchMedia', bootstrap)
  initialize(
    { documentElement: root },
    { pathname: path },
    { getItem(key: string) { if (restrictedStorage) { throw new Error('restricted') } return storage.get(key) ?? null } },
    matchMedia,
  )
  return { root, storage, matchMedia }
}

describe('pre-paint reading theme', () => {
  it.each([false, true])('starts new visitors in light mode when OS dark mode is %s', dark => {
    const { root, matchMedia } = runBootstrap({ dark })
    expect(root.dataset).toEqual({ theme: 'light', readingSize: 'default', readingSpacing: 'default' })
    expect(root.style).toEqual({ colorScheme: 'light', backgroundColor: '#fbf8f2' })
    expect(matchMedia).not.toHaveBeenCalled()
  })

  it.each(['/', '/reset-password'])('keeps %s light without overwriting a cached account theme', path => {
    const preferences = { ...DefaultReadingPreferences, theme: 'system' as const, textSize: 'large' as const }
    const { root, storage, matchMedia } = runBootstrap({ path, preferences })
    expect(root.dataset.theme).toBe('light')
    expect(matchMedia).not.toHaveBeenCalled()
    expect(JSON.parse(storage.get(`${ReadingCachePrefix}reader`)!)).toEqual({ preferences, pending: false })
    expect(storage.get(ActiveReadingUserKey)).toBe('reader')
  })

  it.each(['/settings/reading', '/conversations/example', '/offline.html'])('restores saved reading preferences before paint on %s', path => {
    const preferences = { ...DefaultReadingPreferences, theme: 'dark' as const, textSize: 'large' as const, lineSpacing: 'relaxed' as const }
    const { root } = runBootstrap({ path, preferences })
    expect(root.dataset).toEqual({ theme: 'dark', readingSize: 'large', readingSpacing: 'relaxed' })
    expect(root.style).toEqual({ colorScheme: 'dark', backgroundColor: '#171b22' })
  })

  it.each([false, true])('honors an explicitly saved System preference when OS dark mode is %s', dark => {
    const { root, matchMedia } = runBootstrap({ path: '/settings/reading', dark, preferences: { ...DefaultReadingPreferences, theme: 'system' } })
    expect(root.dataset.theme).toBe(dark ? 'dark' : 'light')
    expect(matchMedia).toHaveBeenCalledWith('(prefers-color-scheme: dark)')
  })

  it.each([null, '{broken', JSON.stringify({ preferences: { ...DefaultReadingPreferences, theme: 'invalid' } })])('uses light for missing or invalid cache: %s', cached => {
    const { root } = runBootstrap({ path: '/settings/reading', cached })
    expect(root.dataset.theme).toBe('light')
  })

  it('uses light when browser storage is unavailable', () => {
    const { root } = runBootstrap({ path: '/settings/reading', restrictedStorage: true })
    expect(root.dataset.theme).toBe('light')
  })
})
