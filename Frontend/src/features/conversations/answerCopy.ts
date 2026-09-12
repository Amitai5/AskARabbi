import { normalizeDisplayText } from '../../displayText.ts'
import type { ConversationMessage, ConversationSource } from './conversationData.ts'

export type AnswerCopyMode = 'text' | 'sources'

export function formatAnswerForClipboard(message: ConversationMessage, mode: AnswerCopyMode): string {
  const content = normalizeDisplayText(message.content)
  if (mode === 'text' || !message.sources?.length) {
    return content
  }

  return `${content}\n\nSources\n\n${message.sources.map(formatSource).join('\n\n')}`
}

function formatSource(source: ConversationSource): string {
  // Use the stored source snapshot; never infer links or alter validated quotations.
  return [
    `[${source.number}] ${source.canonicalReference}`,
    [source.title, source.hebrewTitle].filter(Boolean).join(' · '),
    source.edition ? `Edition: ${source.edition}` : '',
    source.language ? `Language: ${source.language}` : '',
    source.collection ? `Collection: ${source.collection}` : '',
    source.license ? `License: ${source.license}` : '',
    source.sourceUrl,
    source.attributionUrl ? `Attribution: ${source.attributionUrl}` : '',
    ...source.quotations.map(quotation => `“${quotation}”`),
  ].filter(Boolean).join('\n')
}
