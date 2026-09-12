import { useEffect, useEffectEvent } from 'react'

interface Options {
  title: string
  active: boolean
  playing: boolean
  position: number
  duration: number
  rate: number
  play(): void
  pause(): void
  seek(seconds: number): void
  skip(seconds: number): void
}

export function useTeachingMediaSession({ title, active, playing, position, duration, rate, play, pause, seek, skip }: Options) {
  const handleAction = useEffectEvent((event: MediaSessionActionDetails) => {
    switch (event.action) {
      case 'play': play(); break
      case 'pause': pause(); break
      case 'seekbackward': skip(-(event.seekOffset ?? 15)); break
      case 'seekforward': skip(event.seekOffset ?? 15); break
      case 'seekto': if (event.seekTime !== undefined) { seek(event.seekTime) }; break
      case 'stop': pause(); seek(0); break
    }
  })

  useEffect(() => {
    if (!active || !navigator.mediaSession) { return }
    const session = navigator.mediaSession
    const actions: MediaSessionAction[] = ['play', 'pause', 'seekbackward', 'seekforward', 'seekto', 'stop']
    if (typeof MediaMetadata !== 'undefined') { session.metadata = new MediaMetadata({ title, artist: 'AskRabbi', album: 'Weekly Dvar Torah' }) }
    for (const action of actions) {
      try { session.setActionHandler(action, event => handleAction(event)) }
      catch { /* Some browsers implement only a subset of media actions. */ }
    }
    return () => {
      for (const action of actions) {
        try { session.setActionHandler(action, null) }
        catch { /* Unsupported actions also reject removal. */ }
      }
      session.metadata = null
      session.playbackState = 'none'
      try { session.setPositionState?.() }
      catch { /* Position reporting is optional; playback still works. */ }
    }
  }, [active, title])

  useEffect(() => {
    if (!active || !navigator.mediaSession) { return }
    navigator.mediaSession.playbackState = playing ? 'playing' : 'paused'
    if (!Number.isFinite(duration) || duration <= 0) { return }
    try { navigator.mediaSession.setPositionState?.({ duration, playbackRate: rate, position: Math.min(duration, Math.max(0, position)) }) }
    catch { /* Keep in-page playback working when device position reporting is unavailable. */ }
  }, [active, playing, position, duration, rate])
}
