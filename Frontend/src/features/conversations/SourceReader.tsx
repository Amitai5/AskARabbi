import { memo, useEffect, useRef } from 'react'
import type { PointerEvent as ReactPointerEvent } from 'react'
import { ChevronDown, ChevronLeft, ChevronRight, ExternalLink, Quote, X } from 'lucide-react'
import type { ConversationSource } from './conversationData.ts'

interface SourceReaderProps {
  messageId: string
  sources: readonly ConversationSource[]
  selectedIndex: number
  showSourceContextByDefault: boolean
  onSelectSourceNumber(sourceNumber: number): void
  onClose(): void
}

interface SourceReaderContentProps {
  idPrefix: string
  messageId: string
  source: ConversationSource
  showSourceContextByDefault: boolean
  contextLabel: string
}

interface SourceNavigationProps {
  selectedIndex: number
  sources: readonly ConversationSource[]
  onSelectSourceNumber(sourceNumber: number): void
}

export const SourceReader = memo(function SourceReader({ messageId, sources, selectedIndex, showSourceContextByDefault, onSelectSourceNumber, onClose }: SourceReaderProps) {
  const desktopCloseButtonRef = useRef<HTMLButtonElement>(null)
  const mobileCloseButtonRef = useRef<HTMLButtonElement>(null)
  const mobileSheetRef = useRef<HTMLElement>(null)
  const dragStartYRef = useRef<number | null>(null)
  const dragDistanceRef = useRef(0)
  const didDragRef = useRef(false)
  const source = sources[selectedIndex]

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    const desktopCloseButton = desktopCloseButtonRef.current
    const closeButton = desktopCloseButton?.getClientRects().length === 0 ? mobileCloseButtonRef.current : desktopCloseButton
    closeButton?.focus()
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [messageId, onClose])

  if (source === undefined) {
    return null
  }

  function handleDragStart(event: ReactPointerEvent<HTMLButtonElement>) {
    dragStartYRef.current = event.clientY
    dragDistanceRef.current = 0
    didDragRef.current = false
    event.currentTarget.setPointerCapture(event.pointerId)
    if (mobileSheetRef.current !== null) {
      mobileSheetRef.current.style.transition = 'none'
    }
  }

  function handleDragMove(event: ReactPointerEvent<HTMLButtonElement>) {
    if (dragStartYRef.current === null || mobileSheetRef.current === null) {
      return
    }

    const distance = Math.max(0, event.clientY - dragStartYRef.current)
    dragDistanceRef.current = distance
    didDragRef.current = distance > 4
    mobileSheetRef.current.style.transform = `translateY(${distance}px)`
  }

  function handleDragEnd(event: ReactPointerEvent<HTMLButtonElement>) {
    if (dragStartYRef.current === null) {
      return
    }

    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId)
    }
    dragStartYRef.current = null
    if (mobileSheetRef.current !== null) {
      mobileSheetRef.current.style.transition = ''
    }
    if (dragDistanceRef.current >= 96) {
      onClose()
      return
    }

    if (mobileSheetRef.current !== null) {
      mobileSheetRef.current.style.transform = ''
    }
  }

  function handleDragHandleClick() {
    if (!didDragRef.current) {
      onClose()
    }
    didDragRef.current = false
  }

  return (
    <>
      <aside className="source-reader-desktop hidden min-h-0 w-[min(38vw,32rem)] min-w-[23rem] shrink-0 flex-col border-l border-line bg-paper xl:flex" aria-labelledby="desktop-source-reader-title">
        <div className="flex shrink-0 items-center gap-3 border-b border-line px-8 py-7">
          <h2 id="desktop-source-reader-title" className="mr-auto font-display text-[1.85rem] tracking-[-0.025em] text-ink">Source reader</h2>
          <span className="whitespace-nowrap text-sm font-medium text-ink-soft">{selectedIndex + 1} of {sources.length}</span>
          <SourceNavigation selectedIndex={selectedIndex} sources={sources} onSelectSourceNumber={onSelectSourceNumber} />
          <button ref={desktopCloseButtonRef} type="button" onClick={onClose} aria-label="Close source reader" className="flex size-10 items-center justify-center rounded-lg text-ink transition hover:bg-stone hover:text-pomegranate">
            <X aria-hidden="true" className="size-5" strokeWidth={1.75} />
          </button>
        </div>
        <div className="source-reader-scrollbar min-h-0 flex-1 overflow-y-auto overscroll-contain px-8 py-8">
          <SourceReaderContent idPrefix="desktop" messageId={messageId} source={source} showSourceContextByDefault={showSourceContextByDefault} contextLabel="Source context" />
        </div>
      </aside>

      <div className="fixed inset-0 z-50 xl:hidden">
        <button type="button" tabIndex={-1} aria-label="Close source reader" onClick={onClose} className="source-reader-backdrop absolute inset-0 cursor-default bg-ink/55" />
        <section ref={mobileSheetRef} role="dialog" aria-modal="true" aria-labelledby="mobile-source-reader-title" className="source-reader-sheet absolute inset-x-0 bottom-0 flex h-[82dvh] min-h-[32rem] flex-col overflow-hidden rounded-t-[2rem] border-t border-line-strong bg-paper shadow-menu">
          <button type="button" aria-label="Drag down or tap to close source reader" onPointerDown={handleDragStart} onPointerMove={handleDragMove} onPointerUp={handleDragEnd} onPointerCancel={handleDragEnd} onClick={handleDragHandleClick} className="flex h-8 shrink-0 touch-none items-center justify-center">
            <span className="h-1.5 w-16 rounded-full bg-line-strong" aria-hidden="true" />
          </button>
          <div className="flex shrink-0 items-center gap-2 border-b border-line px-4 pb-4 sm:px-6">
            <h2 id="mobile-source-reader-title" className="min-w-0 flex-1 font-display text-[clamp(1.7rem,7vw,2.25rem)] leading-none tracking-[-0.03em] text-ink">Source reader</h2>
            <span className="whitespace-nowrap text-xs font-medium text-ink-soft sm:text-sm">{selectedIndex + 1} of {sources.length}</span>
            <SourceNavigation selectedIndex={selectedIndex} sources={sources} onSelectSourceNumber={onSelectSourceNumber} />
            <button ref={mobileCloseButtonRef} type="button" onClick={onClose} aria-label="Close source reader" className="flex size-10 shrink-0 items-center justify-center rounded-lg text-ink transition hover:bg-stone hover:text-pomegranate">
              <X aria-hidden="true" className="size-6" strokeWidth={1.75} />
            </button>
          </div>
          <div className="source-reader-scrollbar min-h-0 flex-1 overflow-y-auto overscroll-contain px-5 pb-[calc(1.5rem+env(safe-area-inset-bottom))] pt-6 sm:px-8">
            <SourceReaderContent idPrefix="mobile" messageId={messageId} source={source} showSourceContextByDefault={showSourceContextByDefault} contextLabel="Show source context" />
          </div>
        </section>
      </div>
    </>
  )
})

