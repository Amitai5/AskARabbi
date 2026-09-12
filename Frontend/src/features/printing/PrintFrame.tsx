import { useEffect, useState, type RefObject } from 'react'
import { createPortal } from 'react-dom'
import { PrintDocument } from './PrintDocument.tsx'
import type { PrintOptions, PrintRequest } from './printTypes.ts'
import type { CalendarEvent, CalendarRange } from '../calendar/calendarTypes.ts'
import printStyles from './printDocument.css?inline'

// The frame has no scripts or remote resources. React inserts escaped content from the parent.
const FrameDocument = `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'"><meta name="viewport" content="width=device-width, initial-scale=1"><title>AskRabbi study copy</title><style>${printStyles}</style></head><body></body></html>`

interface Props { frameRef: RefObject<HTMLIFrameElement | null>; request: PrintRequest; options: PrintOptions; selectedIds: ReadonlySet<string>; events: readonly CalendarEvent[]; days: CalendarRange; onReady(ready: boolean): void; onClose(): void }

export function PrintFrame({ frameRef, request, options, selectedIds, events, days, onReady, onClose }: Props) {
  const [document, setDocument] = useState<Document | null>(null)
  useEffect(() => {
    if (!document) { return }
    // Keyboard events inside the preview do not reach the parent dialog's native cancel handler.
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key !== 'Escape' || event.isComposing) { return }
      event.preventDefault()
      event.stopPropagation()
      onClose()
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [document, onClose])

  useEffect(() => {
    if (!document) { return }
    let active = true
    let frame = 0
    onReady(false)
    void Promise.resolve(document.fonts?.ready).then(() => {
      if (active) { frame = requestAnimationFrame(() => { if (active) { onReady(true) } }) }
    })
    return () => { active = false; cancelAnimationFrame(frame) }
  }, [document, request, onReady])

  return <>
    <iframe ref={frameRef} title="Study copy preview" srcDoc={FrameDocument} onLoad={event => {
      const frameDocument = event.currentTarget.contentDocument
      if (!frameDocument) { return }
      frameDocument.title = `${request.kind === 'answers' ? request.title : request.kind === 'teaching' ? request.article.title : 'Jewish Calendar'} - AskARabbi.ai`
      setDocument(frameDocument)
    }} className="h-full min-h-0 w-full border-0 bg-white" />
    {document ? createPortal(<style>{`@page { size: ${options.paper === 'a4' ? 'A4' : 'letter'}; margin: 16mm 16mm 22mm; @bottom-left { content: counter(page); font: 8pt Arial, sans-serif; color: #65717a; } }`}</style>, document.head) : null}
    {document ? createPortal(<PrintDocument request={request} options={options} selectedIds={selectedIds} events={events} days={days} />, document.body) : null}
  </>
}
