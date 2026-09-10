import type { DvarTorahClient } from '../dvarTorah/dvarTorahClient.ts'
import { validateAudioTimings } from '../dvarTorah/dvarTorahAudio.ts'
import { normalizeDvarTorahText } from '../dvarTorah/dvarTorahText.ts'
import { readOfflineLibrary, saveOfflinePublication, saveOfflineRecording } from './offlineLibrary.ts'

export const MaximumOfflineAudioBytes = 64 * 1024 * 1024

export async function syncOfflineTeaching(client: DvarTorahClient, signal: AbortSignal, onTextSaved: () => void) {
  const initial = await readOfflineLibrary()
  const publication = await client.getCurrent()
  if (signal.aborted) { return }
  const library = await saveOfflinePublication(publication, initial.revision, signal)
  if (signal.aborted || library.revision !== initial.revision) { return }
  onTextSaved()
  const teaching = library.teaching
  const article = teaching?.publication.dvarTorah
  if (!library.audioEnabled || !teaching || !article?.audio || teaching.audio && teaching.timings || !publication.isCurrentWeek
    || article.week.weekKey !== publication.currentWeek.weekKey) { return }

  const version = article.audio.version
  const [audio, rawTimings] = await Promise.all([
    teaching.audio ?? downloadOfflineAudio(client.getAudioUrl(article.week.weekKey, version), signal),
    client.getAudioTimings(article.week.weekKey, version, signal).catch(() => null),
  ])
  const timings = validateAudioTimings(rawTimings, version, normalizeDvarTorahText(article.title), normalizeDvarTorahText(article.body))
  if (!signal.aborted) {
    await saveOfflineRecording(teaching, initial.revision, audio, timings, signal)
    if (!timings) { throw new Error('The audio was saved, but its word timings could not be saved yet.') }
  }
}

export async function downloadOfflineAudio(url: string, signal: AbortSignal): Promise<Blob> {
  const response = await fetch(url, { credentials: 'include', signal })
  const contentType = response.headers.get('Content-Type') ?? ''
  const contentLength = Number(response.headers.get('Content-Length'))
  if (response.status !== 200 || !contentType.startsWith('audio/') || contentLength > MaximumOfflineAudioBytes || !response.body) {
    await response.body?.cancel()
    throw new Error('This recording could not be saved for offline listening.')
  }
  const reader = response.body.getReader()
  const chunks: BlobPart[] = []
  let size = 0
  try {
    while (true) {
      const { done, value } = await reader.read()
      if (done) { break }
      size += value.byteLength
      if (size > MaximumOfflineAudioBytes) {
        await reader.cancel()
        throw new Error('This recording is too large to save offline.')
      }
      chunks.push(new Uint8Array(value))
    }
  } finally {
    reader.releaseLock()
  }
  if (size === 0 || contentLength > 0 && size !== contentLength) {
    throw new Error('The recording download was incomplete. Try again online.')
  }
  return new Blob(chunks, { type: contentType })
}
