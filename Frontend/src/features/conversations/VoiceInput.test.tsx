import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'
import { useState } from 'react'
import { VoiceInput } from './VoiceInput.tsx'
import { MessageComposer } from './MessageComposer.tsx'
import { canRecordQuestion, startQuestionRecording } from './questionRecording.ts'
import { VoiceActivityEvent, VoiceClient, type VoiceClient as VoiceClientContract } from './voiceClient.ts'

vi.mock('./questionRecording.ts', () => ({ canRecordQuestion: vi.fn(), startQuestionRecording: vi.fn() }))
const stop = vi.fn()
const cancel = vi.fn()
let client: VoiceClientContract

beforeEach(() => {
  vi.mocked(canRecordQuestion).mockReturnValue(true)
  stop.mockReset().mockResolvedValue(new Blob(['pcm']))
  cancel.mockReset()
  vi.mocked(startQuestionRecording).mockReset().mockResolvedValue({ stop, cancel })
  client = { isAvailable: vi.fn().mockResolvedValue(true), transcribe: vi.fn().mockResolvedValue('What is Shabbat?'), synthesize: vi.fn() }
})
afterEach(() => { vi.restoreAllMocks(); vi.useRealTimers() })

function setup(options: { draft?: string; language?: string; disabled?: boolean } = {}) {
  const onTranscript = vi.fn()
  const onBusyChange = vi.fn()
  const view = render(<VoiceInput draft="Existing draft" language="English" disabled={false} {...options} onTranscript={onTranscript} onBusyChange={onBusyChange} client={client} />)
  return { ...view, onTranscript, onBusyChange, user: userEvent.setup() }
}

