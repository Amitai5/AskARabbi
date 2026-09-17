import { afterEach, describe, expect, it, vi } from 'vitest'
import { createVoiceClient } from './voiceClient.ts'

afterEach(() => vi.unstubAllGlobals())

describe('Authenticated speech transport', () => {
  it('uploads bounded PCM without exposing credentials or sending draft text', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ text: 'שלום' }), { status: 200 }))
    vi.stubGlobal('fetch', fetch)
    const client = createVoiceClient('https://api.example.test')
    const body = new Blob(['pcm'], { type: 'application/octet-stream' })
    const signal = new AbortController().signal
    expect(await client.transcribe(body, 'he-IL', signal)).toBe('שלום')
    expect(fetch).toHaveBeenCalledWith('https://api.example.test/api/voice/transcriptions?language=he-IL', {
      method: 'POST', credentials: 'include', cache: 'no-store', headers: { 'Content-Type': 'application/octet-stream' }, body, signal,
    })
  })

  it('requests narration using only saved conversation and message identifiers', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response('mp3', { headers: { 'Content-Type': 'audio/mpeg' } }))
    vi.stubGlobal('fetch', fetch)
    const signal = new AbortController().signal
    const result = await createVoiceClient('https://api.example.test').synthesize('conversation/id', 'answer/id', signal)
    expect(result.type).toBe('audio/mpeg')
    expect(fetch).toHaveBeenCalledWith('https://api.example.test/api/voice/conversations/conversation%2Fid/messages/answer%2Fid/audio', { method: 'POST', credentials: 'include', cache: 'no-store', signal })
  })

  it.each([[401, 'Sign in again'], [429, 'Wait a minute'], [503, 'still type']])('explains HTTP %s without losing text', async (status, message) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status })))
    await expect(createVoiceClient().isAvailable(new AbortController().signal)).rejects.toThrow(message)
  })
})
