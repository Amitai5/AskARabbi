import { ExternalLink } from 'lucide-react'
import type { ConversationMessage, ConversationSource } from './conversationData.ts'

interface AssistantMessageProps {
  message: ConversationMessage
  showSourceContextByDefault: boolean
}

export function AssistantMessage({ message, showSourceContextByDefault }: AssistantMessageProps) {
  const sources = message.sources ?? []
  const sourceNumbers = new Set(sources.map((source) => source.number))

  return (
    <div className="border-l-2 border-pomegranate pl-5">
      <p className="mb-3 font-display text-xl text-ink">AskRabbi</p>
      <div className="space-y-4 text-base leading-7 text-ink sm:text-lg">
        {message.content.split(/\n\n+/).map((paragraph, index) => (
          <p key={`${message.id}-paragraph-${index}`}>{renderParagraph(paragraph, sourceNumbers, message.id)}</p>
        ))}
      </div>

      {sources.length > 0 ? (
        <section className="mt-7 border-t border-line pt-5" aria-label="Sources for this answer">
          <h3 className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Sources and quotations</h3>
          <div className="mt-4 space-y-7">
            {sources.map((source) => <SourceDetails key={`${message.id}-${source.number}`} messageId={message.id} source={source} showContext={showSourceContextByDefault} />)}
          </div>
        </section>
      ) : null}
    </div>
  )
}

function SourceDetails({ messageId, source, showContext }: { messageId: string, source: ConversationSource, showContext: boolean }) {
  const sourceId = createSourceId(messageId, source.number)
  const shouldShowAttribution = hasDistinctAttribution(source.sourceUrl, source.attributionUrl)

  return (
    <div id={sourceId} className="scroll-mt-24">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="text-xs font-bold text-pomegranate">[{source.number}]</span>
        <a href={source.sourceUrl} target="_blank" rel="noreferrer" className="group inline-flex items-center gap-1.5 font-semibold text-ink decoration-pomegranate/50 underline-offset-4 hover:text-pomegranate hover:underline">
          <span>{source.canonicalReference}</span>
          <ExternalLink aria-hidden="true" className="size-3.5 shrink-0 transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5" strokeWidth={1.8} />
          <span className="sr-only">Open source on Sefaria</span>
        </a>
      </div>
      <p className="mt-1 text-xs leading-5 text-muted">{source.title} · {source.edition} · {source.language} · {source.license}</p>

      {source.quotations.map((quotation, index) => (
        <blockquote key={`${sourceId}-quotation-${index}`} className="mt-3 border-l-2 border-brass pl-4 text-[0.98rem] leading-7 text-ink-soft sm:text-base">
          “{quotation}”
        </blockquote>
      ))}

      <details open={showContext} className="mt-3 text-sm text-ink-soft">
        <summary className="w-fit cursor-pointer font-semibold text-ink transition hover:text-pomegranate">Source context{source.isExcerpt ? ' (excerpt)' : ''}</summary>
        <p className="mt-2 whitespace-pre-wrap border-l border-line-strong pl-4 leading-6">{source.context}</p>
      </details>

      {shouldShowAttribution ? <a href={source.attributionUrl} target="_blank" rel="noreferrer" className="mt-2 inline-block text-xs font-semibold text-muted underline decoration-line-strong underline-offset-4 hover:text-pomegranate">Edition attribution</a> : null}
    </div>
  )
}

function renderParagraph(paragraph: string, sourceNumbers: ReadonlySet<number>, messageId: string) {
  return paragraph.split(/(\[\d+\])/g).map((part, index) => {
    const match = /^\[(\d+)\]$/.exec(part)
    const sourceNumber = match === null ? Number.NaN : Number(match[1])
    return sourceNumbers.has(sourceNumber)
      ? <a key={`${messageId}-citation-${sourceNumber}-${index}`} href={`#${createSourceId(messageId, sourceNumber)}`} aria-label={`View source ${sourceNumber}`} className="font-semibold text-pomegranate underline decoration-pomegranate/35 underline-offset-4 hover:decoration-pomegranate">{part}</a>
      : part
  })
}

function createSourceId(messageId: string, sourceNumber: number) {
  return `source-${messageId}-${sourceNumber}`
}

function hasDistinctAttribution(sourceUrl: string, attributionUrl: string) {
  if (sourceUrl === attributionUrl) {
    return false
  }

  try {
    const attribution = new URL(attributionUrl)
    const isSefaria = attribution.hostname === 'www.sefaria.org' || attribution.hostname === 'sefaria.org'
    const isRootPage = attribution.pathname === '/' || attribution.pathname === ''
    return !isSefaria || !isRootPage
  } catch {
    return false
  }
}
