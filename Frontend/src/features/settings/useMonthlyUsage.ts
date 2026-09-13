import { useCallback, useEffect, useRef, useState } from 'react'
import type { ConversationSettingsClient } from '../personalization/conversationSettingsClient.ts'
import type { UsageSummary } from './settingsTypes.ts'
import { publishUserDataEvent, subscribeToUserDataEvents } from './userDataEvents.ts'

export function useMonthlyUsage(client: ConversationSettingsClient, userId: string) {
  const [usage, setUsage] = useState<UsageSummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(() => navigator.onLine)
  const requestVersion = useRef(0)

  const fetchUsage = useCallback(() => {
    const version = ++requestVersion.current
    if (!navigator.onLine) {
      return Promise.resolve()
    }
    return client.getUsage()
      .then((value) => {
        if (requestVersion.current === version) { setUsage(value); setError(null) }
      })
      .catch((failure: unknown) => {
        if (requestVersion.current === version) {
          setError(failure instanceof Error ? failure.message : 'Your chat allowance could not be loaded.')
        }
      })
      .finally(() => {
        if (requestVersion.current === version) { setIsLoading(false) }
      })
  }, [client])

  const refresh = useCallback(() => {
    if (!navigator.onLine) { return Promise.resolve() }
    setIsLoading(true)
    setError(null)
    return fetchUsage()
  }, [fetchUsage])

  const update = useCallback((value: UsageSummary) => {
    // A completed turn is newer than any usage read that was already in flight.
    requestVersion.current += 1
    setUsage(value)
    setError(null)
    setIsLoading(false)
    publishUserDataEvent({ userId, kind: 'usage-changed' })
  }, [userId])

  useEffect(() => {
    void fetchUsage()
    const onFocus = () => { void refresh() }
    const onVisibilityChange = () => {
      if (document.visibilityState === 'visible') { void refresh() }
    }
    const onOffline = () => { requestVersion.current += 1; setIsLoading(false) }
    window.addEventListener('focus', onFocus)
    window.addEventListener('online', onFocus)
    window.addEventListener('offline', onOffline)
    document.addEventListener('visibilitychange', onVisibilityChange)
    const unsubscribe = subscribeToUserDataEvents(userId, (event) => {
      if (event.kind === 'usage-changed') { void refresh() }
    })
    return () => {
      requestVersion.current += 1
      window.removeEventListener('focus', onFocus)
      window.removeEventListener('online', onFocus)
      window.removeEventListener('offline', onOffline)
      document.removeEventListener('visibilitychange', onVisibilityChange)
      unsubscribe()
    }
  }, [fetchUsage, refresh, userId])

  useEffect(() => {
    if (usage === null) { return }
    const remaining = Date.parse(usage.periodEndUtc) - Date.now()
    if (!Number.isFinite(remaining)) { return }
    const timer = window.setTimeout(() => { void refresh() }, Math.min(2_147_000_000, remaining > 0 ? remaining + 100 : 60_000))
    return () => window.clearTimeout(timer)
  }, [usage, refresh])

  return { usage, error, isLoading, refresh, update }
}
