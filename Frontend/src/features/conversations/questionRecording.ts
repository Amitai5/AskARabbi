export interface QuestionRecording {
  stop(): Promise<Blob>
  cancel(): void
}

export function canRecordQuestion() {
  return Boolean(typeof navigator.mediaDevices?.getUserMedia === 'function' && typeof MediaRecorder !== 'undefined' && typeof AudioContext !== 'undefined' && typeof OfflineAudioContext !== 'undefined')
}

export async function startQuestionRecording(signal: AbortSignal): Promise<QuestionRecording> {
  const stream = await navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true } })
  if (signal.aborted) {
    stream.getTracks().forEach(track => track.stop())
    signal.throwIfAborted()
  }
  let recorder: MediaRecorder
  try {
    recorder = new MediaRecorder(stream)
  } catch (error) {
    stream.getTracks().forEach(track => track.stop())
    throw error
  }
  const chunks: Blob[] = []
  let bytes = 0
  let cancelled = false
  let failed = false
  let stopped = false
  let resolveStopped: (blob: Blob) => void = () => {}
  const result = new Promise<Blob>(resolve => { resolveStopped = resolve })
  function release() {
    stream.getTracks().forEach(track => track.stop())
    signal.removeEventListener('abort', cancel)
  }
  function cancel() {
    cancelled = true
    chunks.length = 0
    if (recorder.state !== 'inactive') { recorder.stop() }
    release()
    resolveStopped(new Blob())
  }
  recorder.ondataavailable = event => {
    bytes += event.data.size
    if (bytes > 4_000_000) { failed = true; cancel() }
    else if (!cancelled && event.data.size > 0) { chunks.push(event.data) }
  }
  recorder.onerror = () => { failed = true; cancel() }
  recorder.onstop = () => {
    release()
    resolveStopped(new Blob(chunks, { type: recorder.mimeType }))
    chunks.length = 0
  }
  signal.addEventListener('abort', cancel, { once: true })
  try {
    recorder.start(250)
  } catch (error) {
    release()
    throw error
  }
  return {
    cancel,
    async stop() {
      if (stopped) { throw new Error('This recording has already finished.') }
      stopped = true
      if (recorder.state !== 'inactive') { recorder.stop() }
      release()
      const blob = await result
      signal.throwIfAborted()
      if (failed || cancelled || blob.size === 0) { throw new Error('The recording could not be captured. Try again or type your question.') }
      return decodeQuestion(blob, signal)
    },
  }
}

async function decodeQuestion(blob: Blob, signal: AbortSignal) {
  const context = new AudioContext()
  try {
    const decoded = await context.decodeAudioData(await blob.arrayBuffer())
    signal.throwIfAborted()
    if (decoded.duration < 0.1 || decoded.duration > 31) { throw new Error('Keep your recording between a tenth of a second and 30 seconds.') }
    const resampler = new OfflineAudioContext(1, Math.min(30 * 16000, Math.ceil(decoded.duration * 16000)), 16000)
    const source = resampler.createBufferSource()
    source.buffer = decoded
    source.connect(resampler.destination)
    source.start()
    const audio = await resampler.startRendering()
    signal.throwIfAborted()
    return encodeSpeechPcm(audio.getChannelData(0))
  } finally {
    await context.close()
  }
}

export function encodeSpeechPcm(samples: Float32Array): Blob {
  const buffer = new ArrayBuffer(samples.length * 2)
  const view = new DataView(buffer)
  samples.forEach((sample, index) => {
    const value = Math.max(-1, Math.min(1, sample))
    view.setInt16(index * 2, Math.round(value * (value < 0 ? 32768 : 32767)), true)
  })
  return new Blob([buffer], { type: 'application/octet-stream' })
}
