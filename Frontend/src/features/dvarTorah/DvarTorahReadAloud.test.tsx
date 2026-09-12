import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createRef, StrictMode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { DvarTorahReadAloud, type DvarTorahPlaybackHandle } from './DvarTorahReadAloud.tsx'
import type { DvarTorahClient } from './dvarTorahClient.ts'
import type { DvarTorahAudioTimings, WeeklyDvarTorahAudio } from './dvarTorahTypes.ts'
import { OfflineLibraryChanged, readOfflineLibrary, type OfflineLibrary } from '../pwa/offlineLibrary.ts'

vi.mock('../pwa/offlineLibrary.ts', async importOriginal => ({
  ...await importOriginal<typeof import('../pwa/offlineLibrary.ts')>(), readOfflineLibrary: vi.fn(),
}))

const Audio: WeeklyDvarTorahAudio = { version: 'v1', voice: 'Andrew', durationMs: 10_000, audioUrl: '', timingsUrl: '' }
const Timings: DvarTorahAudioTimings = { schemaVersion: 1, version: 'v1', title: 'A teaching', body: 'Learn together.', durationMs: 10_000, words: [
  { section: 'body', text: 'Learn', textOffset: 0, textLength: 5, audioOffsetMs: 500, durationMs: 500 },
] }

beforeEach(() => {
  vi.mocked(readOfflineLibrary).mockResolvedValue({ audioEnabled: true, revision: 0, teaching: null })
  vi.spyOn(HTMLMediaElement.prototype, 'play').mockImplementation(function (this: HTMLMediaElement) {
    this.dispatchEvent(new Event('playing'))
    return Promise.resolve()
  })
  vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {})
  vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {})
})

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  vi.useRealTimers()
})

