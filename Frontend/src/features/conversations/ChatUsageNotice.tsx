import type { UsageSummary } from '../settings/settingsTypes.ts'

interface ChatUsageNoticeProps {
  usage: UsageSummary | null
  error: string | null
  isOnline: boolean
  onRetry(): void
  onOpenDvarTorah(): void
}

export function ChatUsageNotice({ usage, error, isOnline, onRetry, onOpenDvarTorah }: ChatUsageNoticeProps) {
  if (!isOnline) {
    return <div id="chat-availability-notice" className="mx-auto mb-3 max-w-[50rem] rounded-xl bg-stone px-4 py-3 text-sm text-ink" role="status">
      <p className="font-semibold">You’re offline. Chats are paused.</p>
      <p className="mt-1">Reconnect to send messages. Your draft will stay here.</p>
      <a href="/offline.html" className="mt-2 inline-flex min-h-11 items-center font-semibold text-pomegranate underline underline-offset-4">Read or listen to your saved Dvar Torah</a>
    </div>
  }
  if (usage === null) {
    return <div id="chat-availability-notice" className="mb-2 text-center text-sm text-muted" role="status">
      {error === null ? 'Preparing chat…' : 'Chat is temporarily unavailable.'}
      {error !== null ? <button type="button" onClick={onRetry} className="ml-2 underline">Try again</button> : null}
    </div>
  }
  if (!usage.isLimitReached) {
    return null
  }

  const resetDate = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' }).format(new Date(usage.periodEndUtc))
  return <div id="chat-availability-notice" className="mx-auto mb-3 max-w-[50rem] rounded-xl border border-pomegranate/25 bg-pomegranate/5 px-4 py-3 text-sm text-ink" role="status">
    <p className="font-semibold">Monthly chat limit reached</p>
    <p className="mt-1">You have no chat allowance left this month.</p>
    <p className="mt-1">New messages and follow-ups are paused until {resetDate} at 00:00 UTC. Your chats are still here, and Dvar Torah reading and audio are always available.</p>
    <button type="button" onClick={onOpenDvarTorah} className="mt-2 font-semibold text-pomegranate underline underline-offset-4">Read this week’s Dvar Torah</button>
  </div>
}
