import { useEffect, useRef, useState } from 'react'
import { Headphones, LoaderCircle, Pause, Play, RotateCcw, TextCursorInput } from 'lucide-react'
import { findAudioWord, formatAudioTime, validateAudioTimings } from './dvarTorahAudio.ts'
import type { DvarTorahClient } from './dvarTorahClient.ts'
import type { DvarTorahAudioTimings, DvarTorahAudioWord, WeeklyDvarTorahAudio } from './dvarTorahTypes.ts'

interface DvarTorahReadAloudProps {
  audio: WeeklyDvarTorahAudio | null
  weekKey: string
  title: string
  body: string
  client: DvarTorahClient
  onWordChange(word: DvarTorahAudioWord | null): void
  isFollowing?: boolean
  onToggleFollowing?(): void
}

type PlaybackState = 'idle' | 'loading' | 'playing' | 'paused' | 'error'

export function DvarTorahReadAloud({ audio, weekKey, title, body, client, onWordChange, isFollowing = false, onToggleFollowing }: DvarTorahReadAloudProps) {
  const audioRef = useRef<HTMLAudioElement | null>(null)
  const frameRef = useRef<number | null>(null)
  const requestIdRef = useRef(0)
  const timingsRequestRef = useRef<AbortController | null>(null)
  const timingsRef = useRef<DvarTorahAudioTimings | null>(null)
  const currentWordRef = useRef<DvarTorahAudioWord | null>(null)
  const [playbackState, setPlaybackState] = useState<PlaybackState>('idle')
  const [position, setPosition] = useState(0)
  const [playbackRate, setPlaybackRate] = useState('1')
  const [playbackError, setPlaybackError] = useState<string | null>(null)
  const [timingsError, setTimingsError] = useState(false)

  useEffect(() => {
    const element = audioRef.current
    return () => {
      requestIdRef.current += 1
      timingsRequestRef.current?.abort()
      if (frameRef.current !== null) {
        cancelAnimationFrame(frameRef.current)
      }
      if (element !== null) {
        element.pause()
        element.removeAttribute('src')
        element.load()
      }
    }
  }, [])

  function updateWord() {
    const element = audioRef.current
    const word = element === null || timingsRef.current === null ? null : findAudioWord(timingsRef.current.words, element.currentTime * 1000)
    if (word !== currentWordRef.current) {
      currentWordRef.current = word
      onWordChange(word)
    }
  }

  function animateWord() {
    updateWord()
    if (audioRef.current?.paused === false) {
      frameRef.current = requestAnimationFrame(animateWord)
    }
  }

  function stopAnimation() {
    if (frameRef.current !== null) {
      cancelAnimationFrame(frameRef.current)
      frameRef.current = null
    }
  }

  function loadTimings() {
    if (audio === null || timingsRequestRef.current !== null) {
      return
    }

    const controller = new AbortController()
    timingsRequestRef.current = controller
    setTimingsError(false)
    void client.getAudioTimings(weekKey, audio.version, controller.signal)
      .then((value) => {
        if (controller.signal.aborted) {
          return
        }
        timingsRef.current = validateAudioTimings(value, audio.version, title, body)
        setTimingsError(timingsRef.current === null)
        updateWord()
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          timingsRequestRef.current = null
          setTimingsError(true)
        }
      })
  }

  function togglePlayback() {
    const element = audioRef.current
    if (element === null || audio === null) {
      return
    }
    if (playbackState === 'playing' || playbackState === 'loading') {
      requestIdRef.current += 1
      element.pause()
      stopAnimation()
      setPlaybackState('paused')
      return
    }

    const requestId = ++requestIdRef.current
    setPlaybackError(null)
    setPlaybackState('loading')
    if (!element.hasAttribute('src') || playbackState === 'error') {
      element.src = client.getAudioUrl(weekKey, audio.version)
      element.load()
    }
    if (element.ended) {
      element.currentTime = 0
    }
    element.playbackRate = Number(playbackRate)

    // Start media inside the gesture (especially on iOS); timing metadata must never delay playback.
    void element.play().catch(() => {
      if (requestId === requestIdRef.current) {
        failPlayback()
      }
    })
    loadTimings()
  }

  function failPlayback() {
    stopAnimation()
    setPlaybackState('error')
    setPlaybackError('The recording could not be played. Try again, or sign in again if your session expired.')
    currentWordRef.current = null
    onWordChange(null)
  }

  function updatePosition() {
    setPosition(audioRef.current?.currentTime ?? 0)
    updateWord()
  }

  function seek(seconds: number) {
    const element = audioRef.current
    if (element === null || !element.hasAttribute('src')) {
      return
    }
    element.currentTime = seconds
    updatePosition()
  }

  if (audio === null) {
    return <p className="mt-6 inline-flex items-center gap-2 text-sm text-muted"><Headphones aria-hidden="true" className="size-4" />Audio is not available for this teaching yet.</p>
  }

  const isActive = playbackState === 'playing' || playbackState === 'loading'
  const primaryLabel = isActive ? 'Pause recording' : playbackState === 'paused' ? 'Resume recording' : playbackState === 'error' ? 'Retry recording' : 'Listen to this teaching'
  const PrimaryIcon = playbackState === 'loading' ? LoaderCircle : isActive ? Pause : Play
  const duration = Math.max(0, audio.durationMs / 1000)

  return (
    <section className="mx-auto w-full max-w-[54rem] rounded-2xl border border-line bg-paper px-3 py-2 shadow-[0_-4px_24px_-12px_rgba(20,37,59,0.18)] sm:px-5 sm:py-3" aria-label="Dvar Torah audio player">
      <audio ref={audioRef} crossOrigin="use-credentials" preload="none" aria-label="Dvar Torah recording" onPlaying={() => {
        setPlaybackState('playing')
        stopAnimation()
        animateWord()
      }} onPause={() => {
        stopAnimation()
        setPlaybackState((current) => current === 'error' || current === 'idle' ? current : 'paused')
      }} onWaiting={() => setPlaybackState((current) => current === 'playing' ? 'loading' : current)} onTimeUpdate={updatePosition} onSeeked={updatePosition} onEnded={() => {
        stopAnimation()
        setPlaybackState('idle')
        currentWordRef.current = null
        onWordChange(null)
      }} onError={failPlayback} />
      <div className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1">
        <button type="button" onClick={togglePlayback} aria-label={primaryLabel} title={primaryLabel} className="inline-flex min-h-11 min-w-11 shrink-0 items-center justify-center gap-2.5 rounded-full bg-pomegranate px-3 text-sm font-semibold text-white transition hover:bg-pomegranate-dark focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate sm:px-4">
          <PrimaryIcon aria-hidden="true" className={`size-4 ${playbackState === 'loading' ? 'animate-spin motion-reduce:animate-none' : ''}`} fill={isActive ? 'none' : 'currentColor'} strokeWidth={1.8} />
          <span className="hidden sm:inline">{playbackState === 'loading' ? 'Loading audio…' : isActive ? 'Pause' : playbackState === 'paused' ? 'Resume' : playbackState === 'error' ? 'Try again' : 'Listen'}</span>
        </button>
        {onToggleFollowing === undefined ? null : <button type="button" onClick={onToggleFollowing} aria-pressed={isFollowing} aria-label="Follow text" title={isFollowing ? 'Auto-scroll is on. Scroll manually to pause following.' : 'Resume following the spoken words.'} className={`inline-flex min-h-11 items-center gap-1.5 rounded-full px-1 text-xs font-semibold transition hover:bg-stone sm:px-3 sm:text-sm ${isFollowing ? 'text-pomegranate' : 'text-muted'}`}><TextCursorInput aria-hidden="true" className="hidden size-4 sm:block" /><span>{isFollowing ? 'Follow text' : 'Follow paused'}</span></button>}
        <div className="flex items-center gap-1">
          <button type="button" onClick={() => seek(0)} disabled={playbackState === 'idle' || playbackState === 'error'} aria-label="Restart recording" className="flex size-11 items-center justify-center rounded-full text-ink-soft transition hover:bg-stone hover:text-pomegranate disabled:opacity-40"><RotateCcw aria-hidden="true" className="size-4" /></button>
          <label className="sr-only" htmlFor={`audio-speed-${weekKey}`}>Playback speed</label>
          <select id={`audio-speed-${weekKey}`} value={playbackRate} onChange={(event) => {
            setPlaybackRate(event.target.value)
            if (audioRef.current !== null) {
              audioRef.current.playbackRate = Number(event.target.value)
            }
          }} className="min-h-11 rounded-lg border border-line bg-paper px-2 text-sm font-semibold text-ink" aria-label="Playback speed">
            <option value="0.75">0.75×</option><option value="1">1×</option><option value="1.25">1.25×</option><option value="1.5">1.5×</option><option value="2">2×</option>
          </select>
        </div>
      </div>
      <div className="mt-1 flex items-center gap-3">
        <span className="min-w-9 text-xs tabular-nums text-ink-soft" aria-hidden="true">{formatAudioTime(position)}</span>
        <input type="range" aria-label="Recording position" aria-valuetext={`${formatAudioTime(position)} of ${formatAudioTime(duration)}`} min={0} max={duration} step={0.1} value={Math.min(position, duration)} disabled={playbackState === 'idle' || playbackState === 'error'} onChange={(event) => seek(Number(event.target.value))} className="h-11 min-w-0 flex-1 cursor-pointer accent-pomegranate disabled:cursor-default disabled:opacity-50" />
        <span className="text-xs tabular-nums text-muted" aria-hidden="true">{formatAudioTime(duration)}</span>
      </div>
      <p className="sr-only" aria-live="polite">{playbackState === 'playing' ? 'Playing the Dvar Torah recording.' : playbackState === 'paused' ? 'Recording paused.' : ''}</p>
      {timingsError ? <p className="mt-2 text-xs leading-5 text-muted">Word highlighting is unavailable for this recording. You can still listen.</p> : null}
      {playbackError === null ? null : <p className="mt-2 text-sm leading-6 text-pomegranate" role="alert">{playbackError}</p>}
    </section>
  )
}
