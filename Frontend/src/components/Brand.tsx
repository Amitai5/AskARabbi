import { BookOpen } from 'lucide-react'

interface BrandProps {
  compact?: boolean
}

export function Brand({ compact = false }: BrandProps) {
  return (
    <div className="flex items-center gap-3 text-ink" aria-label="AskRabbi">
      <span className="relative inline-flex shrink-0" aria-hidden="true">
        <BookOpen strokeWidth={1.65} className={compact ? 'size-7' : 'size-9'} />
        <span className="absolute -bottom-1 left-1/2 h-2.5 w-px bg-pomegranate" />
      </span>
      <span className={`${compact ? 'text-[1.55rem]' : 'text-[2rem]'} font-display tracking-[-0.035em]`}>
        AskRabbi
      </span>
    </div>
  )
}
