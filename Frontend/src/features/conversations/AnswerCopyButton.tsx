import { Check, ClipboardList, Copy } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { formatAnswerForClipboard, type AnswerCopyMode } from './answerCopy.ts'
import type { ConversationMessage } from './conversationData.ts'

interface AnswerCopyButtonProps {
  message: ConversationMessage
  mode: AnswerCopyMode
}

type CopyStatus = 'idle' | 'copying' | 'copied' | 'failed'

export function AnswerCopyButton({ message, mode }: AnswerCopyButtonProps) {
  const [status, setStatus] = useState<CopyStatus>('idle')
  const pending = useRef(false)
  const label = mode === 'text' ? 'Copy text' : 'Copy with sources'
  const description = mode === 'text'
    ? 'Copy the answer text only.'
    : 'Copy the answer with numbered sources, links, quotations, and attribution.'
  const copiedMessage = mode === 'text' ? 'Answer text copied to clipboard.' : 'Answer with sources copied to clipboard.'
  const failedMessage = `${label} failed. Try again.`
  const feedback = status === 'copied' ? copiedMessage : status === 'failed' ? failedMessage : status === 'copying' ? 'Copying…' : ''
  const Icon = status === 'copied' ? Check : mode === 'text' ? Copy : ClipboardList

  useEffect(() => {
    if (status !== 'copied' && status !== 'failed') {
      return
    }

    const resetTimeout = window.setTimeout(() => setStatus('idle'), 2_000)
    return () => window.clearTimeout(resetTimeout)
  }, [status])

  async function handleCopy() {
    if (pending.current) {
      return
    }

    pending.current = true
    setStatus('copying')
    try {
      await navigator.clipboard.writeText(formatAnswerForClipboard(message, mode))
      setStatus('copied')
    } catch {
      setStatus('failed')
    } finally {
      pending.current = false
    }
  }

  return (
    <>
      <button type="button" aria-label={label} aria-description={description} aria-disabled={status === 'copying'} title={feedback || description} data-copy-status={status} onClick={() => void handleCopy()} className="answer-copy-button inline-flex size-8 items-center justify-center rounded-md border border-line bg-paper text-muted shadow-sm transition hover:border-line-strong hover:bg-stone hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-pomegranate/55 motion-reduce:transition-none">
        <Icon aria-hidden="true" className={status === 'copied' ? 'size-4 text-pomegranate' : 'size-4'} strokeWidth={1.8} />
      </button>
      <span className="sr-only" role="status" aria-live="polite">{feedback}</span>
    </>
  )
}
