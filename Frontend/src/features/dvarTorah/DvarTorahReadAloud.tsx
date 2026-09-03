import { useEffect, useMemo, useRef, useState } from 'react'
import { Pause, Play, Square, Volume2 } from 'lucide-react'
import { createSpeechChunks } from './dvarTorahSpeech.ts'

interface DvarTorahReadAloudProps {
  title: string
  body: string
}

type PlaybackState = 'idle' | 'playing' | 'paused'

export function DvarTorahReadAloud({ title, body }: DvarTorahReadAloudProps) {
  const speechChunks = useMemo(() => createSpeechChunks(title, body), [body, title])
  const [playbackState, setPlaybackState] = useState<PlaybackState>('idle')
  const [speechError, setSpeechError] = useState<string | null>(null)
  const utteranceRef = useRef<SpeechSynthesisUtterance | null>(null)
  const playbackIdRef = useRef(0)
  const isSupported = typeof window !== 'undefined'
    && 'speechSynthesis' in window
    && typeof SpeechSynthesisUtterance !== 'undefined'
  const speechSynthesis = isSupported ? window.speechSynthesis : null

  useEffect(() => {
    return () => {
      playbackIdRef.current += 1
      detachUtterance(utteranceRef.current)
      utteranceRef.current = null
      speechSynthesis?.cancel()
    }
  }, [speechChunks, speechSynthesis])

  function togglePlayback() {
    if (speechSynthesis === null) {
      return
    }
    if (playbackState === 'playing') {
      speechSynthesis.pause()
      setPlaybackState('paused')
      return
    }
    if (playbackState === 'paused') {
      speechSynthesis.resume()
      setPlaybackState('playing')
      return
    }

    startPlayback()
  }

  function startPlayback() {
    playbackIdRef.current += 1
    const playbackId = playbackIdRef.current
    detachUtterance(utteranceRef.current)
    speechSynthesis?.cancel()

    if (speechSynthesis === null) {
      return
    }

    setSpeechError(null)
    setPlaybackState('playing')
    speakChunk(playbackId, 0)
  }

  function speakChunk(playbackId: number, chunkIndex: number) {
    if (speechSynthesis === null || playbackId !== playbackIdRef.current) {
      return
    }
    if (chunkIndex >= speechChunks.length) {
      finishPlayback(playbackId)
      return
    }

    const utterance = new SpeechSynthesisUtterance(speechChunks[chunkIndex])
    utterance.lang = 'en-US'
    utterance.rate = 0.95
    utterance.onend = () => {
      utteranceRef.current = null
      speakChunk(playbackId, chunkIndex + 1)
    }
    utterance.onerror = (event) => {
      if (playbackId !== playbackIdRef.current || event.error === 'canceled' || event.error === 'interrupted') {
        return
      }

      utteranceRef.current = null
      setPlaybackState('idle')
      setSpeechError('Your browser could not read this teaching aloud.')
    }
    utteranceRef.current = utterance
    speechSynthesis.speak(utterance)
  }

  function finishPlayback(playbackId: number) {
    if (playbackId !== playbackIdRef.current) {
      return
    }

    utteranceRef.current = null
    setPlaybackState('idle')
  }

  function stopPlayback() {
    if (speechSynthesis === null) {
      return
    }

    playbackIdRef.current += 1
    detachUtterance(utteranceRef.current)
    utteranceRef.current = null
    speechSynthesis.cancel()
    setPlaybackState('idle')
  }

  const isIdle = playbackState === 'idle'
  const primaryLabel = !isSupported ? 'Read aloud unavailable' : playbackState === 'playing' ? 'Pause reading' : playbackState === 'paused' ? 'Resume reading' : 'Read aloud'
  const PrimaryIcon = playbackState === 'playing' ? Pause : playbackState === 'paused' ? Play : Volume2

  return (
    <div className="mt-6">
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" disabled={!isSupported} onClick={togglePlayback} aria-label={primaryLabel === 'Read aloud' ? 'Read this Dvar Torah aloud' : primaryLabel} className="inline-flex min-h-11 items-center gap-2 rounded-full border border-line-strong bg-paper px-4 text-sm font-semibold text-ink transition hover:border-pomegranate/45 hover:text-pomegranate disabled:cursor-not-allowed disabled:opacity-55">
          <PrimaryIcon aria-hidden="true" className="size-4" strokeWidth={1.8} />
          {primaryLabel}
        </button>
        {isIdle ? null : (
          <button type="button" onClick={stopPlayback} className="inline-flex min-h-11 items-center gap-2 rounded-full px-3 text-sm font-semibold text-ink-soft transition hover:bg-stone hover:text-pomegranate" aria-label="Stop reading">
            <Square aria-hidden="true" className="size-3.5" fill="currentColor" strokeWidth={1.5} />
            Stop
          </button>
        )}
      </div>
      <p className="sr-only" aria-live="polite">{playbackState === 'playing' ? 'Reading the Dvar Torah aloud.' : playbackState === 'paused' ? 'Reading paused.' : speechError ?? ''}</p>
      {speechError === null ? null : <p className="mt-2 text-sm text-pomegranate" role="alert">{speechError}</p>}
    </div>
  )
}

function detachUtterance(utterance: SpeechSynthesisUtterance | null) {
  if (utterance === null) {
    return
  }

  utterance.onend = null
  utterance.onerror = null
}