describe('DvarTorahReadAloud', () => {
  it('skips 15 seconds in either direction, clamps boundaries, and preserves a paused recording', async () => {
    const user = userEvent.setup()
    renderPlayer(createClient(), { ...Audio, durationMs: 60_000 })
    const element = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement
    Object.defineProperty(element, 'readyState', { configurable: true, value: HTMLMediaElement.HAVE_METADATA })
    expect(screen.getByRole('button', { name: 'Forward 15 seconds' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Listen to this teaching' }))
    await user.click(screen.getByRole('button', { name: 'Forward 15 seconds' }))
    expect(element.currentTime).toBe(15)
    await user.click(screen.getByRole('button', { name: 'Forward 15 seconds' }))
    expect(element.currentTime).toBe(30)
    await user.click(screen.getByRole('button', { name: 'Pause recording' }))
    const plays = vi.mocked(HTMLMediaElement.prototype.play).mock.calls.length
    await user.click(screen.getByRole('button', { name: 'Rewind 15 seconds' }))
    expect(element.currentTime).toBe(15)
    expect(screen.getByRole('button', { name: 'Resume recording' })).toBeVisible()
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalledTimes(plays)
    element.currentTime = 5
    await user.click(screen.getByRole('button', { name: 'Rewind 15 seconds' }))
    expect(element.currentTime).toBe(0)
    element.currentTime = 55
    await user.click(screen.getByRole('button', { name: 'Forward 15 seconds' }))
    expect(element.currentTime).toBe(60)
  })

  it('uses the latest pending seek when metadata has not loaded yet', async () => {
    const user = userEvent.setup()
    renderPlayer(createClient(), { ...Audio, durationMs: 60_000 })
    await user.click(screen.getByRole('button', { name: 'Listen to this teaching' }))
    await user.click(screen.getByRole('button', { name: 'Forward 15 seconds' }))
    await user.click(screen.getByRole('button', { name: 'Forward 15 seconds' }))
    expect(screen.getByRole('slider', { name: 'Recording position' })).toHaveValue('30')
  })

  it('publishes media metadata and device actions only after playback and removes them on unmount', async () => {
    const user = userEvent.setup()
    const handlers = new Map<string, MediaSessionActionHandler | null>()
    const session = { metadata: null, playbackState: 'none', setPositionState: vi.fn(), setActionHandler: vi.fn((action, handler) => handlers.set(action, handler)) }
    Object.defineProperty(navigator, 'mediaSession', { configurable: true, value: session })
    vi.stubGlobal('MediaMetadata', class { constructor(data: MediaMetadataInit) { Object.assign(this, data) } })
    const view = renderPlayer(createClient(), { ...Audio, durationMs: 60_000 })
    expect(session.metadata).toBeNull()
    const element = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement
    Object.defineProperty(element, 'readyState', { configurable: true, value: HTMLMediaElement.HAVE_METADATA })
    await user.click(screen.getByRole('button', { name: 'Listen to this teaching' }))
    expect(session.metadata).toMatchObject({ title: 'A teaching', artist: 'AskRabbi' })
    act(() => handlers.get('seekforward')?.({ action: 'seekforward' }))
    expect(element.currentTime).toBe(15)
    act(() => handlers.get('pause')?.({ action: 'pause' }))
    expect(session.playbackState).toBe('paused')
    act(() => handlers.get('seekbackward')?.({ action: 'seekbackward', seekOffset: 5 }))
    expect(element.currentTime).toBe(10)
    act(() => handlers.get('seekto')?.({ action: 'seekto', seekTime: 40 }))
    expect(element.currentTime).toBe(40)
    fireEvent.change(screen.getByRole('combobox', { name: 'Playback speed' }), { target: { value: '1.5' } })
    expect(session.setPositionState).toHaveBeenLastCalledWith({ duration: 60, position: 40, playbackRate: 1.5 })
    view.unmount()
    expect(session.metadata).toBeNull()
    expect(session.playbackState).toBe('none')
    expect([...handlers.values()].every(handler => handler === null)).toBe(true)
    Reflect.deleteProperty(navigator, 'mediaSession')
  })

  it('keeps playback usable when optional device media actions are unsupported', async () => {
    const user = userEvent.setup()
    Object.defineProperty(navigator, 'mediaSession', { configurable: true, value: { setActionHandler: () => { throw new Error('Unsupported') }, setPositionState: () => { throw new Error('Unsupported') } } })
    const view = renderPlayer(createClient())
    await user.click(screen.getByRole('button', { name: 'Listen to this teaching' }))
    expect(screen.getByRole('button', { name: 'Pause recording' })).toBeVisible()
    view.unmount()
    Reflect.deleteProperty(navigator, 'mediaSession')
  })

  it('switches an already-open stream to the saved recording when seeking a word offline', async () => {
    mockBlobUrls()
    const client = createClient()
    const ref = createRef<DvarTorahPlaybackHandle>()
    const onTimingsChange = vi.fn()
    render(<DvarTorahReadAloud ref={ref} audio={Audio} weekKey="diaspora:2026-09-05" title={Timings.title} body={Timings.body} client={client} onWordChange={vi.fn()} onTimingsChange={onTimingsChange} />)
    await waitFor(() => expect(onTimingsChange).toHaveBeenLastCalledWith(Timings))
    const element = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement
    expect(element.src).toContain('api.askarabbi.test')

    vi.mocked(readOfflineLibrary).mockResolvedValue(savedLibrary())
    fireEvent(window, new Event(OfflineLibraryChanged))
    await waitFor(() => expect(URL.createObjectURL).toHaveBeenCalledTimes(1))
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    Object.defineProperty(element, 'readyState', { configurable: true, value: HTMLMediaElement.HAVE_METADATA })
    act(() => ref.current?.seekToWord(Timings.words[0]))

    expect(element.src).toBe('blob:http://localhost/saved-recording')
    expect(element.currentTime).toBe(0.5)
    expect(screen.getByRole('button', { name: 'Pause recording' })).toBeVisible()
    expect(client.getAudioTimings).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('loads saved timings offline without API requests and supports seeking before Listen', async () => {
    mockBlobUrls()
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    vi.mocked(readOfflineLibrary).mockResolvedValue(savedLibrary())
    const client = createClient()
    const ref = createRef<DvarTorahPlaybackHandle>()
    const onTimingsChange = vi.fn()
    const { unmount } = render(<DvarTorahReadAloud ref={ref} audio={Audio} weekKey="diaspora:2026-09-05" title={Timings.title} body={Timings.body} client={client} onWordChange={vi.fn()} onTimingsChange={onTimingsChange} />)
    await waitFor(() => expect(onTimingsChange).toHaveBeenLastCalledWith(Timings))
    expect(client.getAudioTimings).not.toHaveBeenCalled()
    expect(HTMLMediaElement.prototype.play).not.toHaveBeenCalled()
    const element = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement
    Object.defineProperty(element, 'readyState', { configurable: true, value: HTMLMediaElement.HAVE_METADATA })

    act(() => ref.current?.seekToWord(Timings.words[0]))
    expect(element.src).toBe('blob:http://localhost/saved-recording')
    expect(element.currentTime).toBe(0.5)
    fireEvent.change(screen.getByRole('slider', { name: 'Recording position' }), { target: { value: '5' } })
    expect(element.currentTime).toBe(5)
    unmount()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:http://localhost/saved-recording')
  })

  it('restarts a finished stream from the beginning when the next playback uses the offline copy', async () => {
    mockBlobUrls()
    renderPlayer(createClient())
    await act(async () => {})
    const element = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement
    element.currentTime = 10
    Object.defineProperty(element, 'ended', { configurable: true, value: true })
    Object.defineProperty(element, 'readyState', { configurable: true, value: HTMLMediaElement.HAVE_METADATA })
    vi.mocked(HTMLMediaElement.prototype.load).mockImplementation(function (this: HTMLMediaElement) {
      Object.defineProperty(this, 'ended', { configurable: true, value: false })
      this.currentTime = 0
    })
    vi.mocked(readOfflineLibrary).mockResolvedValue(savedLibrary())
    fireEvent(window, new Event(OfflineLibraryChanged))
    await waitFor(() => expect(URL.createObjectURL).toHaveBeenCalledTimes(1))
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)

    fireEvent.click(screen.getByRole('button', { name: 'Listen to this teaching' }))

    expect(element.src).toBe('blob:http://localhost/saved-recording')
    expect(element.currentTime).toBe(0)
    expect(screen.getByRole('button', { name: 'Pause recording' })).toBeVisible()
  })

  it.each(['missing', 'old-version', 'different-body', 'disabled'])('fails clearly offline instead of streaming when the saved recording is %s', async reason => {
    mockBlobUrls()
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    const library = savedLibrary()
    if (reason === 'missing') { library.teaching = null }
    if (reason === 'disabled') { library.audioEnabled = false }
    if (reason === 'old-version' && library.teaching?.publication.dvarTorah) { library.teaching.publication.dvarTorah.audio = { ...Audio, version: 'old' } }
    if (reason === 'different-body' && library.teaching?.publication.dvarTorah) { library.teaching.publication.dvarTorah.body = 'Different teaching' }
    vi.mocked(readOfflineLibrary).mockResolvedValue(library)
    const client = createClient()
    renderPlayer(client)
    await act(async () => {})

    fireEvent.click(screen.getByRole('button', { name: 'Listen to this teaching' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Reconnect to finish its download')
    expect(screen.getByRole('button', { name: 'Retry recording' })).toBeVisible()
    expect(HTMLMediaElement.prototype.play).not.toHaveBeenCalled()
    expect(client.getAudioTimings).not.toHaveBeenCalled()
    expect(URL.createObjectURL).not.toHaveBeenCalled()
  })

  it('stops a stalled loading indicator with a retry action', async () => {
    vi.useFakeTimers()
    vi.mocked(HTMLMediaElement.prototype.play).mockImplementation(() => new Promise(() => {}))
    renderPlayer(createClient())
    fireEvent.click(screen.getByRole('button', { name: 'Listen to this teaching' }))
    expect(screen.getByText('Loading audio…')).toBeInTheDocument()

    await act(async () => vi.advanceTimersByTimeAsync(15_000))

    expect(screen.getByRole('button', { name: 'Retry recording' })).toBeVisible()
    expect(screen.queryByText('Loading audio…')).not.toBeInTheDocument()
  })

  it('does not fetch or attempt browser synthesis when the recording is missing', () => {
    const client = createClient()
    renderPlayer(client, null)

    expect(screen.getByText('Audio is not available for this teaching yet.')).toBeVisible()
    expect(screen.queryByLabelText('Dvar Torah recording')).not.toBeInTheDocument()
    expect(client.getAudioTimings).not.toHaveBeenCalled()
    expect(HTMLMediaElement.prototype.play).not.toHaveBeenCalled()
  })

  it('keeps playback usable when timing metadata fails', async () => {
    const user = userEvent.setup()
    const client = createClient()
    client.getAudioTimings = vi.fn().mockRejectedValue(new Error('Unavailable'))
    renderPlayer(client)

    await user.click(screen.getByRole('button', { name: 'Listen to this teaching' }))

    expect(await screen.findByText(/Word highlighting is unavailable/)).toBeVisible()
    expect(screen.getByRole('button', { name: 'Pause recording' })).toBeEnabled()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('preloads the recording and validates timings before Listen without playing or highlighting', async () => {
    const client = createClient()
    const onTimingsChange = vi.fn()
    const onWordChange = vi.fn()
    client.getAudioTimings = vi.fn().mockResolvedValue({ ...Timings, words: [{ ...Timings.words[0], audioOffsetMs: 0 }] })
    render(<DvarTorahReadAloud audio={Audio} weekKey="week" title={Timings.title} body={Timings.body} client={client} onWordChange={onWordChange} onTimingsChange={onTimingsChange} />)

    await waitFor(() => expect(onTimingsChange).toHaveBeenLastCalledWith(expect.objectContaining({ version: 'v1' })))
    const element = screen.getByLabelText('Dvar Torah recording')
    expect(element).toHaveAttribute('preload', 'auto')
    expect(element).toHaveAttribute('src', 'https://api.askarabbi.test/audio?version=v1')
    expect(client.getAudioTimings).toHaveBeenCalledTimes(1)
    expect(HTMLMediaElement.prototype.load).toHaveBeenCalledTimes(1)
    expect(HTMLMediaElement.prototype.play).not.toHaveBeenCalled()
    expect(onWordChange).not.toHaveBeenCalled()
  })

  it('seeks to a validated word before metadata is ready and applies only the latest selection', async () => {
    const client = createClient()
    const secondWord = { ...Timings.words[0], text: 'together', textOffset: 6, textLength: 8, audioOffsetMs: 3_000 }
    client.getAudioTimings = vi.fn().mockResolvedValue({ ...Timings, words: [...Timings.words, secondWord] })
    const ref = createRef<DvarTorahPlaybackHandle>()
    const onTimingsChange = vi.fn()
    const onWordChange = vi.fn()
    render(<DvarTorahReadAloud ref={ref} audio={Audio} weekKey="week" title={Timings.title} body={Timings.body} client={client} onWordChange={onWordChange} onTimingsChange={onTimingsChange} />)
    await waitFor(() => expect(onTimingsChange).toHaveBeenLastCalledWith(expect.objectContaining({ version: 'v1' })))
    const element = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement

    act(() => {
      ref.current?.seekToWord(Timings.words[0])
      ref.current?.seekToWord(secondWord)
    })
    expect(element.currentTime).toBe(0)
    expect(screen.getByRole('slider', { name: 'Recording position' })).toHaveValue('3')
    expect(onWordChange).toHaveBeenLastCalledWith(secondWord)
    Object.defineProperty(element, 'readyState', { configurable: true, value: HTMLMediaElement.HAVE_METADATA })
    fireEvent.loadedMetadata(element)

    expect(element.currentTime).toBe(3)
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalledTimes(2)
    expect(client.getAudioTimings).toHaveBeenCalledTimes(1)
    act(() => ref.current?.seekToWord({ ...secondWord, audioOffsetMs: 9_000 }))
    expect(element.currentTime).toBe(3)
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalledTimes(2)
  })

  it('restarts the aborted preload during Strict Mode setup without accepting its stale result', async () => {
    const client = createClient()
    let resolveFirst: (value: unknown) => void = () => {}
    client.getAudioTimings = vi.fn().mockImplementationOnce(() => new Promise(resolve => { resolveFirst = resolve })).mockResolvedValue(Timings)
    const onTimingsChange = vi.fn()
    render(<StrictMode><DvarTorahReadAloud audio={Audio} weekKey="week" title={Timings.title} body={Timings.body} client={client} onWordChange={vi.fn()} onTimingsChange={onTimingsChange} /></StrictMode>)

    await waitFor(() => expect(onTimingsChange).toHaveBeenLastCalledWith(Timings))
    expect(vi.mocked(client.getAudioTimings).mock.calls[0][2]?.aborted).toBe(true)
    await act(async () => resolveFirst({ ...Timings, version: 'stale' }))
    expect(onTimingsChange).toHaveBeenLastCalledWith(Timings)
    expect(screen.getByLabelText('Dvar Torah recording')).toHaveAttribute('src')
    expect(HTMLMediaElement.prototype.play).not.toHaveBeenCalled()
  })

  it('never highlights mismatched article or recording versions', async () => {
    const user = userEvent.setup()
    const client = createClient()
    client.getAudioTimings = vi.fn().mockResolvedValue({ ...Timings, version: 'old' })
    const onWordChange = vi.fn()
    renderPlayer(client, Audio, onWordChange)
    await user.click(screen.getByRole('button', { name: 'Listen to this teaching' }))
    const element = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement
    act(() => {
      element.currentTime = 0.6
      fireEvent.timeUpdate(element)
    })

    expect(await screen.findByText(/Word highlighting is unavailable/)).toBeVisible()
    expect(onWordChange).not.toHaveBeenCalled()
  })

  it('shows an actionable media error and retries without requesting synthesis', async () => {
    const user = userEvent.setup()
    vi.mocked(HTMLMediaElement.prototype.play).mockRejectedValueOnce(new Error('HTTP 401 internal detail'))
    const client = createClient()
    renderPlayer(client)

    await user.click(screen.getByRole('button', { name: 'Listen to this teaching' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('The recording could not be played.')
    expect(screen.queryByText(/HTTP 401/)).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Retry recording' }))

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Pause recording' })).toBeVisible()
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalledTimes(2)
    expect(client.getAudioTimings).toHaveBeenCalledTimes(1)
  })

  it('retries failed timing metadata with recording retry and caches the recovered manifest', async () => {
    const user = userEvent.setup()
    vi.mocked(HTMLMediaElement.prototype.play).mockRejectedValueOnce(new Error('Temporary network failure'))
    const client = createClient()
    client.getAudioTimings = vi.fn().mockRejectedValueOnce(new Error('Temporary network failure')).mockRejectedValueOnce(new Error('Temporary network failure')).mockResolvedValue(Timings)
    const onWordChange = vi.fn()
    renderPlayer(client, Audio, onWordChange)

    await user.click(screen.getByRole('button', { name: 'Listen to this teaching' }))
    expect(await screen.findByText(/Word highlighting is unavailable/)).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Retry recording' }))

    expect(client.getAudioTimings).toHaveBeenCalledTimes(3)
    await waitFor(() => expect(screen.queryByText(/Word highlighting is unavailable/)).not.toBeInTheDocument())
    const element = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement
    act(() => {
      element.currentTime = 0.6
      fireEvent.timeUpdate(element)
    })
    await waitFor(() => expect(onWordChange).toHaveBeenLastCalledWith(Timings.words[0]))
    await user.click(screen.getByRole('button', { name: 'Pause recording' }))
    await user.click(screen.getByRole('button', { name: 'Resume recording' }))
    expect(client.getAudioTimings).toHaveBeenCalledTimes(3)
  })

  it('clears the highlight on completion and restarts at the beginning', async () => {
    const user = userEvent.setup()
    const onWordChange = vi.fn()
    renderPlayer(createClient(), Audio, onWordChange)
    await user.click(screen.getByRole('button', { name: 'Listen to this teaching' }))
    const element = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement
    act(() => {
      element.currentTime = 0.6
      fireEvent.timeUpdate(element)
    })
    await waitFor(() => expect(onWordChange).toHaveBeenCalledWith(Timings.words[0]))
    fireEvent.ended(element)
    expect(onWordChange).toHaveBeenLastCalledWith(null)
    Object.defineProperty(element, 'ended', { value: true })
    Object.defineProperty(element, 'readyState', { configurable: true, value: HTMLMediaElement.HAVE_METADATA })
    await user.click(screen.getByRole('button', { name: 'Listen to this teaching' }))
    expect(element.currentTime).toBe(0)
    await user.click(screen.getByRole('button', { name: 'Restart recording' }))
    expect(element.currentTime).toBe(0)
  })

  it('aborts pending metadata and stops media when leaving the article', async () => {
    const user = userEvent.setup()
    const client = createClient()
    let resolveTimings: (value: unknown) => void = () => {}
    client.getAudioTimings = vi.fn(() => new Promise((resolve) => { resolveTimings = resolve }))
    const onWordChange = vi.fn()
    const { unmount } = renderPlayer(client, Audio, onWordChange)
    await user.click(screen.getByRole('button', { name: 'Listen to this teaching' }))
    const signal = vi.mocked(client.getAudioTimings).mock.calls[0][2]
    const element = screen.getByLabelText('Dvar Torah recording')

    unmount()
    await act(async () => { resolveTimings(Timings) })

    expect(signal?.aborted).toBe(true)
    expect(element).not.toHaveAttribute('src')
    expect(HTMLMediaElement.prototype.pause).toHaveBeenCalled()
    expect(onWordChange).not.toHaveBeenCalled()
  })
})

function createClient(): DvarTorahClient {
  return {
    getReadState: async () => ({ readWeekKeys: [] }),
    setReadState: vi.fn().mockResolvedValue(undefined),
    getCurrent: vi.fn(), getArchive: vi.fn(), getArchived: vi.fn(),
    getAudioUrl: vi.fn(() => 'https://api.askarabbi.test/audio?version=v1'),
    getAudioTimings: vi.fn().mockResolvedValue(Timings),
  }
}

function renderPlayer(client: DvarTorahClient, audio: WeeklyDvarTorahAudio | null = Audio, onWordChange = vi.fn()) {
  return render(<DvarTorahReadAloud audio={audio} weekKey="diaspora:2026-09-05" title={Timings.title} body={Timings.body} client={client} onWordChange={onWordChange} />)
}

function mockBlobUrls() {
  vi.stubGlobal('URL', class extends URL {
    static createObjectURL = vi.fn(() => 'blob:http://localhost/saved-recording')
    static revokeObjectURL = vi.fn()
  })
}

function savedLibrary(): OfflineLibrary {
  const week = { weekKey: 'diaspora:2026-09-05', shabbatDate: '2026-09-05', hebrewDate: '23 Elul, 5786', parashah: 'Nitzavim', holiday: null, inIsrael: false }
  return {
    audioEnabled: true, revision: 0,
    teaching: {
      savedAt: '2026-09-04T12:00:00Z', audio: new Blob(['saved recording'], { type: 'audio/mpeg' }), timings: Timings,
      publication: { currentWeek: week, isCurrentWeek: true, dvarTorah: {
        week, title: Timings.title, body: Timings.body, audio: Audio, sources: [], tags: [], centralTeaching: null,
        torahGroundingPercent: null, generatedAtUtc: '2026-09-04T12:00:00Z', publishedAtUtc: '2026-09-04T12:00:00Z',
      } },
    },
  }
}
