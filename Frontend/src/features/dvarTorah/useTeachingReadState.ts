import { useEffect, useMemo, useRef, useState } from 'react'
import { useOnlineStatus } from '../pwa/useOnlineStatus.ts'
import type { DvarTorahClient } from './dvarTorahClient.ts'

interface ProgressSnapshot {
  scope: object | null
  keys: ReadonlySet<string> | null
  error: string | null
  pendingKey: string | null
}
const EmptyProgress: ProgressSnapshot = { scope: null, keys: null, error: null, pendingKey: null }

export function useTeachingReadState(client: DvarTorahClient, offline: boolean, onChange: () => void) {
  const online = useOnlineStatus()
  const [refresh, setRefresh] = useState(0)
  const scope = useMemo(() => ({ client, offline, online, refresh }), [client, offline, online, refresh])
  const [snapshot, setSnapshot] = useState<ProgressSnapshot>(EmptyProgress)
  const { keys, error, pendingKey } = snapshot.scope === scope ? snapshot : EmptyProgress
  const generation = useRef(0)
  const saving = useRef(false)

  useEffect(() => {
    const controller = new AbortController()
    const current = ++generation.current
    saving.current = false
    if (!offline && online) {
      void client.getReadState(controller.signal).then(value => {
        if (!controller.signal.aborted) { setSnapshot({ scope, keys: new Set(value.readWeekKeys), error: null, pendingKey: null }) }
      }).catch(() => {
        if (!controller.signal.aborted) { setSnapshot({ ...EmptyProgress, scope, error: 'Reading progress could not be loaded. Please try again.' }) }
      })
    }
    return () => { generation.current = current + 1; controller.abort() }
  }, [client, offline, online, scope])

  async function toggle(weekKey: string) {
    if (keys === null || saving.current || offline || !online) { return }
    const current = generation.current
    const isRead = !keys.has(weekKey)
    saving.current = true
    setSnapshot({ scope, keys, error: null, pendingKey: weekKey })
    try {
      await client.setReadState(weekKey, isRead)
      if (generation.current !== current) { return }
      const next = new Set(keys)
      if (isRead) { next.add(weekKey) } else { next.delete(weekKey) }
      setSnapshot({ scope, keys: next, error: null, pendingKey: null })
      onChange()
    } catch {
      if (generation.current === current) { setSnapshot({ scope, keys, pendingKey: null, error: 'Your reading progress could not be saved. Please try marking the teaching again.' }) }
    } finally {
      if (generation.current === current) { saving.current = false }
    }
  }

  return { keys, error, pendingKey, offline: offline || !online, toggle, retry: () => setRefresh(value => value + 1) }
}

export type TeachingReadProgress = ReturnType<typeof useTeachingReadState>
