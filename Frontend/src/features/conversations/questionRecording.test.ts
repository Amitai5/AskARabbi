import { afterEach, describe, expect, it, vi } from 'vitest'
import { canRecordQuestion, encodeSpeechPcm, startQuestionRecording } from './questionRecording.ts'

afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

function readBytes(blob: Blob): Promise<ArrayBuffer> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as ArrayBuffer)
    reader.onerror = () => reject(reader.error)
    reader.readAsArrayBuffer(blob)
  })
}

describe('Microphone recording boundary', () => {
  it('encodes silence, signed samples, and clipping as little-endian 16-bit PCM', async () => {
    const blob = encodeSpeechPcm(new Float32Array([-2, -1, 0, 0.5, 1, 2]))
    expect(blob.type).toBe('application/octet-stream')
    const data = new DataView(await readBytes(blob))
    expect(Array.from({ length: 6 }, (_, index) => data.getInt16(index * 2, true))).toEqual([-32768, -32768, 0, 16384, 32767, 32767])
  })

  it('releases a microphone granted after the user cancels permission', async () => {
    const stop = vi.fn()
    const controller = new AbortController()
    let resolve: (stream: MediaStream) => void = () => {}
    vi.stubGlobal('navigator', { mediaDevices: { getUserMedia: vi.fn(() => new Promise<MediaStream>(value => { resolve = value })) } })
    const pending = startQuestionRecording(controller.signal)
    controller.abort()
    resolve({ getTracks: () => [{ stop }] } as unknown as MediaStream)
    await expect(pending).rejects.toThrow()
    expect(stop).toHaveBeenCalledOnce()
  })

  it('releases tracks if the browser cannot initialize its recorder', async () => {
    const stop = vi.fn()
    vi.stubGlobal('navigator', { mediaDevices: { getUserMedia: vi.fn().mockResolvedValue({ getTracks: () => [{ stop }] }) } })
    vi.stubGlobal('MediaRecorder', class { constructor() { throw new Error('Unsupported recorder') } })
    await expect(startQuestionRecording(new AbortController().signal)).rejects.toThrow('Unsupported recorder')
    expect(stop).toHaveBeenCalledOnce()
  })

  it('cancels recording and releases tracks without decoding or uploading', async () => {
    const stopTrack = vi.fn()
    const stopRecorder = vi.fn()
    vi.stubGlobal('navigator', { mediaDevices: { getUserMedia: vi.fn().mockResolvedValue({ getTracks: () => [{ stop: stopTrack }] }) } })
    vi.stubGlobal('MediaRecorder', class {
      state = 'recording'
      start = vi.fn()
      stop() { this.state = 'inactive'; stopRecorder() }
    })
    const controller = new AbortController()
    const recording = await startQuestionRecording(controller.signal)
    controller.abort()
    expect(stopTrack).toHaveBeenCalled()
    expect(stopRecorder).toHaveBeenCalledOnce()
    await expect(recording.stop()).rejects.toThrow()
  })

  it('offers a typing fallback when microphone APIs are absent', () => {
    vi.stubGlobal('navigator', {})
    expect(canRecordQuestion()).toBe(false)
  })
})
