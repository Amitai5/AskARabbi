import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { CheckCircle2, Download, LoaderCircle } from 'lucide-react'
import { Brand } from '../../components/Brand.tsx'

interface InstallPromptEvent extends Event {
  prompt(): Promise<unknown>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

interface InstallContextValue {
  isInstalled: boolean
  openInstall(): void
}

const InstallContext = createContext<InstallContextValue | null>(null)

function isInstalledApp() {
  return window.matchMedia?.('(display-mode: standalone)').matches === true
    || (navigator as Navigator & { standalone?: boolean }).standalone === true
}

export function PwaInstallProvider({ children }: { children: ReactNode }) {
  const [isInstalled, setIsInstalled] = useState(isInstalledApp)
  const [deferredPrompt, setDeferredPrompt] = useState<InstallPromptEvent | null>(null)
  const [isOpen, setIsOpen] = useState(false)
  const [isInstalling, setIsInstalling] = useState(false)
  const [installError, setInstallError] = useState<string | null>(null)
  const submittingRef = useRef(false)
  const openInstall = useCallback(() => setIsOpen(true), [])
  const closeInstall = useCallback(() => setIsOpen(false), [])

  useEffect(() => {
    function handlePrompt(event: Event) {
      if (!('prompt' in event) || typeof event.prompt !== 'function' || !('userChoice' in event)) {
        return
      }
      event.preventDefault()
      setDeferredPrompt(event as InstallPromptEvent)
      setInstallError(null)
    }
    function handleInstalled() {
      setIsInstalled(true)
      setDeferredPrompt(null)
      setIsOpen(false)
    }
    function handleDisplayMode() {
      setIsInstalled(isInstalledApp())
    }
    const displayMode = window.matchMedia?.('(display-mode: standalone)')
    window.addEventListener('beforeinstallprompt', handlePrompt)
    window.addEventListener('appinstalled', handleInstalled)
    displayMode?.addEventListener('change', handleDisplayMode)
    return () => {
      window.removeEventListener('beforeinstallprompt', handlePrompt)
      window.removeEventListener('appinstalled', handleInstalled)
      displayMode?.removeEventListener('change', handleDisplayMode)
    }
  }, [])

  async function install() {
    if (deferredPrompt === null || submittingRef.current) {
      return
    }
    submittingRef.current = true
    setIsInstalling(true)
    setInstallError(null)
    try {
      // The browser prompt must start inside the user's click, not after another asynchronous task.
      await deferredPrompt.prompt()
      const choice = await deferredPrompt.userChoice
      if (choice.outcome === 'accepted') {
        setIsOpen(false)
      }
    } catch {
      setInstallError('Installation could not be started. You can still use the browser instructions below.')
    } finally {
      // A browser install event can only be used once, even if its prompt is dismissed.
      setDeferredPrompt(current => current === deferredPrompt ? null : current)
      submittingRef.current = false
      setIsInstalling(false)
    }
  }

  const context = useMemo(() => ({ isInstalled, openInstall }), [isInstalled, openInstall])
  return (
    <InstallContext.Provider value={context}>
      {children}
      {isOpen && !isInstalled ? <InstallDialog canPrompt={deferredPrompt !== null} isInstalling={isInstalling} error={installError} onInstall={install} onClose={closeInstall} /> : null}
    </InstallContext.Provider>
  )
}

export function InstallAppButton({ disabled = false }: { disabled?: boolean }) {
  const context = useContext(InstallContext)
  if (context === null || context.isInstalled) {
    return null
  }
  return <button type="button" onClick={context.openInstall} disabled={disabled} className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-lg border border-line-strong bg-paper px-3 py-2 text-sm font-semibold text-ink transition hover:border-pomegranate hover:text-pomegranate disabled:cursor-not-allowed disabled:opacity-60"><Download aria-hidden="true" className="size-4" />Install app</button>
}

export function InstallAppPanel() {
  const context = useContext(InstallContext)
  return (
    <div className="border-y border-line py-5">
      <p className="text-ink-soft">Open AskRabbi from your home screen, Dock, or desktop in its own app window. Keep this week’s teaching with you offline; chats still need an internet connection.</p>
      <div className="mt-4">
        {context?.isInstalled ? <p className="inline-flex items-center gap-2 font-semibold text-ink" role="status"><CheckCircle2 aria-hidden="true" className="size-5 text-pomegranate" />You’re using the installed app.</p> : <InstallAppButton />}
      </div>
    </div>
  )
}

interface InstallDialogProps {
  canPrompt: boolean
  isInstalling: boolean
  error: string | null
  onInstall(): Promise<void>
  onClose(): void
}

function InstallDialog({ canPrompt, isInstalling, error, onInstall, onClose }: InstallDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const closeRef = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    const dialog = dialogRef.current
    const trigger = document.activeElement
    dialog?.showModal()
    closeRef.current?.focus()
    return () => {
      dialog?.close()
      if (trigger instanceof HTMLElement && trigger.isConnected) {
        trigger.focus()
      }
    }
  }, [])

  return createPortal(
    <dialog ref={dialogRef} aria-labelledby="install-app-title" aria-describedby="install-app-description" className="pwa-install-dialog m-auto max-h-[calc(100dvh-2rem)] w-[min(34rem,calc(100vw-2rem))] overflow-y-auto rounded-2xl border border-line bg-paper p-6 text-ink shadow-menu sm:p-8" onCancel={event => { event.preventDefault(); onClose() }}>
      <Brand compact />
      <h2 id="install-app-title" className="mt-6 font-display text-3xl leading-tight">Keep your learning close.</h2>
      <p id="install-app-description" className="mt-3 text-base leading-7 text-ink-soft">Install AskRabbi for quick access in its own app window. You may need to sign in again after installation.</p>
      {error ? <p role="alert" className="mt-4 text-sm leading-6 text-pomegranate">{error}</p> : null}
      <details key={canPrompt ? 'native' : 'manual'} open={!canPrompt} className="mt-5 border-y border-line py-4 text-sm leading-6 text-ink-soft">
        <summary className="cursor-pointer font-semibold text-ink">Installation instructions for your device</summary>
        <ul className="mt-3 space-y-3">
          <li><strong>iPhone or iPad:</strong> Open Safari’s Share menu, then choose Add to Home Screen. Enable Open as Web App if shown, then tap Add.</li>
          <li><strong>Android:</strong> Open the browser menu and choose Install app or Add to Home Screen.</li>
          <li><strong>Windows or Linux:</strong> In Chrome or Edge, use the install icon in the address bar or the browser’s app-install menu.</li>
          <li><strong>Mac:</strong> In Safari, choose File → Add to Dock. You can also use the install icon in Chrome or Edge.</li>
        </ul>
      </details>
      <p className="mt-4 text-sm leading-6 text-muted">This week’s teaching is saved after you sign in online, with offline audio enabled by default in Settings. Chats need an internet connection. Installation availability depends on your browser; you can always keep using the website.</p>
      <div className="mt-6 flex flex-wrap justify-end gap-3">
        <button ref={closeRef} type="button" onClick={onClose} className="min-h-11 rounded-lg border border-line-strong px-4 text-sm font-semibold transition hover:bg-stone">Close</button>
        {canPrompt ? <button type="button" disabled={isInstalling} onClick={() => void onInstall()} className="inline-flex min-h-11 items-center gap-2 rounded-lg bg-pomegranate px-4 text-sm font-semibold text-white transition hover:bg-pomegranate-dark disabled:cursor-wait disabled:opacity-60">{isInstalling ? <LoaderCircle aria-hidden="true" className="size-4 animate-spin" /> : <Download aria-hidden="true" className="size-4" />}{isInstalling ? 'Installing…' : 'Install AskRabbi'}</button> : null}
      </div>
    </dialog>, document.body,
  )
}
