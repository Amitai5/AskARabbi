import { useCallback, useEffect, useImperativeHandle, useRef, useState, type Ref } from 'react'
import { Headphones, LoaderCircle, Pause, Play, RotateCcw, RotateCw, TextCursorInput } from 'lucide-react'
import { findAudioWord, formatAudioTime, validateAudioTimings } from './dvarTorahAudio.ts'
import type { DvarTorahClient } from './dvarTorahClient.ts'
import type { DvarTorahAudioTimings, DvarTorahAudioWord, WeeklyDvarTorahAudio } from './dvarTorahTypes.ts'
import { useSavedRecording } from '../pwa/useSavedRecording.ts'
import { useTeachingMediaSession } from './useTeachingMediaSession.ts'

interface DvarTorahReadAloudProps {
  ref?: Ref<DvarTorahPlaybackHandle>
  audio: WeeklyDvarTorahAudio | null
  weekKey: string
  title: string
  body: string
  client: DvarTorahClient
  onWordChange(word: DvarTorahAudioWord | null): void
  onTimingsChange?(timings: DvarTorahAudioTimings | null): void
  isFollowing?: boolean
  onToggleFollowing?(): void
}

export interface DvarTorahPlaybackHandle {
  seekToWord(word: DvarTorahAudioWord): void
}

type PlaybackState = 'idle' | 'loading' | 'playing' | 'paused' | 'error'

export function DvarTorahReadAloud(props: DvarTorahReadAloudProps) {
  return <DvarTorahPlayer key={`${props.weekKey}:${props.audio?.version ?? ''}`} {...props} />
}

