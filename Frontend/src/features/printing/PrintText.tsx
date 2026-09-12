import type { ReactNode } from 'react'
import { normalizeDisplayText } from '../../displayText.ts'
import type { PrintSource } from './printTypes.ts'

export function PrintText({ text, sources = [], prefix = '' }: { text: string; sources?: readonly PrintSource[]; prefix?: string }) {
  function inline(value: string): ReactNode[] {
    return value.split(/(\*\*[^*]+\*\*|\*[^*\n]+\*|\[[A-Za-z0-9_-]+\])/g).map((part, index, parts) => {
      if (part.startsWith('**') && part.endsWith('**')) { return <strong key={index}>{inline(part.slice(2, -2))}</strong> }
      if (part.startsWith('*') && part.endsWith('*')) { return <em key={index}>{inline(part.slice(1, -1))}</em> }
      const citation = /^\[([A-Za-z0-9_-]+)\]$/.exec(part)
      const source = citation ? sources.find(item => item.key === citation[1]) : undefined
      const nextCitation = /^\[([A-Za-z0-9_-]+)\]$/.exec(parts[index + 1] ?? '')
      const text = nextCitation && sources.some(item => item.key === nextCitation[1]) ? part.replace(/[ \t]+$/, '\u00a0') : part
      return source ? <a key={index} className="print-citation" href={`#${prefix}-source-${source.number}`} aria-label={`Source ${source.number}`}>[{source.number}]</a> : text
    })
  }
  return normalizeDisplayText(text).trim().split(/\n\s*\n/).filter(Boolean).map((block, index) => {
    const lines = block.split('\n')
    if (lines.every(line => /^\s*[-*•]\s+/.test(line))) { return <ul key={index}>{lines.map((line, i) => <li key={i} dir="auto">{inline(line.replace(/^\s*[-*•]\s+/, ''))}</li>)}</ul> }
    if (lines.every(line => /^\s*\d+[.)]\s+/.test(line))) { return <ol key={index} start={Number.parseInt(lines[0], 10)}>{lines.map((line, i) => <li key={i} dir="auto">{inline(line.replace(/^\s*\d+[.)]\s+/, ''))}</li>)}</ol> }
    if (/^#{1,6}\s+/.test(block) && lines.length === 1) { return <h3 key={index} dir="auto">{inline(block.replace(/^#{1,6}\s+/, ''))}</h3> }
    return <p key={index} dir="auto">{inline(block)}</p>
  })
}
