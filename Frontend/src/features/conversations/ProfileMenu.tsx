import { useEffect, useRef, useState } from 'react'
import { ChevronUp, Gauge, LogOut, UserRound, Wrench } from 'lucide-react'
import type { AuthenticatedUser } from '../auth/authTypes.ts'
import { formatUsageRemainingPercent, type UsageSummary } from '../settings/settingsTypes.ts'

interface ProfileMenuProps {
  user: AuthenticatedUser
  usage: UsageSummary | null
  isLoadingUsage: boolean
  usageError: string | null
  isOffline: boolean
  onOpenUsage(): void
  onOpenSettings(): void
  onLogout(): Promise<void>
}

export function ProfileMenu({ user, usage, isLoadingUsage, usageError, isOffline, onOpenUsage, onOpenSettings, onLogout }: ProfileMenuProps) {
  const [isOpen, setIsOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const usageLabel = isOffline ? 'Offline' : usageError ? 'Unavailable' : usage ? `${formatUsageRemainingPercent(usage)}% left` : isLoadingUsage ? 'Loading…' : 'Unavailable'

  useEffect(() => {
    if (!isOpen) {
      return
    }
    containerRef.current?.querySelector<HTMLButtonElement>('[role="menuitem"]')?.focus()

    function handlePointerDown(event: PointerEvent) {
      if (containerRef.current?.contains(event.target as Node) === false) {
        setIsOpen(false)
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault()
        setIsOpen(false)
        triggerRef.current?.focus()
      } else if (['ArrowUp', 'ArrowDown', 'Home', 'End'].includes(event.key)) {
        const items = Array.from(containerRef.current?.querySelectorAll<HTMLButtonElement>('[role="menuitem"]') ?? [])
        const index = items.findIndex(item => item === document.activeElement)
        event.preventDefault()
        items[event.key === 'Home' ? 0 : event.key === 'End' ? items.length - 1 : (index + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length]?.focus()
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [isOpen])

  return (
    <div ref={containerRef} className="relative border-t border-line px-4 py-4">
      {isOpen ? (
        <div className="readable-menu absolute bottom-[calc(100%+0.5rem)] left-4 right-4 z-20 rounded-xl border border-line bg-paper p-2 shadow-menu" role="menu" aria-label="Profile options">
          <button type="button" role="menuitem" aria-label={`Usage, ${usageLabel}`} onClick={() => { setIsOpen(false); onOpenUsage() }} className="flex min-h-11 w-full items-center gap-3 rounded-lg px-3 font-medium text-ink transition hover:bg-stone">
            <Gauge aria-hidden="true" className="size-[1.1rem] shrink-0" strokeWidth={1.75} />
            <span className="flex-1 text-left">Usage</span>
            <span className="text-xs tabular-nums text-muted">{usageLabel}</span>
          </button>
          <div className="my-1 h-px bg-line" />
          <button
            type="button"
            onClick={() => {
              setIsOpen(false)
              onOpenSettings()
            }}
            className="flex min-h-11 w-full items-center gap-3 rounded-lg px-3 py-2 text-left font-medium text-ink transition hover:bg-stone"
            role="menuitem"
          >
            <Wrench aria-hidden="true" className="size-[1.1rem] shrink-0" strokeWidth={1.75} />
            Settings &amp; Personalization
          </button>
          <div className="my-1 h-px bg-line" />
          <button type="button" onClick={() => void onLogout()} className="flex h-11 w-full items-center gap-3 rounded-lg px-3 font-medium text-ink transition hover:bg-stone" role="menuitem">
            <LogOut aria-hidden="true" className="size-[1.1rem]" strokeWidth={1.75} />
            Log out
          </button>
        </div>
      ) : null}

      <button
        ref={triggerRef}
        type="button"
        aria-expanded={isOpen}
        aria-haspopup="menu"
        aria-label="Open profile menu"
        onClick={() => setIsOpen((current) => !current)}
        className="flex min-h-14 w-full items-center gap-3 rounded-xl px-2 text-left text-ink transition hover:bg-stone-deep/60"
      >
        <span className="flex size-10 shrink-0 items-center justify-center rounded-full border border-ink/35 bg-paper">
          <UserRound aria-hidden="true" className="size-5" strokeWidth={1.65} />
        </span>
        <span className="min-w-0 flex-1 truncate font-semibold">{user.name}</span>
        <ChevronUp aria-hidden="true" className={`size-4 transition-transform ${isOpen ? '' : 'rotate-180'}`} strokeWidth={1.75} />
      </button>
    </div>
  )
}
