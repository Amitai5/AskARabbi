import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'
import { createDemoApplicationClients } from './test/demoApplicationClients.ts'
import { VoiceClient } from './features/conversations/voiceClient.ts'

vi.mock('./features/conversations/questionRecording.ts', () => ({
  canRecordQuestion: () => true,
  startQuestionRecording: vi.fn().mockImplementation(async () => ({ stop: async () => new Blob(['pcm']), cancel: vi.fn() })),
}))

beforeEach(() => {
  Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() })
  window.history.replaceState({}, '', '/conversations/new')
  window.sessionStorage.clear()
  vi.spyOn(VoiceClient, 'isAvailable').mockResolvedValue(true)
  vi.spyOn(VoiceClient, 'transcribe').mockResolvedValue('Why is chicken not eaten with dairy?')
  vi.spyOn(VoiceClient, 'synthesize').mockResolvedValue(new Blob(['mp3'], { type: 'audio/mpeg' }))
  vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: vi.fn().mockReturnValue('blob:answer'), revokeObjectURL: vi.fn() }))
  vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue()
  vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {})
})
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals() })

async function recordDraft(clients: ReturnType<typeof createDemoApplicationClients>) {
  const user = userEvent.setup()
  render(<App authClient={clients.authClient} conversationClient={clients.conversationClient} conversationSettingsClient={clients.conversationSettingsClient} />)
  await user.click(await screen.findByRole('button', { name: 'Continue with Google' }))
  await waitFor(() => expect(screen.getByRole('button', { name: 'Record question' })).toBeEnabled())
  await user.click(screen.getByRole('button', { name: 'Record question' }))
  await user.click(await screen.findByRole('button', { name: 'Stop recording' }))
  await waitFor(() => expect(screen.getByLabelText('Message AskRabbi')).toHaveValue('Why is chicken not eaten with dairy?'))
  return user
}

describe('Voice conversation flow', () => {
  it('reviews a spoken question, uses the normal saved turn, and keeps citations interactive during playback', async () => {
    const clients = createDemoApplicationClients()
    const create = vi.spyOn(clients.conversationClient, 'createWithMessage')
    const user = await recordDraft(clients)
    expect(create).not.toHaveBeenCalled()
    expect(VoiceClient.synthesize).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Send message' }))
    await waitFor(() => expect(VoiceClient.synthesize).toHaveBeenCalledOnce())
    expect(create).toHaveBeenCalledWith(expect.any(String), 'Why is chicken not eaten with dairy?', expect.arrayContaining(['collection:Torah']))
    const [conversationId, messageId] = vi.mocked(VoiceClient.synthesize).mock.calls[0]
    const saved = await clients.conversationClient.get(conversationId)
    const answer = saved.messages.find(message => message.id === messageId)
    expect(answer?.role).toBe('Assistant')
    expect(answer?.sources?.length).toBeGreaterThan(0)
    expect(screen.getByLabelText('Listen to answer')).toHaveAttribute('src', 'blob:answer')
    await user.click(screen.getAllByRole('button', { name: 'View source 1' })[0])
    expect(screen.getAllByRole('link', { name: /Mishnah Chullin 8:1/ })[0]).toHaveAttribute('href', 'https://www.sefaria.org/Mishnah_Chullin.8.1')
    expect(screen.getByLabelText('Listen to answer')).toBeInTheDocument()
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalledOnce()
    await user.keyboard('{Escape}')
    await user.click(screen.getByRole('button', { name: 'Open profile menu' }))
    await user.click(screen.getByRole('menuitem', { name: 'Settings & Personalization' }))
    await user.click(screen.getByRole('button', { name: 'Back' }))
    expect(screen.getByRole('button', { name: 'Listen to answer' })).toBeEnabled()
    expect(VoiceClient.synthesize).toHaveBeenCalledOnce()
  })

  it('never synthesizes a failed grounding outcome', async () => {
    const clients = createDemoApplicationClients()
    const create = clients.conversationClient.createWithMessage
    clients.conversationClient.createWithMessage = async (...args) => {
      const turn = await create(...args)
      if ('messages' in turn) { throw new Error('Expected full demo conversation') }
      return { ...turn, status: 'validation_failed', message: 'The answer could not be validated.', conversation: { ...turn.conversation, messages: turn.conversation.messages.filter(message => message.role === 'User') } }
    }
    const user = await recordDraft(clients)

    await user.click(screen.getByRole('button', { name: 'Send message' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The answer could not be validated')
    expect(VoiceClient.synthesize).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: 'Listen to answer' })).not.toBeInTheDocument()
  })
})