describe('Push-to-talk question input', () => {
  it('requests the microphone only after activation, transcribes after stop, and preserves the draft', async () => {
    const { user, onTranscript, onBusyChange } = setup()
    expect(startQuestionRecording).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Record question' }))
    expect(await screen.findByRole('button', { name: 'Stop recording' })).toHaveAttribute('aria-pressed', 'true')
    expect(client.transcribe).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Stop recording' }))
    await waitFor(() => expect(onTranscript).toHaveBeenCalledWith('Existing draft\nWhat is Shabbat?'))
    expect(client.transcribe).toHaveBeenCalledWith(expect.any(Blob), 'en-US', expect.any(AbortSignal))
    expect(onBusyChange).toHaveBeenLastCalledWith(false)
    expect(screen.getByRole('status')).toHaveTextContent('Review or edit it')
  })

  it('allows keyboard activation and keeps Send disabled until the transcript is ready', async () => {
    vi.spyOn(VoiceClient, 'isAvailable').mockImplementation(client.isAvailable)
    vi.spyOn(VoiceClient, 'transcribe').mockImplementation(client.transcribe)
    const submit = vi.fn()
    const voiceDraft = vi.fn()
    function Composer() {
      const [draft, setDraft] = useState('Typed')
      return <MessageComposer draft={draft} conversationLanguage="English" quotationLanguage="English" selectedSourceKeys={['collection:Torah']} isSending={false} onDraftChange={setDraft} onSelectedSourceKeysChange={vi.fn()} onSubmit={submit} onVoiceDraft={voiceDraft} />
    }
    render(<Composer />)
    const user = userEvent.setup()
    screen.getByRole('button', { name: 'Record question' }).focus()
    await user.keyboard('{Enter}')
    await screen.findByRole('button', { name: 'Stop recording' })
    expect(screen.getByRole('button', { name: 'Send message' })).toBeDisabled()
    fireEvent.keyDown(screen.getByLabelText('Message AskRabbi'), { key: 'Enter', ctrlKey: true })
    expect(submit).not.toHaveBeenCalled()
    await user.keyboard(' ')
    await waitFor(() => expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('Typed\nWhat is Shabbat?'))
    expect(screen.getByLabelText('Message AskRabbi')).toHaveFocus()
    expect(voiceDraft).toHaveBeenCalledOnce()
    expect(submit).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Send message' }))
    expect(submit).toHaveBeenCalledOnce()
  })

  it('stops recording when a new draft replaces the current conversation without replacing the text field', async () => {
    vi.spyOn(VoiceClient, 'isAvailable').mockImplementation(client.isAvailable)
    const props = { draft: 'Typed', conversationLanguage: 'English', quotationLanguage: 'English', selectedSourceKeys: ['collection:Torah'], isSending: false, onDraftChange: vi.fn(), onSelectedSourceKeysChange: vi.fn(), onSubmit: vi.fn() }
    const view = render(<MessageComposer {...props} voiceScope="first" />)
    const input = screen.getByLabelText('Message AskRabbi')
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Record question' }))
    await screen.findByRole('button', { name: 'Stop recording' })

    view.rerender(<MessageComposer {...props} voiceScope="second" draft="New draft" />)

    expect(cancel).toHaveBeenCalled()
    expect(vi.mocked(startQuestionRecording).mock.calls[0][0].aborted).toBe(true)
    expect(screen.getByLabelText('Message AskRabbi')).toBe(input)
    expect(input).toHaveValue('New draft')
    expect(screen.getByRole('button', { name: 'Send message' })).toBeEnabled()
  })

  it('handles denied permission without changing the typed question', async () => {
    vi.mocked(startQuestionRecording).mockRejectedValue(new DOMException('Denied', 'NotAllowedError'))
    const { user, onTranscript } = setup()
    await user.click(screen.getByRole('button', { name: 'Record question' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Microphone permission was denied')
    expect(onTranscript).not.toHaveBeenCalled()
    expect(client.transcribe).not.toHaveBeenCalled()
  })

  it('does not request a microphone when voice is unconfigured', async () => {
    vi.mocked(client.isAvailable).mockResolvedValue(false)
    const { user } = setup()
    await user.click(screen.getByRole('button', { name: 'Record question' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Voice is not enabled')
    expect(startQuestionRecording).not.toHaveBeenCalled()
  })

  it.each(['No speech was recognized.', 'Sign in again to use voice.', 'Voice is busy.'])('preserves input on provider failure: %s', async message => {
    vi.mocked(client.transcribe).mockRejectedValue(new Error(message))
    const { user, onTranscript } = setup()
    await user.click(screen.getByRole('button', { name: 'Record question' }))
    await user.click(await screen.findByRole('button', { name: 'Stop recording' }))
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(message))
    expect(onTranscript).not.toHaveBeenCalled()
  })

  it('cancels a pending transcription and ignores its late result', async () => {
    let resolve: (text: string) => void = () => {}
    vi.mocked(client.transcribe).mockReturnValue(new Promise(value => { resolve = value }))
    const { user, onTranscript } = setup()
    await user.click(screen.getByRole('button', { name: 'Record question' }))
    await user.click(await screen.findByRole('button', { name: 'Stop recording' }))
    await user.click(screen.getByRole('button', { name: 'Cancel recording' }))
    await act(async () => resolve('Late transcript'))
    expect(onTranscript).not.toHaveBeenCalled()
    expect(screen.getByRole('status')).toHaveTextContent('cancelled')
  })

  it('aborts on navigation and stops a microphone that arrives after cancellation', async () => {
    const { user, unmount, onTranscript } = setup()
    await user.click(screen.getByRole('button', { name: 'Record question' }))
    await screen.findByRole('button', { name: 'Stop recording' })
    const signal = vi.mocked(startQuestionRecording).mock.calls[0][0]
    unmount()
    expect(signal.aborted).toBe(true)
    expect(cancel).toHaveBeenCalled()
    expect(onTranscript).not.toHaveBeenCalled()
  })

  it.each(['offline', VoiceActivityEvent])('stops capture on %s', async event => {
    const { user, onTranscript } = setup()
    await user.click(screen.getByRole('button', { name: 'Record question' }))
    await screen.findByRole('button', { name: 'Stop recording' })
    act(() => window.dispatchEvent(new Event(event)))
    expect(cancel).toHaveBeenCalled()
    expect(onTranscript).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Record question' })).toBeEnabled()
  })

  it('stops automatically after thirty seconds', async () => {
    vi.useFakeTimers()
    const { onTranscript } = setup()
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'Record question' })) })
    await act(async () => { await vi.advanceTimersByTimeAsync(30_000) })
    expect(stop).toHaveBeenCalledOnce()
    expect(onTranscript).toHaveBeenCalledOnce()
  })

  it('does not truncate or overwrite a full draft', async () => {
    const { user, onTranscript } = setup({ draft: 'x'.repeat(4000) })
    await user.click(screen.getByRole('button', { name: 'Record question' }))
    await user.click(await screen.findByRole('button', { name: 'Stop recording' }))
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('too long'))
    expect(onTranscript).not.toHaveBeenCalled()
  })

  it.each([{ language: 'Yiddish' }, { disabled: true }])('retains typed input when capture is unavailable: %j', options => {
    setup(options)
    expect(screen.getByRole('button', { name: 'Record question' })).toBeDisabled()
    expect(startQuestionRecording).not.toHaveBeenCalled()
  })

  it('explains unsupported browsers', () => {
    vi.mocked(canRecordQuestion).mockReturnValue(false)
    setup()
    expect(screen.getByRole('button', { name: 'Record question' })).toBeDisabled()
    expect(screen.getByText(/unavailable in this browser/)).toBeVisible()
  })
})
