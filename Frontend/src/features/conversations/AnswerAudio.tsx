import { useEffect, useEffectEvent, useRef, useState } from 'react'
import { VoiceActivityEvent, VoiceClient, type VoiceClient as VoiceClientContract } from './voiceClient.ts'

interface AnswerAudioProps {
  conversationId: string
  messageId: string
  autoPlay?: boolean
  client?: VoiceClientContract
}

export function AnswerAudio({ conversationId, messageId, autoPlay = false, client = VoiceClient }: AnswerAudioProps) {
  const audio = useRef<HTMLAudioElement>(null)
  const request = useRef<AbortController | null>(null)
  const url = useRef<string | null>(null)
  const [source, setSource] = useState<string | null>(null)
  const [loading, setLoading] = useState(autoPlay)
  const [notice, setNotice] = useState<string | null>(null)

  async function prepare() {
    if (request.current || url.current) { return }
    window.dispatchEvent(new CustomEvent(VoiceActivityEvent, { detail: audio.current }))
    const current = new AbortController()
    request.current = current
    try {
      const blob = await client.synthesize(conversationId, messageId, current.signal)
      current.signal.throwIfAborted()
      const next = URL.createObjectURL(blob)
      url.current = next
      setSource(next)
    } catch (error) {
      if (!current.signal.aborted) { setNotice(error instanceof Error ? error.message : 'Audio is unavailable. Read the answer and sources below.') }
    } finally {
      if (request.current === current) { request.current = null; setLoading(false) }
    }
  }
  const prepareCurrent = useEffectEvent(prepare)
  useEffect(() => {
    const stop = (event: Event) => {
      if (event instanceof CustomEvent && event.detail === audio.current) { return }
      request.current?.abort()
      request.current = null
      setLoading(false)
      if (audio.current && !audio.current.paused) { audio.current.pause() }
    }
    window.addEventListener(VoiceActivityEvent, stop)
    const player = audio.current
    return () => {
      window.removeEventListener(VoiceActivityEvent, stop)
      request.current?.abort()
      request.current = null
      if (player && !player.paused) { player.pause() }
      if (url.current) { URL.revokeObjectURL(url.current) }
    }
  }, [])
  // oxlint-disable-next-line react/set-state-in-effect -- Starts external speech IO; state changes occur on completion.
  useEffect(() => { if (autoPlay) { void prepareCurrent() } }, [autoPlay])
  useEffect(() => {
    if (!source) { return }
    void audio.current?.play().catch(() => setNotice('Your answer is ready to listen to. Press Play.'))
  }, [source])

  return (
    <div className="my-3 text-sm text-muted" aria-label="Spoken answer">
      <audio ref={audio} hidden={!source} src={source ?? undefined} controls preload="metadata" aria-label="Listen to answer" className="max-w-full"
        onPlay={() => window.dispatchEvent(new CustomEvent(VoiceActivityEvent, { detail: audio.current }))}
        onError={() => { setNotice('Audio could not play. You can still read the answer and open its sources.'); if (url.current) { URL.revokeObjectURL(url.current) }; url.current = null; setSource(null) }} />
      {!source ? <button type="button" disabled={loading} onClick={() => { setLoading(true); setNotice(null); void prepare() }} className="min-h-11 rounded-lg border border-line-strong px-3 text-ink hover:bg-stone focus-visible:outline-2 focus-visible:outline-pomegranate disabled:opacity-50">{loading ? 'Preparing audio…' : 'Listen to answer'}</button> : null}
      {loading ? <button type="button" onClick={() => window.dispatchEvent(new Event(VoiceActivityEvent))} className="min-h-11 px-3 underline">Cancel audio</button> : null}
      {notice ? <p role="status" className="mt-1">{notice}</p> : null}
    </div>
  )
}