function DvarTorahPlayer({ ref, audio, weekKey, title, body, client, onWordChange, onTimingsChange, isFollowing = false, onToggleFollowing }: DvarTorahReadAloudProps) {
  const audioRef = useRef<HTMLAudioElement | null>(null)
  const frameRef = useRef<number | null>(null)
  const requestIdRef = useRef(0)
  const timingsRequestRef = useRef<AbortController | null>(null)
  const timingsRef = useRef<DvarTorahAudioTimings | null>(null)
  const currentWordRef = useRef<DvarTorahAudioWord | null>(null)
  const pendingSeekRef = useRef<number | null>(null)
  const hasInteractedRef = useRef(false)
  const [playbackState, setPlaybackState] = useState<PlaybackState>('idle')
  const [position, setPosition] = useState(0)
  const [playbackRate, setPlaybackRate] = useState('1')
  const [playbackError, setPlaybackError] = useState<string | null>(null)
  const [timingsError, setTimingsError] = useState(false)
  const version = audio?.version
  const savedRecording = useSavedRecording(weekKey, version, title, body)

  const updateWord = useCallback(() => {
    if (!hasInteractedRef.current) {
      return
    }
    const element = audioRef.current
    const word = element === null || timingsRef.current === null ? null : findAudioWord(timingsRef.current.words, (pendingSeekRef.current ?? element.currentTime) * 1000)
    if (word !== currentWordRef.current) {
      currentWordRef.current = word
      onWordChange(word)
    }
  }, [onWordChange])

  function animateWord() {
    updateWord()
    if (audioRef.current?.paused === false) {
      frameRef.current = requestAnimationFrame(animateWord)
    }
  }

  const stopAnimation = useCallback(() => {
    if (frameRef.current !== null) {
      cancelAnimationFrame(frameRef.current)
      frameRef.current = null
    }
  }, [])

  const loadTimings = useCallback(() => {
    if (version === undefined || timingsRequestRef.current !== null || timingsRef.current !== null) {
      return
    }
    if (!navigator.onLine && !client.getAudioUrl(weekKey, version).startsWith('blob:')) { return }

    const controller = new AbortController()
    timingsRequestRef.current = controller
    setTimingsError(false)
    void client.getAudioTimings(weekKey, version, controller.signal)
      .then((value) => {
        if (controller.signal.aborted) {
          return
        }
        timingsRef.current = validateAudioTimings(value, version, title, body)
        setTimingsError(timingsRef.current === null)
        onTimingsChange?.(timingsRef.current)
        updateWord()
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setTimingsError(true)
        }
      })
      .finally(() => {
        if (timingsRequestRef.current === controller) {
          timingsRequestRef.current = null
        }
      })
  }, [body, client, onTimingsChange, title, updateWord, version, weekKey])

  useEffect(() => {
    const element = audioRef.current
    timingsRef.current = null
    pendingSeekRef.current = null
    hasInteractedRef.current = false
    onTimingsChange?.(null)
    if (currentWordRef.current !== null) {
      currentWordRef.current = null
      onWordChange(null)
    }
    if (element !== null && version !== undefined) {
      // Warm the authenticated recording and manifest together, without starting playback.
      const url = client.getAudioUrl(weekKey, version)
      if (navigator.onLine || url.startsWith('blob:')) {
        element.src = url
        element.load()
      }
      loadTimings()
    }
    return () => {
      requestIdRef.current += 1
      timingsRequestRef.current?.abort()
      timingsRequestRef.current = null
      if (frameRef.current !== null) {
        cancelAnimationFrame(frameRef.current)
        frameRef.current = null
      }
      if (element !== null) {
        element.pause()
        element.removeAttribute('src')
        element.load()
      }
    }
  }, [client, loadTimings, onTimingsChange, onWordChange, version, weekKey])

  useEffect(() => {
    if (!savedRecording?.timings || timingsRef.current !== null) { return }
    timingsRequestRef.current?.abort()
    timingsRequestRef.current = null
    timingsRef.current = savedRecording.timings
    setTimingsError(false)
    onTimingsChange?.(savedRecording.timings)
    updateWord()
  }, [savedRecording, onTimingsChange, updateWord])

  useImperativeHandle(ref, () => ({
    seekToWord(word) {
      if (timingsRef.current?.words.includes(word)) {
        startPlayback(word.audioOffsetMs / 1000)
      }
    },
  }))

  function togglePlayback() {
    const element = audioRef.current
    if (element === null || audio === null) {
      return
    }
    if (playbackState === 'playing' || playbackState === 'loading') {
      pausePlayback()
      return
    }

    startPlayback()
  }

  function pausePlayback() {
    requestIdRef.current += 1
    audioRef.current?.pause()
    stopAnimation()
    setPlaybackState('paused')
  }

  function skip(seconds: number) {
    seek((pendingSeekRef.current ?? audioRef.current?.currentTime ?? 0) + seconds)
  }

  useTeachingMediaSession({ title, active: playbackState !== 'idle' && playbackState !== 'error', playing: playbackState === 'playing', position, duration: (audio?.durationMs ?? 0) / 1000, rate: Number(playbackRate), play: () => startPlayback(), pause: pausePlayback, seek, skip })

  function startPlayback(seconds?: number) {
    const element = audioRef.current
    if (element === null || audio === null) {
      return
    }
    const requestId = ++requestIdRef.current
    hasInteractedRef.current = true
    setPlaybackError(null)
    setPlaybackState((current) => current === 'playing' ? current : 'loading')
    const previousPosition = pendingSeekRef.current ?? element.currentTime
    const hadEnded = element.ended
    const sourceChanged = prepareSource(playbackState === 'error')
    if (sourceChanged === null) { return }
    if (seconds !== undefined) {
      seek(seconds)
    } else if (hadEnded) {
      seek(0)
    } else if (sourceChanged && previousPosition > 0) {
      seek(previousPosition)
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

  const failPlayback = useCallback(() => {
    requestIdRef.current += 1
    stopAnimation()
    audioRef.current?.pause()
    hasInteractedRef.current = false
    setPlaybackState('error')
    setPlaybackError(navigator.onLine
      ? 'The recording could not be played. Try again, or sign in again if your session expired.'
      : 'This recording is not ready for offline playback. Reconnect to finish its download in Settings → App and offline, then try again.')
    currentWordRef.current = null
    onWordChange(null)
  }, [onWordChange, stopAnimation])

  useEffect(() => {
    if (playbackState !== 'loading') { return }
    const timeout = window.setTimeout(failPlayback, 15_000)
    return () => window.clearTimeout(timeout)
  }, [failPlayback, playbackState])

  function prepareSource(forceReload = false): boolean | null {
    const element = audioRef.current
    if (!element || !audio) { return null }
    const url = savedRecording?.url ?? client.getAudioUrl(weekKey, audio.version)
    if (!navigator.onLine && !url.startsWith('blob:')) {
      failPlayback()
      return null
    }
    if (element.getAttribute('src') !== url || forceReload) {
      element.src = url
      element.load()
      return true
    }
    return false
  }

  function updatePosition() {
    setPosition(pendingSeekRef.current ?? audioRef.current?.currentTime ?? 0)
    updateWord()
  }

  function seek(seconds: number) {
    const element = audioRef.current
    if (element === null || audio === null || !Number.isFinite(seconds)) {
      return
    }
    const wasPlaying = !element.paused
    const sourceChanged = prepareSource()
    if (sourceChanged === null) { return }
    const position = Math.max(0, Math.min(seconds, audio.durationMs / 1000))
    hasInteractedRef.current = true
    pendingSeekRef.current = position
    if (element.readyState >= HTMLMediaElement.HAVE_METADATA) {
      element.currentTime = position
      pendingSeekRef.current = null
    }
    updatePosition()
    if (sourceChanged && wasPlaying) {
      const requestId = ++requestIdRef.current
      void element.play().catch(() => { if (requestId === requestIdRef.current) { failPlayback() } })
    }
  }

  function applyPendingSeek() {
    if (pendingSeekRef.current !== null) {
      seek(pendingSeekRef.current)
    }
  }

  if (audio === null) {
    return <p className="mt-6 inline-flex items-center gap-2 text-sm text-muted"><Headphones aria-hidden="true" className="size-4" />Audio is not available for this teaching yet.</p>
  }

  const isActive = playbackState === 'playing' || playbackState === 'loading'
  const primaryLabel = isActive ? 'Pause recording' : playbackState === 'paused' ? 'Resume recording' : playbackState === 'error' ? 'Retry recording' : 'Listen to this teaching'
  const PrimaryIcon = playbackState === 'loading' ? LoaderCircle : isActive ? Pause : Play
  const duration = Math.max(0, audio.durationMs / 1000)

  return (
    <section className="mx-auto w-full max-w-[80rem] rounded-2xl border border-line bg-paper px-3 py-2 shadow-[0_-4px_24px_-12px_rgba(20,37,59,0.18)] sm:px-5 sm:py-3" aria-label="Dvar Torah audio player">
      <audio ref={audioRef} crossOrigin="use-credentials" preload="auto" aria-label="Dvar Torah recording" onLoadedMetadata={applyPendingSeek} onPlaying={() => {
        applyPendingSeek()
        setPlaybackState('playing')
        stopAnimation()
        animateWord()
      }} onPause={() => {
        stopAnimation()
        setPlaybackState((current) => current === 'error' || current === 'idle' ? current : 'paused')
      }} onWaiting={() => setPlaybackState((current) => current === 'playing' ? 'loading' : current)} onTimeUpdate={updatePosition} onSeeked={updatePosition} onEnded={() => {
        stopAnimation()
        setPlaybackState('idle')
        hasInteractedRef.current = false
        pendingSeekRef.current = null
        currentWordRef.current = null
        onWordChange(null)
      }} onError={failPlayback} />
      <div className="flex items-center justify-between gap-1">
        <div className="flex shrink-0 items-center">
        <button type="button" onClick={() => skip(-15)} disabled={playbackState === 'idle' || playbackState === 'error'} aria-label="Rewind 15 seconds" title="Rewind 15 seconds" className="relative flex size-[44px] shrink-0 items-center justify-center rounded-full text-ink hover:bg-stone disabled:opacity-40"><RotateCcw aria-hidden="true" className="size-7" strokeWidth={1.5} /><span aria-hidden="true" className="absolute text-[10px] font-bold">15</span></button>
        <button type="button" onClick={togglePlayback} aria-label={primaryLabel} title={primaryLabel} className="inline-flex min-h-[44px] min-w-[44px] shrink-0 items-center justify-center gap-2.5 rounded-full bg-pomegranate px-3 text-sm font-semibold text-white transition hover:bg-pomegranate-dark focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate sm:px-4">
          <PrimaryIcon aria-hidden="true" className={`size-4 ${playbackState === 'loading' ? 'animate-spin motion-reduce:animate-none' : ''}`} fill={isActive ? 'none' : 'currentColor'} strokeWidth={1.8} />
          <span className="hidden sm:inline">{playbackState === 'loading' ? 'Loading audio…' : isActive ? 'Pause' : playbackState === 'paused' ? 'Resume' : playbackState === 'error' ? 'Try again' : 'Listen'}</span>
        </button>
        <button type="button" onClick={() => skip(15)} disabled={playbackState === 'idle' || playbackState === 'error'} aria-label="Forward 15 seconds" title="Forward 15 seconds" className="relative flex size-[44px] shrink-0 items-center justify-center rounded-full text-ink hover:bg-stone disabled:opacity-40"><RotateCw aria-hidden="true" className="size-7" strokeWidth={1.5} /><span aria-hidden="true" className="absolute text-[10px] font-bold">15</span></button>
        </div>
        {onToggleFollowing === undefined ? null : <button type="button" onClick={onToggleFollowing} aria-pressed={isFollowing} aria-label="Follow text" title={isFollowing ? 'Auto-scroll is on. Scroll manually to pause following.' : 'Resume following the spoken words.'} className={`inline-flex min-h-[44px] min-w-[44px] shrink-0 items-center justify-center gap-1.5 rounded-full text-sm font-semibold transition hover:bg-stone focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-pomegranate sm:px-3 ${isFollowing ? 'bg-pomegranate/8 text-pomegranate' : 'text-muted'}`}><TextCursorInput aria-hidden="true" className="size-5 sm:size-4" /><span className="hidden sm:inline">{isFollowing ? 'Follow text' : 'Follow paused'}</span></button>}
        <div className="shrink-0">
          <label className="sr-only" htmlFor={`audio-speed-${weekKey}`}>Playback speed</label>
          <select id={`audio-speed-${weekKey}`} value={playbackRate} onChange={(event) => {
            setPlaybackRate(event.target.value)
            if (audioRef.current !== null) {
              audioRef.current.playbackRate = Number(event.target.value)
            }
          }} className="min-h-[44px] w-[4.5rem] rounded-lg border border-line bg-paper px-1 text-sm font-semibold text-ink sm:w-auto sm:px-2" aria-label="Playback speed">
            <option value="0.75">0.75×</option><option value="1">1×</option><option value="1.25">1.25×</option><option value="1.5">1.5×</option><option value="2">2×</option>
          </select>
        </div>
      </div>
      <div className="mt-1 flex items-center gap-2 sm:gap-3">
        <button type="button" onClick={() => seek(0)} disabled={playbackState === 'idle' || playbackState === 'error'} aria-label="Restart recording" title="Restart recording" className="flex size-[44px] shrink-0 items-center justify-center rounded-full text-ink-soft transition hover:bg-stone hover:text-pomegranate disabled:opacity-40"><RotateCcw aria-hidden="true" className="size-4" /></button>
        <span className="min-w-9 text-xs tabular-nums text-ink-soft" aria-hidden="true">{formatAudioTime(position)}</span>
        <input type="range" aria-label="Recording position" aria-valuetext={`${formatAudioTime(position)} of ${formatAudioTime(duration)}`} min={0} max={duration} step={0.1} value={Math.min(position, duration)} disabled={playbackState === 'idle' || playbackState === 'error'} onChange={(event) => seek(Number(event.target.value))} className="h-11 min-w-0 flex-1 cursor-pointer accent-pomegranate disabled:cursor-default disabled:opacity-50" />
        <span className="text-xs tabular-nums text-muted" aria-hidden="true">{formatAudioTime(duration)}</span>
      </div>
      <p className="sr-only" aria-live="polite">{playbackState === 'playing' ? 'Playing the Dvar Torah recording.' : playbackState === 'paused' ? 'Recording paused.' : ''}</p>
      {timingsError ? <p className="mt-2 text-xs leading-5 text-muted">Word navigation and text following are unavailable for this recording. You can still listen.</p> : null}
      {playbackError === null ? null : <p className="mt-2 text-sm leading-6 text-pomegranate" role="alert">{playbackError}</p>}
    </section>
  )
}
