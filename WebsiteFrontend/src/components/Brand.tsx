import { BookOpen } from 'lucide-react'

export function Brand({ compact = false }: { compact?: boolean }) {
  return (
    <span className={`inline-flex items-center text-ink ${compact ? 'gap-2.5' : 'gap-3'}`}>
      <span className="relative inline-flex shrink-0" aria-hidden="true">
        <BookOpen strokeWidth={1.65} className={compact ? 'size-7' : 'size-8'} />
        <span className="absolute -bottom-1 left-1/2 h-2.5 w-px bg-pomegranate" />
      </span>
      <span className={`font-display tracking-[-0.035em] ${compact ? 'text-[1.6rem]' : 'text-[1.9rem]'}`}>AskRabbi</span>
    </span>
  )
}
