import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createDemoApplicationClients } from '../../test/demoApplicationClients.ts'
import { ReadingPreferencesProvider } from './ReadingPreferencesProvider.tsx'
import { ReadingSettings } from './ReadingSettings.tsx'
import { ActiveReadingUserKey, applyReadingPreferences, cacheReadingPreferences, clearActiveReadingUser, DefaultReadingPreferences, isLongReading, isReadingPreferences, ReadingCachePrefix, readReadingCache, type ReadingPreferences } from './readingPreferences.ts'

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>(done => { resolve = done })
  return { promise, resolve }
}

beforeEach(() => {
  vi.useFakeTimers()
  localStorage.clear()
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: true })
})
afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); vi.unstubAllGlobals(); clearActiveReadingUser() })

describe('reading preferences', () => {
  it('defaults to light on a dark device and returns to light after signing out', () => {
    vi.stubGlobal('matchMedia', () => ({ matches: true }))
    applyReadingPreferences(DefaultReadingPreferences)
    expect(DefaultReadingPreferences.theme).toBe('light')
    expect(document.documentElement.dataset.theme).toBe('light')

    const saved = { ...DefaultReadingPreferences, theme: 'dark' as const }
    cacheReadingPreferences('reader', saved, false)
    applyReadingPreferences(saved)
    clearActiveReadingUser()

    expect(document.documentElement.dataset.theme).toBe('light')
    expect(document.documentElement.style.colorScheme).toBe('light')
    expect(readReadingCache('reader')?.preferences).toEqual(saved)
  })

  it('validates cache presets and isolates different accounts', () => {
    cacheReadingPreferences('reader-a', { ...DefaultReadingPreferences, theme: 'dark', textSize: 'large' }, false)
    expect(readReadingCache('reader-a')?.preferences.theme).toBe('dark')
    expect(readReadingCache('reader-b')).toBeNull()
    localStorage.setItem(`${ReadingCachePrefix}reader-a`, '{broken')
    expect(readReadingCache('reader-a')).toBeNull()
    expect(isReadingPreferences({ ...DefaultReadingPreferences, theme: 'constructor' })).toBe(false)
    expect(isReadingPreferences({ ...DefaultReadingPreferences, textSize: '100px' })).toBe(false)
    expect(isReadingPreferences({ ...DefaultReadingPreferences, lineSpacing: null })).toBe(false)
    expect(isReadingPreferences(null)).toBe(false)
    clearActiveReadingUser()
    expect(localStorage.getItem(ActiveReadingUserKey)).toBeNull()
  })

  it('continues safely when browser storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('restricted') })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('full') })
    expect(() => cacheReadingPreferences('reader', DefaultReadingPreferences, true)).not.toThrow()
    expect(readReadingCache('reader')).toBeNull()
  })

  it('recognizes long content including languages without word separators', () => {
    expect(isLongReading('A short answer.')).toBe(false)
    expect(isLongReading('word '.repeat(150))).toBe(true)
    expect(isLongReading('學'.repeat(1000))).toBe(true)
  })

  it('applies choices immediately and coalesces rapid changes into one account save', async () => {
    const client = createDemoApplicationClients().conversationSettingsClient
    client.updateReadingPreferences = vi.fn(client.updateReadingPreferences)
    render(<ReadingPreferencesProvider userId="reader" client={client}><ReadingSettings /></ReadingPreferencesProvider>)
    await act(async () => {})

    fireEvent.click(screen.getByRole('radio', { name: 'Large' }))
    fireEvent.click(screen.getByRole('radio', { name: 'Relaxed' }))
    fireEvent.click(screen.getByRole('radio', { name: 'Dark' }))

    expect(document.documentElement.dataset).toMatchObject({ readingSize: 'large', readingSpacing: 'relaxed', theme: 'dark' })
    expect(screen.getByRole('region', { name: 'Reading preview' }).querySelector('.reading-content')).not.toBeNull()
    expect(client.updateReadingPreferences).not.toHaveBeenCalled()
    await act(async () => { await vi.advanceTimersByTimeAsync(350) })
    expect(client.updateReadingPreferences).toHaveBeenCalledExactlyOnceWith({ ...DefaultReadingPreferences, textSize: 'large', lineSpacing: 'relaxed', theme: 'dark' })
    expect(readReadingCache('reader')?.pending).toBe(false)
    expect(screen.getByRole('status')).toHaveTextContent('Reading preferences saved.')
  })

  it('does not let a late initial read overwrite a local selection', async () => {
    const client = createDemoApplicationClients().conversationSettingsClient
    const response = deferred<ReadingPreferences>()
    client.getReadingPreferences = () => response.promise
    render(<ReadingPreferencesProvider userId="reader" client={client}><ReadingSettings /></ReadingPreferencesProvider>)

    fireEvent.click(screen.getByRole('radio', { name: 'Extra Large' }))
    await act(async () => response.resolve(DefaultReadingPreferences))

    expect(screen.getByRole('radio', { name: 'Extra Large' })).toBeChecked()
    expect(document.documentElement.dataset.readingSize).toBe('extra-large')
  })

  it('serializes in-flight writes and saves the latest selection last', async () => {
    const client = createDemoApplicationClients().conversationSettingsClient
    const firstSave = deferred<ReadingPreferences>()
    client.updateReadingPreferences = vi.fn().mockReturnValueOnce(firstSave.promise).mockImplementation(value => Promise.resolve(value))
    render(<ReadingPreferencesProvider userId="reader" client={client}><ReadingSettings /></ReadingPreferencesProvider>)
    await act(async () => {})
    fireEvent.click(screen.getByRole('radio', { name: 'Small' }))
    await act(async () => { await vi.advanceTimersByTimeAsync(350) })
    fireEvent.click(screen.getByRole('radio', { name: 'Extra Large' }))
    await act(async () => { await vi.advanceTimersByTimeAsync(350) })
    expect(client.updateReadingPreferences).toHaveBeenCalledTimes(1)

    await act(async () => firstSave.resolve({ ...DefaultReadingPreferences, textSize: 'small' }))

    expect(client.updateReadingPreferences).toHaveBeenCalledTimes(2)
    expect(client.updateReadingPreferences).toHaveBeenLastCalledWith({ ...DefaultReadingPreferences, textSize: 'extra-large' })
    expect(readReadingCache('reader')).toEqual({ preferences: { ...DefaultReadingPreferences, textSize: 'extra-large' }, pending: false })
  })

  it('keeps failed changes active and retries without reverting the preview', async () => {
    const client = createDemoApplicationClients().conversationSettingsClient
    client.updateReadingPreferences = vi.fn().mockRejectedValueOnce(new Error('offline')).mockImplementation(value => Promise.resolve(value))
    render(<ReadingPreferencesProvider userId="reader" client={client}><ReadingSettings /></ReadingPreferencesProvider>)
    await act(async () => {})
    fireEvent.click(screen.getByRole('radio', { name: 'Dark' }))
    await act(async () => { await vi.advanceTimersByTimeAsync(350) })
    expect(screen.getByRole('status')).toHaveTextContent('could not be saved')
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(readReadingCache('reader')?.pending).toBe(true)

    fireEvent.click(screen.getByRole('button', { name: 'Retry' }))
    await act(async () => {})
    expect(readReadingCache('reader')?.pending).toBe(false)
  })

  it('uses the cache before loading the account, and syncs offline choices on reconnect', async () => {
    cacheReadingPreferences('reader', { ...DefaultReadingPreferences, textSize: 'large', theme: 'dark' }, false)
    const client = createDemoApplicationClients().conversationSettingsClient
    client.getReadingPreferences = vi.fn().mockRejectedValue(new Error('offline'))
    client.updateReadingPreferences = vi.fn(client.updateReadingPreferences)
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: false })
    render(<ReadingPreferencesProvider userId="reader" client={client}><ReadingSettings /></ReadingPreferencesProvider>)
    expect(screen.getByRole('radio', { name: 'Dark' })).toBeChecked()
    const focusSwitch = screen.getByRole('switch', { name: 'Focused reading view' })
    expect(focusSwitch).toHaveAccessibleDescription('Open long chat answers in a distraction-free column by default.')
    expect(screen.getByText(/Dvar Torah teachings always use the regular page view/)).toBeVisible()
    fireEvent.click(focusSwitch)
    await act(async () => { await vi.advanceTimersByTimeAsync(350) })
    expect(client.updateReadingPreferences).not.toHaveBeenCalled()
    expect(readReadingCache('reader')?.preferences.focusLongContent).toBe(true)

    Object.defineProperty(navigator, 'onLine', { configurable: true, value: true })
    await act(async () => window.dispatchEvent(new Event('online')))
    expect(client.updateReadingPreferences).toHaveBeenCalledTimes(1)
    expect(readReadingCache('reader')?.pending).toBe(false)
  })

  it('follows operating-system appearance changes only in System mode', async () => {
    const events = new EventTarget()
    const media = { matches: true, addEventListener: events.addEventListener.bind(events), removeEventListener: events.removeEventListener.bind(events) }
    vi.stubGlobal('matchMedia', () => media)
    const client = createDemoApplicationClients().conversationSettingsClient
    const { unmount } = render(<ReadingPreferencesProvider userId="reader" client={client}><ReadingSettings /></ReadingPreferencesProvider>)
    await act(async () => {})
    expect(screen.getByRole('radio', { name: 'Light' })).toBeChecked()
    expect(document.documentElement.dataset.theme).toBe('light')
    fireEvent.click(screen.getByRole('radio', { name: 'System' }))
    expect(document.documentElement.dataset.theme).toBe('dark')
    media.matches = false
    act(() => events.dispatchEvent(new Event('change')))
    expect(document.documentElement.dataset.theme).toBe('light')
    fireEvent.click(screen.getByRole('radio', { name: 'Dark' }))
    act(() => events.dispatchEvent(new Event('change')))
    expect(document.documentElement.dataset.theme).toBe('dark')
    unmount()
    vi.unstubAllGlobals()
    applyReadingPreferences(DefaultReadingPreferences)
  })
})
