import { useEffect, useEffectEvent, useLayoutEffect, useRef, useState } from 'react'
import { Mic, Square } from 'lucide-react'
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
  const [notice, setNotice] = useState<string | null>(null)
  const request = useRef<AbortController | null>(null)
  const recording = useRef<QuestionRecording | null>(null)
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const latestDraft = useRef(draft)
  useLayoutEffect(() => { latestDraft.current = draft }, [draft])
  const locale = SpokenLanguages[language]
  const supported = canRecordQuestion()

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
      setNotice('Question added. Review or edit it, then send. The answer will be read aloud.')
    } catch (error) {
      if (!current.signal.aborted) { setNotice(error instanceof Error ? error.message : 'Voice could not transcribe the recording. You can still type.') }
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
        setNotice(error instanceof DOMException && error.name === 'NotAllowedError'
          ? 'Microphone permission was denied. Allow it in your browser or type your question.'
          : error instanceof Error ? error.message : 'The microphone is unavailable. You can still type.')
        cancel()
      }
    }
  }

  return (
    <div className="w-full px-2 pt-2 text-sm text-muted">
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" onClick={() => { if (phase === 'recording') { void finish() } else { void start() } }}
          disabled={disabled || !supported || !locale || phase === 'starting' || phase === 'transcribing'}
          aria-pressed={phase === 'recording'} aria-describedby="voice-help"
          className="inline-flex min-h-11 items-center gap-2 rounded-lg border border-line-strong px-3 text-ink hover:bg-stone focus-visible:outline-2 focus-visible:outline-pomegranate disabled:opacity-50">
          {phase === 'recording' ? <Square className="size-4" aria-hidden="true" /> : <Mic className="size-4" aria-hidden="true" />}
          {phase === 'recording' ? 'Stop recording' : 'Record question'}
        </button>
        {phase !== 'idle' ? <button type="button" onClick={() => { cancel(); setNotice('Recording cancelled. Your draft has been kept.') }} className="min-h-11 rounded-lg px-3 text-ink underline">Cancel recording</button> : null}
        <span id="voice-help">{!supported ? 'Voice recording is unavailable in this browser. You can still type.' : !locale ? `Voice input is unavailable in ${language}. You can still type.` : `Speak in ${language} · up to 30 seconds`}</span>
      </div>
      {phase !== 'idle' || notice ? <p role="status" className="mt-1" dir="auto">{phase === 'recording' ? 'Recording… Select Stop recording when you finish.' : phase === 'starting' ? 'Opening microphone…' : phase === 'transcribing' ? 'Transcribing your question…' : notice}</p> : null}
      <p className="mt-1">Record only when you choose. Audio goes to Azure Speech for transcription; review the text before sending. <a className="underline" href="/privacy-policy#information" target="_blank" rel="noreferrer">Voice privacy</a></p>
    </div>
  )
}
