import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createRef, StrictMode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { DvarTorahReadAloud, type DvarTorahPlaybackHandle } from './DvarTorahReadAloud.tsx'
import type { DvarTorahClient } from './dvarTorahClient.ts'
import type { DvarTorahAudioTimings, WeeklyDvarTorahAudio } from './dvarTorahTypes.ts'

const Audio: WeeklyDvarTorahAudio = { version: 'v1', voice: 'Andrew', durationMs: 10_000, audioUrl: '', timingsUrl: '' }
const Timings: DvarTorahAudioTimings = { schemaVersion: 1, version: 'v1', title: 'A teaching', body: 'Learn together.', durationMs: 10_000, words: [
  { section: 'body', text: 'Learn', textOffset: 0, textLength: 5, audioOffsetMs: 500, durationMs: 500 },
] }

beforeEach(() => {
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
})

describe('DvarTorahReadAloud', () => {
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
    getCurrent: vi.fn(), getArchive: vi.fn(), getArchived: vi.fn(),
    getAudioUrl: vi.fn(() => 'https://api.askarabbi.test/audio?version=v1'),
    getAudioTimings: vi.fn().mockResolvedValue(Timings),
  }
}

function renderPlayer(client: DvarTorahClient, audio: WeeklyDvarTorahAudio | null = Audio, onWordChange = vi.fn()) {
  return render(<DvarTorahReadAloud audio={audio} weekKey="diaspora:2026-09-05" title={Timings.title} body={Timings.body} client={client} onWordChange={onWordChange} />)
}
