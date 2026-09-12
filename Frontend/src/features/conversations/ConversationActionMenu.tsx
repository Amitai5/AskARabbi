import { useEffect, useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react'
import { createPortal } from 'react-dom'
import { Pencil, Trash2 } from 'lucide-react'

interface ConversationActionMenuProps {
  anchor: HTMLButtonElement
  title: string
  onRename(): void
  onDelete(): void
  onClose(): void
}

export function ConversationActionMenu({ anchor, title, onRename, onDelete, onClose }: ConversationActionMenuProps) {
  const menuRef = useRef<HTMLDivElement>(null)
  const [position, setPosition] = useState({ top: 0, left: 0 })

  useLayoutEffect(() => {
    const rect = anchor.getBoundingClientRect()
    const menu = menuRef.current
    if (!menu) { return }
    const margin = 12
    const below = rect.bottom + 6
    const top = below + menu.offsetHeight <= window.innerHeight - margin ? below : rect.top - menu.offsetHeight - 6
    setPosition({ top: Math.max(margin, top), left: Math.max(margin, Math.min(rect.right - menu.offsetWidth, window.innerWidth - menu.offsetWidth - margin)) })
    menu.querySelector<HTMLButtonElement>('[role="menuitem"]')?.focus()
  }, [anchor])

  useEffect(() => {
    function outside(event: PointerEvent) {
      if (event.target instanceof Node && !menuRef.current?.contains(event.target) && !anchor.contains(event.target)) {
        onClose()
      }
    }
    function dismissOnMove(event: Event) {
      if (!(event.target instanceof Node) || !menuRef.current?.contains(event.target)) { onClose() }
    }
    document.addEventListener('pointerdown', outside)
    window.addEventListener('resize', dismissOnMove)
    window.addEventListener('scroll', dismissOnMove, true)
    return () => {
      document.removeEventListener('pointerdown', outside)
      window.removeEventListener('resize', dismissOnMove)
      window.removeEventListener('scroll', dismissOnMove, true)
    }
  }, [anchor, onClose])

  function handleKey(event: KeyboardEvent) {
    if (event.key === 'Escape' || event.key === 'Tab') {
      if (event.key === 'Escape') { event.preventDefault() }
      anchor.focus()
      onClose()
      return
    }
    const buttons = [...(menuRef.current?.querySelectorAll<HTMLButtonElement>('[role="menuitem"]') ?? [])]
    const current = buttons.indexOf(document.activeElement as HTMLButtonElement)
    const index = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : event.key === 'ArrowDown' ? (current + 1) % buttons.length : event.key === 'ArrowUp' ? (current - 1 + buttons.length) % buttons.length : null
    if (index !== null) { event.preventDefault(); buttons[index]?.focus() }
  }

  return createPortal(
    <div ref={menuRef} role="menu" aria-label={`Actions for ${title}`} onKeyDown={handleKey} data-conversation-actions className="readable-menu fixed z-[60] w-60 max-w-[calc(100vw-1.5rem)] rounded-xl border border-line bg-paper p-1.5 shadow-menu" style={position}>
      <p className="truncate px-3 pb-2 pt-1.5 text-xs text-muted" title={title}>{title}</p>
      <button type="button" role="menuitem" onClick={onRename} className="flex min-h-11 w-full items-center gap-3 rounded-lg px-3 text-left text-ink transition hover:bg-stone focus-visible:bg-stone"><Pencil aria-hidden="true" className="size-4" />Rename</button>
      <button type="button" role="menuitem" onClick={() => { anchor.focus(); onDelete() }} className="flex min-h-11 w-full items-center gap-3 rounded-lg px-3 text-left text-pomegranate transition hover:bg-pomegranate/5 focus-visible:bg-pomegranate/5"><Trash2 aria-hidden="true" className="size-4" />Delete</button>
    </div>, document.body,
  )
}
