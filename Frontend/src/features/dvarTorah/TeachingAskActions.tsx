import { useEffect, useRef, useState, type RefObject } from 'react'
import { createPortal } from 'react-dom'
import { MessageCircle, X } from 'lucide-react'
import type { ConversationTeachingContext } from '../conversations/conversationData.ts'

interface Props {
  context: ConversationTeachingContext
  bodyRef: RefObject<HTMLDivElement | null>
  disabled?: boolean
  onAsk(context: ConversationTeachingContext): void
}

interface PassageSelection { text: string; top: number; left: number }
const MaximumSelectionLength = 4000

export function TeachingAskActions({ context, bodyRef, disabled = false, onAsk }: Props) {
  const [selection, setSelection] = useState<PassageSelection | null>(null)
  const toolbarRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    let frame = 0
    function updateSelection() {
      const selected = window.getSelection()
      if (toolbarRef.current?.contains(document.activeElement)) { return }
      if (!selected || selected.isCollapsed || selected.rangeCount !== 1 || !bodyRef.current) { setSelection(null); return }
      const range = selected.getRangeAt(0)
      if (!bodyRef.current.contains(range.startContainer) || !bodyRef.current.contains(range.endContainer)) { setSelection(null); return }
      const text = selected.toString().replace(/\s+/g, ' ').trim()
      if (!text) { setSelection(null); return }
      const rect = range.getBoundingClientRect?.() ?? bodyRef.current.getBoundingClientRect()
      const viewportWidth = document.documentElement.clientWidth || window.innerWidth
      const viewportHeight = window.innerHeight
      const width = Math.min(320, viewportWidth - 32)
      setSelection({ text, left: Math.max(16, Math.min(rect.left, viewportWidth - width - 16)), top: Math.max(16, Math.min(rect.bottom + 10, viewportHeight - 120)) })
    }
    function scheduleSelection() { cancelAnimationFrame(frame); frame = requestAnimationFrame(updateSelection) }
    function handleSelectionKey(event: KeyboardEvent) {
      if (event.key === 'Escape' && toolbarRef.current) {
        event.preventDefault()
        window.getSelection()?.removeAllRanges()
        setSelection(null)
      }
      if (event.key === 'Tab' && !event.shiftKey && toolbarRef.current && !toolbarRef.current.contains(document.activeElement) && window.getSelection()?.isCollapsed === false) {
        event.preventDefault()
        toolbarRef.current.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus()
      }
    }
    document.addEventListener('selectionchange', scheduleSelection)
    document.addEventListener('pointerup', scheduleSelection)
    document.addEventListener('keyup', scheduleSelection)
    document.addEventListener('keydown', handleSelectionKey)
    document.addEventListener('scroll', scheduleSelection, { capture: true, passive: true })
    window.addEventListener('resize', scheduleSelection)
    return () => {
      cancelAnimationFrame(frame)
      document.removeEventListener('selectionchange', scheduleSelection)
      document.removeEventListener('pointerup', scheduleSelection)
      document.removeEventListener('keyup', scheduleSelection)
      document.removeEventListener('keydown', handleSelectionKey)
      document.removeEventListener('scroll', scheduleSelection, true)
      window.removeEventListener('resize', scheduleSelection)
    }
  }, [bodyRef])

  function ask(selectedText: string | null) {
    if (disabled) { return }
    onAsk({ ...context, selectedText })
    window.getSelection()?.removeAllRanges()
    setSelection(null)
  }

  return <>
    <button type="button" disabled={disabled} onClick={() => ask(null)} aria-label="Ask about this teaching" title={disabled ? 'Chat is unavailable right now' : 'Start a new conversation with the whole teaching as context'} className="inline-flex min-h-11 items-center gap-2 rounded-lg border border-line-strong bg-paper px-3 text-sm font-semibold text-pomegranate hover:bg-stone disabled:opacity-50">
      <MessageCircle aria-hidden="true" className="size-4" /><span className="sm:hidden">Ask</span><span className="hidden sm:inline">Ask about this teaching</span>
    </button>
    {selection ? createPortal(<div ref={toolbarRef} role="group" aria-label="Selected teaching passage" style={{ top: selection.top, left: selection.left }} className="fixed z-50 w-80 max-w-[calc(100vw-2rem)] rounded-xl border border-line-strong bg-paper p-3 text-ink shadow-menu" onMouseDown={event => event.preventDefault()}>
      <div className="flex items-center gap-2">
        <button type="button" disabled={disabled || selection.text.length > MaximumSelectionLength} onClick={() => ask(selection.text)} className="inline-flex min-h-11 flex-1 items-center justify-center gap-2 rounded-lg bg-pomegranate px-3 text-sm font-semibold text-white hover:bg-pomegranate-dark disabled:opacity-50"><MessageCircle className="size-4" aria-hidden="true" />Ask about this passage</button>
        <button type="button" aria-label="Dismiss selected passage" onClick={() => { window.getSelection()?.removeAllRanges(); setSelection(null) }} className="flex size-11 shrink-0 items-center justify-center rounded-lg hover:bg-stone"><X aria-hidden="true" className="size-4" /></button>
      </div>
      <p className="mt-2 text-xs leading-5 text-muted" role="status">{selection.text.length > MaximumSelectionLength ? 'Select a shorter passage, or ask about the whole teaching.' : disabled ? 'Chat is unavailable right now.' : 'The whole teaching will be included as context.'}<span className="sr-only"> Press Tab to reach this action, or Escape to dismiss.</span></p>
    </div>, document.body) : null}
  </>
}
