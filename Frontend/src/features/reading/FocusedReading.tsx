import { useCallback, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { ArrowLeft, ScanText } from 'lucide-react'
import { FocusContext, useFocusedReading } from './focusedReadingContext.ts'

export function FocusedReadingProvider({ children }: { children: ReactNode }) {
  const [target, setTarget] = useState<string | null>(null)
  const trigger = useRef<HTMLElement | null>(null)
  const enter = useCallback((id: string, element?: HTMLElement) => {
    trigger.current = element ?? Array.from(document.querySelectorAll<HTMLElement>('[data-reading-target]')).find(node => node.dataset.readingTarget === id)?.querySelector<HTMLElement>('[data-focus-reading]') ?? null
    setTarget(id)
  }, [])
  const exit = useCallback((id?: string) => setTarget(current => id === undefined || current === id ? null : current), [])
  useEffect(() => {
    if (target !== null) { return }
    const element = trigger.current
    if (element?.isConnected) { element.focus({ preventScroll: true }) }
    trigger.current = null
  }, [target])
  return <FocusContext.Provider value={{ target, available: true, enter, exit }}>{children}</FocusContext.Provider>
}

export function FocusReadingButton({ id, label = 'Focus answer', hideLabelOnMobile = false }: { id: string; label?: string; hideLabelOnMobile?: boolean }) {
  const { enter, available, target } = useFocusedReading()
  if (!available) { return null }
  return <button type="button" data-focus-reading hidden={target === id} onClick={event => enter(id, event.currentTarget)} aria-label={label} title={label} className="inline-flex min-h-[44px] min-w-[44px] shrink-0 items-center justify-center gap-2 rounded-lg px-3 text-sm font-semibold text-ink-soft transition hover:bg-stone hover:text-pomegranate focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate"><ScanText aria-hidden="true" className="size-4" /><span className={hideLabelOnMobile ? 'hidden sm:inline' : undefined}>Focus</span></button>
}

export function FocusedReadingToolbar() {
  const { target, exit } = useFocusedReading()
  const button = useRef<HTMLButtonElement>(null)
  useLayoutEffect(() => {
    if (target === null) { return }
    button.current?.focus({ preventScroll: true })
    // Only the active reading pane moves; never the application shell or audio dock.
    document.querySelector<HTMLElement>('[data-reading-focused="true"]')?.closest<HTMLElement>('[data-reading-scroll]')?.scrollTo({ top: 0 })
  }, [target])
  useEffect(() => {
    if (target === null) { return }
    function keyDown(event: KeyboardEvent) {
      if (event.key === 'Escape' && !event.defaultPrevented && !document.querySelector('[role="dialog"], dialog[open]')) { exit() }
    }
    window.addEventListener('keydown', keyDown)
    return () => window.removeEventListener('keydown', keyDown)
  }, [target, exit])
  if (target === null) { return null }
  return <div className="flex shrink-0 items-center justify-between gap-4 border-b border-line bg-parchment px-4 py-2 sm:px-8"><button ref={button} type="button" onClick={() => exit()} className="inline-flex min-h-11 items-center gap-2 rounded-lg px-2 font-semibold text-ink hover:bg-stone"><ArrowLeft aria-hidden="true" className="size-4" />Exit focused reading</button><span className="text-sm text-muted" role="status">Focused reading</span></div>
}
