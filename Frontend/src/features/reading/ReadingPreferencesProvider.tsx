import { useCallback, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import type { ConversationSettingsClient } from '../personalization/conversationSettingsClient.ts'
import { applyReadingPreferences, cacheReadingPreferences, DefaultReadingPreferences, isReadingPreferences, readActiveReadingPreferences, readReadingCache, type ReadingPreferences } from './readingPreferences.ts'

import { ReadingContext, type ReadingContextValue } from './readingContext.ts'

/** The offline library uses only the local presentation cache, never an authenticated API. */
export function CachedReadingPreferencesProvider({ children }: { children: ReactNode }) {
  const [preferences] = useState(readActiveReadingPreferences)
  useLayoutEffect(() => {
    const apply = () => applyReadingPreferences(preferences)
    apply()
    const media = window.matchMedia?.('(prefers-color-scheme: dark)')
    media?.addEventListener('change', apply)
    return () => media?.removeEventListener('change', apply)
  }, [preferences])
  return <ReadingContext.Provider value={{ preferences, status: 'saved', error: null, update() {}, retry() {} }}>{children}</ReadingContext.Provider>
}

export function ReadingPreferencesProvider({ userId, client, children }: { userId: string; client: ConversationSettingsClient; children: ReactNode }) {
  const [initial] = useState(() => readReadingCache(userId))
  const [preferences, setPreferences] = useState(initial?.preferences ?? DefaultReadingPreferences)
  const [status, setStatus] = useState<ReadingContextValue['status']>('loading')
  const [error, setError] = useState<string | null>(null)
  const [reloadKey, setReloadKey] = useState(0)
  const state = useRef({ preferences, version: 0, pending: initial?.pending ?? false, saving: false })
  const mounted = useRef(false)

  const save = useCallback(async () => {
    if (!mounted.current || state.current.saving || !state.current.pending) { return }
    if (!navigator.onLine) {
      setStatus('error')
      setError('Changes are saved on this device. They’ll sync to your account when you reconnect.')
      return
    }
    state.current.saving = true
    setStatus('saving')
    setError(null)
    try {
      // Serialize saves: an earlier response must never overwrite a newer selection.
      while (mounted.current && state.current.pending) {
        const { version, preferences: snapshot } = state.current
        await client.updateReadingPreferences(snapshot)
        if (!mounted.current) { return }
        if (version === state.current.version) {
          state.current.pending = false
          cacheReadingPreferences(userId, snapshot, false)
        }
      }
      if (mounted.current) { setStatus('saved') }
    } catch {
      if (mounted.current) {
        setStatus('error')
        setError('Your changes are active on this device, but could not be saved to your account. Please retry.')
      }
    } finally {
      state.current.saving = false
    }
  }, [client, userId])

  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const version = state.current.version
    const hadPendingChanges = state.current.pending
    void client.getReadingPreferences(controller.signal).then(value => {
      if (controller.signal.aborted || version !== state.current.version || hadPendingChanges) { return }
      if (!isReadingPreferences(value)) { throw new Error('Invalid reading preferences') }
      state.current.preferences = value
      setPreferences(value)
      cacheReadingPreferences(userId, value, false)
      setStatus('saved')
      setError(null)
    }).catch(() => {
      if (!controller.signal.aborted && !state.current.saving && version === state.current.version) {
        setStatus('error')
        setError('Account reading preferences could not be loaded. Your device preferences are still active.')
      }
    })
    return () => controller.abort()
  }, [client, userId, reloadKey])

  useLayoutEffect(() => {
    applyReadingPreferences(preferences)
    const media = window.matchMedia?.('(prefers-color-scheme: dark)')
    const updateTheme = () => applyReadingPreferences(preferences)
    media?.addEventListener('change', updateTheme)
    return () => media?.removeEventListener('change', updateTheme)
  }, [preferences])

  useEffect(() => {
    if (!state.current.pending) { return }
    const timer = window.setTimeout(() => void save(), 350)
    return () => window.clearTimeout(timer)
  }, [preferences, save])

  useEffect(() => {
    const online = () => { if (state.current.pending) { void save() } else { setReloadKey(key => key + 1) } }
    window.addEventListener('online', online)
    return () => window.removeEventListener('online', online)
  }, [save])

  const update = useCallback((patch: Partial<ReadingPreferences>) => {
    const next = { ...state.current.preferences, ...patch }
    state.current.preferences = next
    state.current.version += 1
    state.current.pending = true
    cacheReadingPreferences(userId, next, true)
    setPreferences(next)
    setStatus('saving')
    setError(null)
  }, [userId])

  return <ReadingContext.Provider value={{ preferences, status, error, update, retry: () => { if (state.current.pending) { void save() } else { setReloadKey(key => key + 1) } } }}>{children}</ReadingContext.Provider>
}
