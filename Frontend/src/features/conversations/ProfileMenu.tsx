import { useEffect, useRef, useState } from 'react'
import { ChevronUp, LogOut, SlidersHorizontal, UserRound, Wrench } from 'lucide-react'
import type { AuthenticatedUser } from '../auth/authTypes.ts'

interface ProfileMenuProps {
  user: AuthenticatedUser
  onOpenSettings(): void
  onOpenPersonalization(): void
  onLogout(): Promise<void>
}

export function ProfileMenu({ user, onOpenSettings, onOpenPersonalization, onLogout }: ProfileMenuProps) {
  const [isOpen, setIsOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!isOpen) {
      return
    }

    function handlePointerDown(event: PointerEvent) {
      if (containerRef.current?.contains(event.target as Node) === false) {
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

  return (
    <div ref={containerRef} className="relative border-t border-line px-4 py-4">
      {isOpen ? (
        <div className="absolute bottom-[calc(100%+0.5rem)] left-4 right-4 z-20 rounded-xl border border-line bg-paper p-2 shadow-menu" role="menu" aria-label="Profile options">
          <button
            type="button"
            onClick={() => {
              setIsOpen(false)
              onOpenSettings()
            }}
            className="flex h-11 w-full items-center gap-3 rounded-lg px-3 text-sm font-medium text-ink transition hover:bg-stone"
            role="menuitem"
          >
            <Wrench aria-hidden="true" className="size-[1.1rem]" strokeWidth={1.75} />
            Settings
          </button>
          <button
            type="button"
            onClick={() => {
              setIsOpen(false)
              onOpenPersonalization()
            }}
            className="flex h-11 w-full items-center gap-3 rounded-lg px-3 text-sm font-medium text-ink transition hover:bg-stone"
            role="menuitem"
          >
            <SlidersHorizontal aria-hidden="true" className="size-[1.1rem]" strokeWidth={1.75} />
            Personalization
          </button>
          <div className="my-1 h-px bg-line" />
          <button type="button" onClick={() => void onLogout()} className="flex h-11 w-full items-center gap-3 rounded-lg px-3 text-sm font-medium text-ink transition hover:bg-stone" role="menuitem">
            <LogOut aria-hidden="true" className="size-[1.1rem]" strokeWidth={1.75} />
            Log out
          </button>
        </div>
      ) : null}

      <button
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
        <span className="min-w-0 flex-1 truncate text-sm font-semibold">{user.name}</span>
        <ChevronUp aria-hidden="true" className={`size-4 transition-transform ${isOpen ? '' : 'rotate-180'}`} strokeWidth={1.75} />
      </button>
    </div>
  )
}
