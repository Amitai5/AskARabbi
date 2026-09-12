import { useState, type ComponentType } from 'react'
import { LoaderCircle, Printer } from 'lucide-react'
import type { PrintDialogProps } from './PrintDialog.tsx'
import type { PrintRequest } from './printTypes.ts'

export function PrintAction({ getRequest, label, iconOnly = false }: { getRequest(): PrintRequest; label: string; iconOnly?: boolean }) {
  const [preview, setPreview] = useState<{ Dialog: ComponentType<PrintDialogProps>; request: PrintRequest; trigger: HTMLButtonElement } | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  async function open(trigger: HTMLButtonElement) {
    setLoading(true)
    setError(null)
    try {
      const request = getRequest()
      const { PrintDialog } = await import('./PrintDialog.tsx')
      setPreview({ Dialog: PrintDialog, request, trigger })
    } catch { setError('The print preview could not open. Refresh the page and try again.') }
    finally { setLoading(false) }
  }
  return <>
    <button type="button" onClick={event => void open(event.currentTarget)} disabled={loading} aria-label={label} title={label} className={`inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-lg text-sm font-semibold text-ink-soft transition hover:bg-stone hover:text-pomegranate disabled:opacity-50 ${iconOnly ? 'w-11' : 'border border-line-strong bg-paper px-3'}`}>
      {loading ? <LoaderCircle aria-hidden="true" className="size-4 animate-spin" /> : <Printer aria-hidden="true" className="size-4" />}{iconOnly ? null : label}
    </button>
    {error ? <p className="text-sm text-pomegranate" role="alert">{error}</p> : null}
    {preview ? <preview.Dialog request={preview.request} returnFocusTo={preview.trigger} onClose={() => setPreview(null)} /> : null}
  </>
}
