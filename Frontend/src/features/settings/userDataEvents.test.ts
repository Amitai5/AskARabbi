import { afterEach, describe, expect, it, vi } from 'vitest'
import { publishUserDataEvent, subscribeToUserDataEvents } from './userDataEvents.ts'

describe('account data notifications', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('does not fail a completed deletion when cross-tab messaging is prohibited', () => {
    vi.stubGlobal('BroadcastChannel', class {
      constructor() { throw new DOMException('Messaging prohibited', 'SecurityError') }
    })

    expect(() => publishUserDataEvent({ userId: 'owner', kind: 'account-deleted' })).not.toThrow()
    const dispose = subscribeToUserDataEvents('owner', vi.fn())
    expect(dispose).not.toThrow()
  })

  it('supports browsers without cross-tab messaging', () => {
    vi.stubGlobal('BroadcastChannel', undefined)

    expect(() => publishUserDataEvent({ userId: 'owner', kind: 'chats-deleted' })).not.toThrow()
    expect(subscribeToUserDataEvents('owner', vi.fn())).not.toThrow()
  })

  it('notifies only the matching account and closes the subscription on cleanup', () => {
    const close = vi.fn()
    let receive: ((event: MessageEvent<unknown>) => void) | null = null
    vi.stubGlobal('BroadcastChannel', class {
      set onmessage(handler: (event: MessageEvent<unknown>) => void) { receive = handler }
      close = close
    })
    const onEvent = vi.fn()
    const dispose = subscribeToUserDataEvents('owner', onEvent)
    const dispatch = (data: unknown) => receive?.(new MessageEvent('message', { data }))

    dispatch(null)
    dispatch({ userId: 'different-owner', kind: 'chats-deleted' })
    dispatch({ userId: 'owner', kind: 'unknown' })
    expect(onEvent).not.toHaveBeenCalled()
    dispatch({ userId: 'owner', kind: 'chats-deleted' })
    expect(onEvent).toHaveBeenCalledExactlyOnceWith({ userId: 'owner', kind: 'chats-deleted' })
    dispose()
    expect(close).toHaveBeenCalledOnce()
  })
})
