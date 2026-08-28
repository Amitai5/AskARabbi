import { useEffect, useRef, useState } from 'react'
import { BookOpenCheck, Check, ChevronDown } from 'lucide-react'
import { AllSourceKeys, SourceOptions } from './sourceOptions.ts'

interface SourceFilterMenuProps {
  selectedSourceKeys: readonly string[]
  onChange(sourceKeys: string[]): void
}

export function SourceFilterMenu({ selectedSourceKeys, onChange }: SourceFilterMenuProps) {
  const [isOpen, setIsOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const selectedSourceKeySet = new Set(selectedSourceKeys)
  const areAllSourcesSelected = selectedSourceKeys.length === SourceOptions.length
  const selectionLabel = areAllSourcesSelected ? 'All sources' : selectedSourceKeys.length === 0 ? 'Choose sources' : `${selectedSourceKeys.length} sources`

  useEffect(() => {
    if (!isOpen) {
      return
    }

    function handlePointerDown(event: PointerEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        setIsOpen(false)
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setIsOpen(false)
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [isOpen])

  function toggleSource(sourceKey: string) {
    const nextSelectedSourceKeys = selectedSourceKeySet.has(sourceKey)
      ? selectedSourceKeys.filter((key) => key !== sourceKey)
      : SourceOptions.filter((source) => selectedSourceKeySet.has(source.key) || source.key === sourceKey).map((source) => source.key)
    onChange([...nextSelectedSourceKeys])
  }

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        aria-expanded={isOpen}
        aria-controls="conversation-source-filter"
        aria-label={`Choose sources: ${selectionLabel}`}
        onClick={() => setIsOpen((current) => !current)}
        className="inline-flex h-9 items-center gap-2 rounded-lg px-2 text-xs font-semibold text-ink-soft transition hover:bg-stone hover:text-ink"
      >
        <BookOpenCheck aria-hidden="true" className="size-4 text-pomegranate" strokeWidth={1.8} />
        <span>{selectionLabel}</span>
        <ChevronDown aria-hidden="true" className={`size-3.5 transition-transform ${isOpen ? 'rotate-180' : ''}`} strokeWidth={1.8} />
      </button>

      {isOpen ? (
        <div id="conversation-source-filter" role="dialog" aria-label="Sources used for this conversation" className="absolute bottom-full left-0 z-30 mb-3 w-[min(22rem,calc(100vw-2rem))] overflow-hidden rounded-xl border border-line-strong bg-paper shadow-menu">
          <div className="flex items-start justify-between gap-4 border-b border-line px-4 py-3.5">
            <div>
              <p className="text-sm font-semibold text-ink">Sources used</p>
              <p className="mt-1 text-xs leading-5 text-muted">Only enabled sources will ground this conversation.</p>
            </div>
            <div className="flex shrink-0 gap-1">
              <button type="button" aria-label="Select all sources" onClick={() => onChange([...AllSourceKeys])} className="rounded-md px-2 py-1 text-xs font-semibold text-pomegranate transition hover:bg-stone">All</button>
              <button type="button" aria-label="Clear all sources" onClick={() => onChange([])} className="rounded-md px-2 py-1 text-xs font-semibold text-ink-soft transition hover:bg-stone">Clear</button>
            </div>
          </div>

          <div className="max-h-[min(27rem,60vh)] overflow-y-auto px-2 py-2">
            {(['Core collections', 'Major works'] as const).map((group) => (
              <div key={group} className="py-1">
                <p className="px-2 pb-1 pt-1 text-[0.68rem] font-semibold uppercase tracking-[0.14em] text-muted">{group}</p>
                {SourceOptions.filter((source) => source.group === group).map((source) => {
                  const isSelected = selectedSourceKeySet.has(source.key)
                  return (
                    <label key={source.key} className="flex min-h-12 cursor-pointer items-center gap-3 rounded-lg px-2 py-2 transition hover:bg-stone">
                      <input type="checkbox" aria-label={source.label} checked={isSelected} onChange={() => toggleSource(source.key)} className="sr-only" />
                      <span aria-hidden="true" className={`flex size-5 shrink-0 items-center justify-center rounded border transition ${isSelected ? 'border-pomegranate bg-pomegranate text-white' : 'border-line-strong bg-paper text-transparent'}`}>
                        <Check className="size-3.5" strokeWidth={2.2} />
                      </span>
                      <span className="min-w-0">
                        <span className="block text-sm font-semibold text-ink">{source.label}</span>
                        <span className="block truncate text-xs text-muted">{source.description}</span>
                      </span>
                    </label>
                  )
                })}
              </div>
            ))}
          </div>
        </div>
      ) : null}
    </div>
  )
}
