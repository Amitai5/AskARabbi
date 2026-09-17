import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AnswerAudio } from './AnswerAudio.tsx'
import { VoiceActivityEvent, type VoiceClient } from './voiceClient.ts'

let client: VoiceClient
beforeEach(() => {
  client = { isAvailable: vi.fn(), transcribe: vi.fn(), synthesize: vi.fn().mockResolvedValue(new Blob(['mp3'], { type: 'audio/mpeg' })) }
  vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: vi.fn().mockReturnValue('blob:answer'), revokeObjectURL: vi.fn() }))
  vi.spyOn(HTMLMediaElement.prototype, 'play').mockImplementation(function (this: HTMLMediaElement) { fireEvent.play(this); return Promise.resolve() })
  vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {})
})
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('Saved answer playback', () => {
  it('loads an owned answer on demand, exposes playback controls, and releases audio on navigation', async () => {
    const { unmount } = render(<AnswerAudio conversationId="conversation" messageId="answer" client={client} />)
    expect(client.synthesize).not.toHaveBeenCalled()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Listen to answer' }))
    const player = screen.getByLabelText('Listen to answer')
    await waitFor(() => expect(player).toHaveAttribute('src', 'blob:answer'))
    expect(player).toHaveAttribute('controls')
    expect(client.synthesize).toHaveBeenCalledWith('conversation', 'answer', expect.any(AbortSignal))
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalledOnce()
    unmount()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:answer')
  })

  it('automatically prepares a spoken response and handles blocked autoplay', async () => {
    vi.mocked(HTMLMediaElement.prototype.play).mockRejectedValue(new DOMException('Blocked', 'NotAllowedError'))
    render(<AnswerAudio conversationId="conversation" messageId="answer" autoPlay client={client} />)
    expect(await screen.findByRole('status')).toHaveTextContent('Press Play')
    expect(screen.getByLabelText('Listen to answer')).toHaveAttribute('controls')
  })

  it('shows a recoverable synthesis failure without hiding the answer', async () => {
    vi.mocked(client.synthesize).mockRejectedValue(new Error('Voice unavailable'))
    render(<><p>Validated answer [1]</p><button>View source 1</button><AnswerAudio conversationId="conversation" messageId="answer" autoPlay client={client} /></>)
    expect(await screen.findByRole('status')).toHaveTextContent('Voice unavailable')
    expect(screen.getByText('Validated answer [1]')).toBeVisible()
    expect(screen.getByRole('button', { name: 'View source 1' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Listen to answer' })).toBeEnabled()
  })

  it('cancels synthesis and discards stale audio', async () => {
    let resolve: (blob: Blob) => void = () => {}
    vi.mocked(client.synthesize).mockReturnValue(new Promise(value => { resolve = value }))
    render(<AnswerAudio conversationId="conversation" messageId="answer" autoPlay client={client} />)
    await userEvent.setup().click(screen.getByRole('button', { name: 'Cancel audio' }))
    expect(vi.mocked(client.synthesize).mock.calls[0][2].aborted).toBe(true)
    await act(async () => resolve(new Blob(['late'])))
    expect(URL.createObjectURL).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Listen to answer' })).toBeEnabled()
  })

  it('stops other playback when the microphone or another answer is activated', async () => {
    render(<AnswerAudio conversationId="conversation" messageId="answer" autoPlay client={client} />)
    await waitFor(() => expect(screen.getByLabelText('Listen to answer')).toHaveAttribute('src', 'blob:answer'))
    Object.defineProperty(screen.getByLabelText('Listen to answer'), 'paused', { configurable: true, value: false })
    vi.mocked(HTMLMediaElement.prototype.pause).mockClear()
    act(() => window.dispatchEvent(new Event(VoiceActivityEvent)))
    expect(HTMLMediaElement.prototype.pause).toHaveBeenCalledOnce()
    vi.mocked(HTMLMediaElement.prototype.pause).mockClear()
    fireEvent.play(screen.getByLabelText('Listen to answer'))
    expect(HTMLMediaElement.prototype.pause).not.toHaveBeenCalled()
  })
})
