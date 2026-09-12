import { useEffect, useRef, useState, type ComponentType } from 'react'
import type { PrintDialogProps } from './PrintDialog.tsx'
import type { PrintRequest } from './printTypes.ts'

export function usePrintPreview() {
  const [preview, setPreview] = useState<{ Dialog: ComponentType<PrintDialogProps>; request: PrintRequest; trigger: HTMLElement } | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const generation = useRef(0)

  useEffect(() => () => { generation.current += 1 }, [])

  async function open(getRequest: () => PrintRequest | Promise<PrintRequest>, trigger: HTMLElement) {
    const requestGeneration = ++generation.current
    setLoading(true)
    setError(null)
    setPreview(null)
    try {
      const [request, { PrintDialog }] = await Promise.all([getRequest(), import('./PrintDialog.tsx')])
      if (generation.current === requestGeneration) { setPreview({ Dialog: PrintDialog, request, trigger }) }
    } catch {
      if (generation.current === requestGeneration) { setError('The print preview could not open. Please try again.') }
    } finally {
      if (generation.current === requestGeneration) { setLoading(false) }
    }
  }

  function close() {
    generation.current += 1
    setPreview(null)
    setLoading(false)
    setError(null)
  }

  return { preview, loading, error, open, close }
}
