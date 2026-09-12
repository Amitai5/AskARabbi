import { useEffect, useRef, useState } from 'react'
import { BookOpenCheck, Check, ChevronDown } from 'lucide-react'
import { AllSourceKeys, CoreSourceKeys, SourceOptions } from './sourceOptions.ts'

interface SourceFilterMenuProps {
  selectedSourceKeys: readonly string[]
  isDisabled: boolean
  onChange(sourceKeys: string[]): void
}

export function SourceFilterMenu({ selectedSourceKeys, isDisabled, onChange }: SourceFilterMenuProps) {
  const [isOpen, setIsOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const selectedSourceKeySet = new Set(selectedSourceKeys)
  const areAllSourcesSelected = selectedSourceKeys.length === SourceOptions.length
  const areCoreSourcesSelected = selectedSourceKeys.length === CoreSourceKeys.length && CoreSourceKeys.every((sourceKey) => selectedSourceKeySet.has(sourceKey))
  const selectionLabel = areAllSourcesSelected ? 'All sources' : areCoreSourcesSelected ? 'Core sources' : selectedSourceKeys.length === 0 ? 'Choose sources' : `${selectedSourceKeys.length} sources`

  useEffect(() => {
    if (isDisabled || !isOpen) {
      return
    }

    function handlePointerDown(event: PointerEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        setIsOpen(false)
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault()
        setIsOpen(false)
        triggerRef.current?.focus()
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [isDisabled, isOpen])

  function toggleSource(sourceKey: string) {
    if (isDisabled) {
      return
    }

    const nextSelectedSourceKeys = selectedSourceKeySet.has(sourceKey)
      ? selectedSourceKeys.filter((key) => key !== sourceKey)
      : SourceOptions.filter((source) => selectedSourceKeySet.has(source.key) || source.key === sourceKey).map((source) => source.key)
    onChange([...nextSelectedSourceKeys])
  }

  return (
    <div ref={containerRef} className="relative">
      <button
        ref={triggerRef}
        type="button"
        disabled={isDisabled}
        aria-expanded={isOpen}
        aria-haspopup="dialog"
        aria-controls="conversation-source-filter"
        aria-label={`Choose sources: ${selectionLabel}`}
        onClick={() => setIsOpen((current) => !current)}
        className="inline-flex h-9 items-center gap-2 rounded-lg px-2 font-semibold text-ink-soft transition hover:bg-stone hover:text-ink"
      >
        <BookOpenCheck aria-hidden="true" className="size-4 text-pomegranate" strokeWidth={1.8} />
        <span>{selectionLabel}</span>
        <ChevronDown aria-hidden="true" className={`size-3.5 transition-transform ${isOpen ? 'rotate-180' : ''}`} strokeWidth={1.8} />
      </button>

      {isOpen ? (
        <div id="conversation-source-filter" role="dialog" aria-label="Sources used for this conversation" className="readable-menu absolute bottom-full left-0 z-30 mb-3 flex max-h-[min(38rem,calc(100dvh-12rem))] w-[min(26rem,calc(100vw-4rem))] flex-col overflow-hidden rounded-xl border border-line-strong bg-paper shadow-menu">
          <div className="shrink-0 border-b border-line px-4 pt-4 pb-2">
            <div>
              <p className="text-sm font-semibold text-ink">Sources used</p>
              <p className="mt-1 text-xs leading-5 text-muted">Only enabled sources will ground this conversation.</p>
            </div>
            <div className="mt-2 flex gap-2">
              <button type="button" disabled={isDisabled} aria-label="Select core sources" onClick={() => onChange([...CoreSourceKeys])} className="rounded-md px-2 py-1 text-sm font-semibold text-pomegranate transition hover:bg-stone disabled:cursor-wait disabled:opacity-50">Core</button>
              <button type="button" disabled={isDisabled} aria-label="Select all sources" onClick={() => onChange([...AllSourceKeys])} className="rounded-md px-2 py-1 text-sm font-semibold text-pomegranate transition hover:bg-stone disabled:cursor-wait disabled:opacity-50">All</button>
              <button type="button" disabled={isDisabled} aria-label="Clear all sources" onClick={() => onChange([])} className="rounded-md px-2 py-1 text-sm font-semibold text-ink-soft transition hover:bg-stone disabled:cursor-wait disabled:opacity-50">Clear</button>
            </div>
          </div>

          <div className="sidebar-scroll min-h-0 overflow-y-auto overscroll-contain px-2 py-2">
            {(['Core collections', 'Major works'] as const).map((group) => (
              <div key={group} className="py-1">
                <p className="px-2 pb-2 pt-2 text-xs font-semibold uppercase tracking-[0.08em] text-muted">{group}</p>
                {SourceOptions.filter((source) => source.group === group).map((source) => {
                  const isSelected = selectedSourceKeySet.has(source.key)
                  return (
                    <label key={source.key} className="flex min-h-14 cursor-pointer items-center gap-3 rounded-lg px-2 py-2.5 transition hover:bg-stone focus-within:bg-stone">
                      <input type="checkbox" disabled={isDisabled} aria-label={source.label} checked={isSelected} onChange={() => toggleSource(source.key)} className="peer sr-only" />
                      <span aria-hidden="true" className={`flex size-[20px] shrink-0 items-center justify-center rounded border transition peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-pomegranate ${isSelected ? 'border-pomegranate bg-pomegranate text-white' : 'border-line-strong bg-paper text-transparent'}`}>
                        <Check className="size-3.5" strokeWidth={2.2} />
                      </span>
                      <span className="min-w-0">
                        <span className="block text-sm font-semibold text-ink">{source.label}</span>
                        <span className="mt-0.5 block text-xs text-muted">{source.description}</span>
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
