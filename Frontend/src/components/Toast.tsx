import { useEffect } from 'react'
import { CheckCircle2, X } from 'lucide-react'

interface ToastProps {
  notificationId: number
  title: string
  message: string
  onDismiss(): void
}

export function Toast({ notificationId, title, message, onDismiss }: ToastProps) {
  useEffect(() => {
    const timeoutId = window.setTimeout(onDismiss, 5_000)
    return () => window.clearTimeout(timeoutId)
  }, [notificationId, onDismiss])

  return (
    <div className="enter-softly fixed left-4 right-4 top-20 z-50 flex items-start gap-3 rounded-xl border border-line-strong bg-paper px-4 py-3.5 shadow-menu sm:left-auto sm:right-6 sm:w-[22rem]" role="status" aria-live="polite">
      <CheckCircle2 aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-pomegranate" strokeWidth={1.9} />
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold text-ink">{title}</p>
        <p className="mt-1 text-xs leading-5 text-muted">{message}</p>
      </div>
      <button type="button" onClick={onDismiss} className="flex size-8 shrink-0 items-center justify-center rounded-lg text-muted transition hover:bg-stone hover:text-ink" aria-label="Dismiss notification">
        <X aria-hidden="true" className="size-4" strokeWidth={1.75} />
      </button>
    </div>
  )
}
