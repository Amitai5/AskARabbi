import { useEffect, useEffectEvent, useId, useLayoutEffect, useRef, useState } from 'react'
import { LoaderCircle, Mic, Square, X } from 'lucide-react'
import { canRecordQuestion, startQuestionRecording, type QuestionRecording } from './questionRecording.ts'
import { SpokenLanguages, VoiceActivityEvent, VoiceClient, type VoiceClient as VoiceClientContract } from './voiceClient.ts'

interface VoiceInputProps {
  draft: string
  language: string
  disabled: boolean
  onTranscript(draft: string): void
  onBusyChange(busy: boolean): void
  client?: VoiceClientContract
}

export function VoiceInput({ draft, language, disabled, onTranscript, onBusyChange, client = VoiceClient }: VoiceInputProps) {
  const [phase, setPhase] = useState<'idle' | 'starting' | 'recording' | 'transcribing'>('idle')
  const [notice, setNotice] = useState<{ message: string; isError: boolean } | null>(null)
  const helpId = useId()
  const buttonRef = useRef<HTMLButtonElement>(null)
  const request = useRef<AbortController | null>(null)
  const recording = useRef<QuestionRecording | null>(null)
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const latestDraft = useRef(draft)
  useLayoutEffect(() => { latestDraft.current = draft }, [draft])
  const locale = SpokenLanguages[language]
  const supported = canRecordQuestion()
  const isPending = phase === 'starting' || phase === 'transcribing'
  const label = phase === 'recording' ? 'Stop recording' : phase === 'starting' ? 'Opening microphone' : phase === 'transcribing' ? 'Transcribing question' : 'Record question'
  const unavailableReason = !supported ? 'Voice recording is unavailable in this browser. You can still type.' : !locale ? `Voice input is unavailable in ${language}. You can still type.` : null
  const status = phase === 'recording' ? 'Recording… Select Stop recording when you finish.' : phase === 'starting' ? 'Opening microphone…' : phase === 'transcribing' ? 'Transcribing your question…' : notice?.message

  function clearTimer() {
    if (timer.current !== null) { clearTimeout(timer.current); timer.current = null }
  }
  function cancel() {
    request.current?.abort()
    request.current = null
    recording.current?.cancel()
    recording.current = null
    clearTimer()
    setPhase('idle')
    onBusyChange(false)
  }
  const cancelCurrent = useEffectEvent(cancel)
  useEffect(() => {
    const stop = () => cancelCurrent()
    const hide = () => { if (document.hidden) { stop() } }
    window.addEventListener(VoiceActivityEvent, stop)
    window.addEventListener('pagehide', stop)
    window.addEventListener('offline', stop)
    document.addEventListener('visibilitychange', hide)
    return () => {
      window.removeEventListener(VoiceActivityEvent, stop)
      window.removeEventListener('pagehide', stop)
      window.removeEventListener('offline', stop)
      document.removeEventListener('visibilitychange', hide)
      request.current?.abort()
      recording.current?.cancel()
      if (timer.current !== null) { clearTimeout(timer.current) }
      onBusyChange(false)
    }
  }, [onBusyChange])

  async function finish() {
    const current = request.current
    const active = recording.current
    if (!current || !active || !locale) { return }
    recording.current = null
    clearTimer()
    setPhase('transcribing')
    try {
      const pcm = await active.stop()
      current.signal.throwIfAborted()
      const text = await client.transcribe(pcm, locale, current.signal)
      current.signal.throwIfAborted()
      const next = [latestDraft.current.trimEnd(), text.trim()].filter(Boolean).join('\n')
      if (next.length > 4000) { throw new Error('Your draft is too long to add this recording. Shorten it and record again; your text has been kept.') }
      if (!text.trim()) { throw new Error('No speech was recognized. Try again or type your question.') }
      onTranscript(next)
      setNotice({ message: 'Question added. Review or edit it, then send. The answer will be read aloud.', isError: false })
    } catch (error) {
      if (!current.signal.aborted) { setNotice({ message: error instanceof Error ? error.message : 'Voice could not transcribe the recording. You can still type.', isError: true }) }
    } finally {
      if (request.current === current) {
        request.current = null
        setPhase('idle')
        onBusyChange(false)
      }
    }
  }

  async function start() {
    if (request.current || disabled || !supported || !locale) { return }
    window.dispatchEvent(new Event(VoiceActivityEvent))
    const current = new AbortController()
    request.current = current
    setNotice(null)
    setPhase('starting')
    onBusyChange(true)
    try {
      if (!await client.isAvailable(current.signal)) { throw new Error('Voice is not enabled right now. You can still type your question.') }
      current.signal.throwIfAborted()
      const active = await startQuestionRecording(current.signal)
      if (current.signal.aborted) { active.cancel(); return }
      recording.current = active
      setPhase('recording')
      timer.current = setTimeout(() => { void finish() }, 30_000)
    } catch (error) {
      if (!current.signal.aborted) {
        setNotice({ message: error instanceof DOMException && error.name === 'NotAllowedError'
          ? 'Microphone permission was denied. Allow it in your browser or type your question.'
          : error instanceof Error ? error.message : 'The microphone is unavailable. You can still type.', isError: true })
        cancel()
      }
    }
  }

  return (
    <div className="relative flex shrink-0 items-center gap-1">
      {phase !== 'idle' ? (
        <button type="button" onClick={() => { cancel(); setNotice({ message: 'Recording cancelled. Your draft has been kept.', isError: false }) }} aria-label="Cancel recording" title="Cancel recording" className="flex size-11 items-center justify-center rounded-full text-muted transition hover:bg-stone hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate sm:size-9">
          <X className="size-4" aria-hidden="true" />
        </button>
      ) : null}
      <button ref={buttonRef} type="button" onClick={() => { if (phase === 'recording') { void finish() } else { void start() } }}
        disabled={disabled || !supported || !locale || isPending}
        aria-label={label} title={unavailableReason ?? label} aria-busy={isPending}
        aria-pressed={phase === 'recording'} aria-describedby={unavailableReason ? helpId : undefined}
        className={`flex size-11 items-center justify-center rounded-full transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate disabled:cursor-not-allowed disabled:opacity-50 sm:size-9 ${phase === 'recording' ? 'bg-pomegranate text-white hover:bg-pomegranate-dark' : 'text-ink-soft hover:bg-stone hover:text-ink'}`}>
        {isPending ? <LoaderCircle className="size-4 motion-safe:animate-spin" aria-hidden="true" /> : phase === 'recording' ? <Square className="size-3.5 fill-current" aria-hidden="true" /> : <Mic className="size-4" strokeWidth={1.9} aria-hidden="true" />}
      </button>
      {unavailableReason ? <span id={helpId} className="sr-only">{unavailableReason}</span> : null}
      {notice?.isError ? (
        <div role="alert" className="absolute bottom-full right-0 z-20 mb-2 flex w-80 max-w-[calc(100vw-6rem)] items-start gap-2 rounded-lg border border-pomegranate/25 bg-paper py-2 pl-3 pr-2 text-sm leading-5 text-pomegranate shadow-lg">
          <p className="min-w-0 flex-1" dir="auto">{notice.message}</p>
          <button type="button" aria-label="Dismiss voice error" title="Dismiss voice error" onClick={() => { setNotice(null); buttonRef.current?.focus() }} className="flex size-8 shrink-0 items-center justify-center rounded-full hover:bg-stone focus-visible:outline-2 focus-visible:outline-pomegranate">
            <X className="size-4" aria-hidden="true" />
          </button>
        </div>
      ) : <p role="status" className="sr-only" dir="auto">{status}</p>}
    </div>
  )
}
