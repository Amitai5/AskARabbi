type UserDataEvent = { userId: string; kind: 'chats-deleted' | 'account-deleted' | 'usage-changed'; status?: 'deleted' | 'pending' }
const ChannelName = 'askrabbi-user-data'
const EventOrigin = crypto.randomUUID()

function openChannel() {
  if (typeof BroadcastChannel === 'undefined') { return null }
  try {
    return new BroadcastChannel(ChannelName)
  } catch (error) {
    // Some privacy modes prohibit cross-tab messaging. Server-side session checks
    // remain authoritative; this optional UI notification must not undo erasure.
    if (error instanceof DOMException && error.name === 'SecurityError') { return null }
    throw error
  }
}

// No chat content or personal profile is persisted in browser storage.
export function publishUserDataEvent(event: UserDataEvent) {
  const channel = openChannel()
  if (!channel) { return }
  try {
    channel.postMessage({ ...event, origin: EventOrigin })
  } finally {
    channel.close()
  }
}

export function subscribeToUserDataEvents(userId: string, onEvent: (event: UserDataEvent) => void) {
  const channel = openChannel()
  if (!channel) { return () => {} }
  channel.onmessage = (message: MessageEvent<unknown>) => {
    const value = message.data
    if (typeof value !== 'object' || value === null || !('userId' in value) || value.userId !== userId || !('kind' in value)) { return }
    // The sending tab already applied the turn's usage; only other tabs need a refresh.
    if (value.kind === 'usage-changed' && 'origin' in value && value.origin === EventOrigin) { return }
    if (value.kind === 'chats-deleted' || value.kind === 'account-deleted' || value.kind === 'usage-changed') {
      onEvent(value as UserDataEvent)
    }
  }
  return () => channel.close()
}
