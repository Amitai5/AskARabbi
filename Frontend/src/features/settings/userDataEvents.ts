type UserDataEvent = { userId: string; kind: 'chats-deleted' | 'account-deleted'; status?: 'deleted' | 'pending' }
const ChannelName = 'askrabbi-user-data'

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
    channel.postMessage(event)
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
    if (value.kind === 'chats-deleted' || value.kind === 'account-deleted') {
      onEvent(value as UserDataEvent)
    }
  }
  return () => channel.close()
}
