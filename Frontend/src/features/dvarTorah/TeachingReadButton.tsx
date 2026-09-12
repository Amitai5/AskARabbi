import { BookOpen, Check, LoaderCircle } from 'lucide-react'
import type { TeachingReadProgress } from './useTeachingReadState.ts'

export function TeachingReadButton({ progress, weekKey, title }: { progress: TeachingReadProgress; weekKey: string; title: string }) {
  const isRead = progress.keys?.has(weekKey) ?? false
  const pending = progress.pendingKey === weekKey
  const label = isRead ? 'Mark as unread' : 'Mark as read'
  return (
    <button type="button" onClick={() => void progress.toggle(weekKey)} disabled={progress.offline || progress.keys === null || progress.pendingKey !== null}
      aria-label={`${label}: ${title}`} aria-pressed={progress.keys === null ? undefined : isRead} aria-busy={pending}
      title={progress.offline ? 'Connect to update your reading progress' : label}
      className={`reading-nonessential inline-flex min-h-11 items-center gap-2 rounded-lg border px-3 text-sm font-semibold transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate disabled:cursor-not-allowed disabled:opacity-55 ${isRead ? 'border-pomegranate/30 bg-pomegranate/5 text-pomegranate' : 'border-line-strong bg-paper text-ink-soft hover:border-pomegranate/45 hover:text-pomegranate'}`}>
      {pending ? <LoaderCircle aria-hidden="true" className="size-4 animate-spin" /> : isRead ? <Check aria-hidden="true" className="size-4" /> : <BookOpen aria-hidden="true" className="size-4" />}
      {pending ? 'Saving…' : isRead ? 'Read · Mark as unread' : 'Mark as read'}
    </button>
  )
}
