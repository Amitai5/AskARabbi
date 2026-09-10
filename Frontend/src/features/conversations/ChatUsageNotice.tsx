import { formatUsagePercent, type UsageSummary } from '../settings/settingsTypes.ts'

interface ChatUsageNoticeProps {
  usage: UsageSummary | null
  error: string | null
  onRetry(): void
  onOpenDvarTorah(): void
}

export function ChatUsageNotice({ usage, error, onRetry, onOpenDvarTorah }: ChatUsageNoticeProps) {
  if (usage === null) {
    return <div id="chat-allowance-notice" className="mb-2 text-center text-sm text-muted" role="status">
      {error ?? 'Checking your chat allowance…'}
      {error !== null ? <button type="button" onClick={onRetry} className="ml-2 underline">Try again</button> : null}
    </div>
  }
  if (!usage.isLimitReached) {
    return <p className="mb-1 text-center text-xs text-muted">{formatUsagePercent(usage)}% of monthly chat allowance used</p>
  }

  const resetDate = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' }).format(new Date(usage.periodEndUtc))
  return <div id="chat-allowance-notice" className="mx-auto mb-3 max-w-[50rem] rounded-xl border border-pomegranate/25 bg-pomegranate/5 px-4 py-3 text-sm text-ink" role="status">
    <p className="font-semibold">100% used · Monthly chat limit reached</p>
    <p className="mt-1">New messages and follow-ups are paused until {resetDate} at 00:00 UTC. Your chats are still here, and Dvar Torah reading and audio are always available.</p>
    <button type="button" onClick={onOpenDvarTorah} className="mt-2 font-semibold text-pomegranate underline underline-offset-4">Read this week’s Dvar Torah</button>
  </div>
}
