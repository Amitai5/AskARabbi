import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { VoiceInput } from './VoiceInput.tsx'
import { ArrowUp } from 'lucide-react'
import { LegalLinks } from '../legal/LegalLinks.tsx'
import { SourceFilterMenu } from './SourceFilterMenu.tsx'

interface MessageComposerProps {
  focusKey?: number
  voiceScope?: string
  draft: string
  selectedSourceKeys: readonly string[]
  conversationLanguage: string
  quotationLanguage: string
  isSending: boolean
  isChatDisabled?: boolean
  enterSendsMessage?: boolean
  onDraftChange(value: string): void
  onSelectedSourceKeysChange(sourceKeys: string[]): void
  onVoiceDraft?(): void
  onSubmit(): void
}

export function MessageComposer({ focusKey = 0, voiceScope = 'new', draft, selectedSourceKeys, conversationLanguage, quotationLanguage, isSending, isChatDisabled = false, enterSendsMessage = false, onDraftChange, onSelectedSourceKeysChange, onSubmit, onVoiceDraft }: MessageComposerProps) {
  const [voiceBusy, setVoiceBusy] = useState(false)
  const formRef = useRef<HTMLFormElement>(null)
  const inputRef = useRef<HTMLTextAreaElement>(null)
  useEffect(() => {
    if (focusKey > 0) { inputRef.current?.focus(); inputRef.current?.setSelectionRange(inputRef.current.value.length, inputRef.current.value.length) }
  }, [focusKey])

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!voiceBusy && !isChatDisabled && !isSending && draft.trim().length > 0 && selectedSourceKeys.length > 0) {
      onSubmit()
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key !== 'Enter' || event.nativeEvent.isComposing || event.nativeEvent.keyCode === 229 || event.altKey) { return }
    if (event.ctrlKey || event.metaKey || (enterSendsMessage && !event.shiftKey)) {
      event.preventDefault()
      if (!event.repeat) { formRef.current?.requestSubmit() }
    }
  }

  return (
    <div className="w-full max-w-[50rem] text-base leading-6 lg:text-lg">
      <form ref={formRef} onSubmit={handleSubmit} className="rounded-2xl border border-line-strong bg-paper p-2.5 shadow-[0_10px_30px_rgb(16_35_63_/_0.06)] transition focus-within:border-pomegranate focus-within:ring-3 focus-within:ring-pomegranate/10 sm:p-3">
        <label htmlFor="message" className="sr-only">Message AskRabbi</label>
        <textarea
          ref={inputRef}
          id="message"
          value={draft}
          readOnly={isChatDisabled}
          aria-disabled={isChatDisabled}
          aria-describedby={`message-keyboard-help${isChatDisabled ? ' chat-availability-notice' : ''}`}
          onChange={(event) => onDraftChange(event.target.value)}
          onKeyDown={handleKeyDown}
          rows={1}
          maxLength={4000}
          placeholder="Ask about Jewish learning…"
          className="message-composer-input field-sizing-content max-h-40 min-h-10 w-full resize-none overflow-y-auto bg-transparent px-2 py-2 text-ink outline-none placeholder:text-muted/80"
        />
        <div className="flex items-center justify-between gap-2 pt-1">
          <div className="flex min-w-0 flex-1 items-center gap-2">
            <SourceFilterMenu key={isSending || isChatDisabled ? 'source-filter-disabled' : 'source-filter-ready'} selectedSourceKeys={selectedSourceKeys} isDisabled={isSending || isChatDisabled} onChange={onSelectedSourceKeysChange} />
            <span className="hidden truncate text-sm leading-4 text-muted sm:inline">{conversationLanguage} · quotes in {quotationLanguage}</span>
          </div>
          <div className="flex shrink-0 items-center gap-1">
            <VoiceInput key={`${voiceScope}:${isChatDisabled || isSending ? 'disabled' : 'ready'}`} draft={draft} language={conversationLanguage} disabled={isChatDisabled || isSending} onBusyChange={setVoiceBusy} onTranscript={value => { onDraftChange(value); onVoiceDraft?.(); inputRef.current?.focus() }} />
            <button type="submit" disabled={voiceBusy || isChatDisabled || isSending || draft.trim().length === 0 || selectedSourceKeys.length === 0} className="flex size-11 items-center justify-center rounded-full bg-pomegranate text-white transition hover:bg-pomegranate-dark focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate disabled:cursor-not-allowed disabled:bg-stone-deep disabled:text-muted sm:size-9" aria-label="Send message" title="Send message (Ctrl/Cmd+Enter)" aria-keyshortcuts={enterSendsMessage ? 'Enter Control+Enter Meta+Enter' : 'Control+Enter Meta+Enter'}>
              <ArrowUp aria-hidden="true" className="size-4" strokeWidth={1.9} />
            </button>
          </div>
        </div>
        {selectedSourceKeys.length === 0 ? <p className="px-2 pt-2 text-sm font-medium leading-4 text-pomegranate" role="alert">Select at least one source before sending.</p> : null}
      </form>
      <div className="mt-1.5 text-center text-sm leading-5 text-muted">
        <p id="message-keyboard-help" className="sr-only">{enterSendsMessage ? 'Enter to send · Shift+Enter for a new line · Ctrl/Cmd+Enter also sends' : 'Enter for a new line · Ctrl/Cmd+Enter to send'}</p>
        <p>AskRabbi can make mistakes. Check the cited sources. <LegalLinks /></p>
      </div>
    </div>
  )
}