function SourceNavigation({ selectedIndex, sources, onSelectSourceNumber }: SourceNavigationProps) {
  const previousSource = sources[selectedIndex - 1]
  const nextSource = sources[selectedIndex + 1]

  return (
    <div className="flex shrink-0 gap-2" aria-label="Source navigation">
      <button type="button" disabled={previousSource === undefined} onClick={() => previousSource === undefined ? undefined : onSelectSourceNumber(previousSource.number)} aria-label="Previous source" className="flex size-10 items-center justify-center rounded-lg border border-line text-ink transition hover:border-pomegranate/60 hover:text-pomegranate disabled:cursor-not-allowed disabled:text-line-strong">
        <ChevronLeft aria-hidden="true" className="size-5" strokeWidth={1.8} />
      </button>
      <button type="button" disabled={nextSource === undefined} onClick={() => nextSource === undefined ? undefined : onSelectSourceNumber(nextSource.number)} aria-label="Next source" className="flex size-10 items-center justify-center rounded-lg border border-pomegranate/70 text-ink transition hover:bg-pomegranate hover:text-paper disabled:cursor-not-allowed disabled:border-line disabled:text-line-strong disabled:hover:bg-transparent">
        <ChevronRight aria-hidden="true" className="size-5" strokeWidth={1.8} />
      </button>
    </div>
  )
}

function SourceReaderContent({ idPrefix, messageId, source, showSourceContextByDefault, contextLabel }: SourceReaderContentProps) {
  const sourceContextKey = `${idPrefix}-${messageId}-${source.number}-${showSourceContextByDefault ? 'open' : 'closed'}`

  return (
    <div className="mx-auto w-full max-w-[34rem]">
      <div className="flex items-start gap-3">
        <span className="pt-1 text-sm font-bold text-pomegranate">[{source.number}]</span>
        <div className="min-w-0">
          <a href={source.sourceUrl} target="_blank" rel="noreferrer" className="group inline-flex items-start gap-2 font-semibold text-ink decoration-pomegranate/45 underline-offset-4 hover:text-pomegranate hover:underline">
            <span className="font-display text-[1.35rem] leading-7 sm:text-2xl">{source.canonicalReference}</span>
            <ExternalLink aria-hidden="true" className="mt-1 size-4 shrink-0 transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5" strokeWidth={1.8} />
            <span className="sr-only">{source.externalLinkLabel ?? 'Open source on Sefaria'}</span>
          </a>
          <p className="mt-2 text-sm leading-6 text-muted">{source.title} · {source.edition} · {source.language} · {source.license}</p>
        </div>
      </div>

      {source.quotations.length > 0 ? (
        <div className="mt-8">
          <div className="flex items-center gap-4 text-brass" aria-hidden="true">
            <span className="h-px flex-1 bg-brass/65" />
            <Quote className="size-7 fill-current" strokeWidth={1.4} />
            <span className="h-px flex-1 bg-brass/65" />
          </div>
          <div className="mt-7 space-y-6">
            {source.quotations.map((quotation, index) => (
              <blockquote key={`${source.number}-quotation-${index}`} className="font-display text-[clamp(1.35rem,2.2vw,1.8rem)] leading-[1.55] tracking-[-0.01em] text-ink">
                “{quotation}”
              </blockquote>
            ))}
          </div>
        </div>
      ) : null}

      <details key={sourceContextKey} open={showSourceContextByDefault} className="group mt-9 border-t border-line pt-6 text-sm text-ink-soft">
        <summary className="flex w-fit list-none items-center gap-2 font-semibold text-ink transition hover:text-pomegranate">
          <ChevronDown aria-hidden="true" className="size-4 transition-transform group-open:rotate-180" strokeWidth={1.9} />
          <span>{contextLabel}{source.isExcerpt ? ' (excerpt)' : ''}</span>
        </summary>
        <p className="mt-4 whitespace-pre-wrap border-l border-line-strong pl-4 leading-7">{source.context}</p>
      </details>
    </div>
  )
}
