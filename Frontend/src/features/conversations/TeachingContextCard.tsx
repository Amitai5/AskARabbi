import { BookOpenText, X } from 'lucide-react'
import type { ConversationTeachingContext } from './conversationData.ts'
import { teachingPath } from './pageRoutes.ts'

export function TeachingContextCard({ context, onRemove }: { context: ConversationTeachingContext; onRemove?(): void }) {
  return <aside aria-label="Teaching context" className="mx-auto mb-3 w-full max-w-[50rem] rounded-xl border border-brass/35 bg-stone/50 px-3 py-2 text-sm">
    <div className="flex items-center gap-3">
      <BookOpenText aria-hidden="true" className="size-4 shrink-0 text-brass" />
      <div className="min-w-0 flex-1"><p className="text-xs text-muted">Whole teaching included as context</p><a href={teachingPath({ weekKey: context.weekKey })} target="_blank" rel="noopener noreferrer" className="block truncate font-semibold text-ink hover:text-pomegranate" title="Open teaching in a new tab">{context.title}<span className="sr-only"> (opens in a new tab)</span></a></div>
      {onRemove ? <button type="button" onClick={onRemove} aria-label="Remove teaching context" className="flex size-11 shrink-0 items-center justify-center rounded-lg text-muted hover:bg-stone-deep"><X aria-hidden="true" className="size-4" /></button> : null}
    </div>
    {context.selectedText ? <details className="mt-1"><summary className="min-h-8 cursor-pointer py-1 text-pomegranate">Selected passage</summary><blockquote className="mt-2 max-h-28 overflow-y-auto border-l-2 border-brass pl-3 leading-6 text-ink-soft">{context.selectedText}</blockquote></details> : null}
  </aside>
}
