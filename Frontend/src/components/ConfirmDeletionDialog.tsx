import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { createPortal } from 'react-dom'
import { LoaderCircle, Trash2 } from 'lucide-react'

interface ConfirmDeletionDialogProps {
  title: string
  description: string
  confirmLabel: string
  confirmationPhrase?: string
  onConfirm(): Promise<void>
  onClose(): void
}

export function ConfirmDeletionDialog({ title, description, confirmLabel, confirmationPhrase, onConfirm, onClose }: ConfirmDeletionDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const cancelRef = useRef<HTMLButtonElement>(null)
  const submittingRef = useRef(false)
  const [confirmation, setConfirmation] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const id = useId()

  useEffect(() => {
    const dialog = dialogRef.current
    const trigger = document.activeElement
    dialog?.showModal()
    cancelRef.current?.focus()
    return () => {
      dialog?.close()
      if (trigger instanceof HTMLElement && trigger.isConnected) {
        trigger.focus()
      }
    }
  }, [])

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (submittingRef.current || (confirmationPhrase && confirmation !== confirmationPhrase)) {
      return
    }
    submittingRef.current = true
    setIsSubmitting(true)
    setError(null)
    try {
      await onConfirm()
      onClose()
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Deletion could not be completed. Please try again.')
    } finally {
      submittingRef.current = false
      setIsSubmitting(false)
    }
  }

  return createPortal(
    <dialog ref={dialogRef} aria-labelledby={`${id}-title`} aria-describedby={`${id}-description`} className="deletion-dialog m-auto max-h-[calc(100dvh-2rem)] w-[min(28rem,calc(100vw-2rem))] overflow-y-auto rounded-2xl border border-line bg-paper p-6 text-ink shadow-menu sm:p-7" onCancel={(event) => { event.preventDefault(); if (!submittingRef.current) { onClose() } }}>
      <form onSubmit={(event) => void submit(event)} aria-busy={isSubmitting}>
        <span className="mb-4 inline-flex size-11 items-center justify-center rounded-full bg-pomegranate/10 text-pomegranate"><Trash2 aria-hidden="true" className="size-5" /></span>
        <h2 id={`${id}-title`} className="break-words font-display text-2xl leading-tight">{title}</h2>
        <p id={`${id}-description`} className="mt-3 text-base leading-7 text-ink-soft">{description}</p>
        {confirmationPhrase ? <label className="mt-5 block text-sm font-semibold" htmlFor={`${id}-confirmation`}>
          Type <span className="select-all">{confirmationPhrase}</span> to confirm
          <input id={`${id}-confirmation`} autoComplete="off" spellCheck={false} value={confirmation} disabled={isSubmitting} onChange={(event) => setConfirmation(event.target.value)} className="mt-2 h-11 w-full rounded-lg border border-line-strong bg-parchment px-3 font-normal" />
        </label> : null}
        {error ? <p className="mt-4 text-sm leading-6 text-pomegranate" role="alert">{error}</p> : null}
        <div className="mt-6 flex flex-wrap justify-end gap-3">
          <button ref={cancelRef} type="button" onClick={onClose} disabled={isSubmitting} className="min-h-11 rounded-lg border border-line-strong px-4 text-sm font-semibold transition hover:bg-stone disabled:opacity-50">Cancel</button>
          <button type="submit" disabled={isSubmitting || Boolean(confirmationPhrase && confirmation !== confirmationPhrase)} className="inline-flex min-h-11 items-center justify-center gap-2 rounded-lg bg-pomegranate px-4 text-sm font-semibold text-white transition hover:bg-pomegranate-dark disabled:cursor-not-allowed disabled:opacity-50">
            {isSubmitting ? <LoaderCircle aria-hidden="true" className="size-4 animate-spin" /> : null}
            {isSubmitting ? 'Deleting…' : confirmLabel}
          </button>
        </div>
      </form>
    </dialog>, document.body,
  )
}
