import { LoaderCircle, Printer } from 'lucide-react'
import type { PrintRequest } from './printTypes.ts'
import { usePrintPreview } from './usePrintPreview.ts'

export function PrintAction({ getRequest, label, iconOnly = false, compact = false, className = '' }: { getRequest(): PrintRequest; label: string; iconOnly?: boolean; compact?: boolean; className?: string }) {
  const { preview, loading, error, open, close } = usePrintPreview()
  const buttonClass = compact
    ? 'size-8 rounded-md border border-line bg-paper text-muted shadow-sm hover:border-line-strong'
    : `min-h-11 rounded-lg text-ink-soft ${iconOnly ? 'w-11' : 'border border-line-strong bg-paper px-3'}`
  return <>
    <button type="button" onClick={event => void open(getRequest, event.currentTarget)} disabled={loading} aria-label={label} title={label} className={`inline-flex shrink-0 items-center justify-center gap-2 text-sm font-semibold transition hover:bg-stone hover:text-pomegranate focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate disabled:opacity-50 ${buttonClass} ${className}`}>
      {loading ? <LoaderCircle aria-hidden="true" className="size-4 animate-spin" /> : <Printer aria-hidden="true" className="size-4" />}{iconOnly ? null : label}
    </button>
    {error ? <p className={`text-sm text-pomegranate ${compact ? 'absolute bottom-full right-0 mb-2 w-64 rounded-lg border border-line bg-paper p-3 shadow-menu' : ''}`} role="alert">{error}</p> : null}
    {preview ? <preview.Dialog request={preview.request} returnFocusTo={preview.trigger} onClose={close} /> : null}
  </>
}
