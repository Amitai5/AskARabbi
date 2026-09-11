import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { ArrowLeft, Search, X } from 'lucide-react'
import { searchSettings, SettingsSections, type SettingsSectionId } from './settingsRegistry.ts'

interface Props {
  section: SettingsSectionId
  isMobileOpen: boolean
  onClose(): void
  onBack(): void
  onNavigate(section: SettingsSectionId, settingId?: string, keepNavigationOpen?: boolean): void
}

export function SettingsSidebar({ section, isMobileOpen, onClose, onBack, onNavigate }: Props) {
  const [query, setQuery] = useState('')
  const panel = useRef<HTMLElement>(null)
  const results = searchSettings(query)
  useEffect(() => {
    if (!isMobileOpen) { return }
    const previous = document.activeElement
    panel.current?.querySelector<HTMLInputElement>('input')?.focus()
    const element = panel.current
    return () => { if ((element?.contains(document.activeElement) || document.activeElement === document.body) && previous instanceof HTMLElement && previous.isConnected) { previous.focus({ preventScroll: true }) } }
  }, [isMobileOpen])

  function handleKey(event: KeyboardEvent<HTMLElement>) {
    if (event.key === 'Escape') { event.preventDefault(); if (query) { setQuery('') } else { onClose() } }
    if (event.key === 'Tab' && isMobileOpen) {
      const elements = Array.from(panel.current?.querySelectorAll<HTMLElement>('button, input, a[href]') ?? []).filter(element => element.tabIndex >= 0 && element.getClientRects().length > 0)
      if (event.shiftKey && document.activeElement === elements[0]) { event.preventDefault(); elements.at(-1)?.focus() }
      else if (!event.shiftKey && document.activeElement === elements.at(-1)) { event.preventDefault(); elements[0]?.focus() }
    }
  }

  function moveTab(event: KeyboardEvent<HTMLAnchorElement>, index: number) {
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) { return }
    event.preventDefault()
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? SettingsSections.length - 1 : (index + (event.key === 'ArrowDown' ? 1 : -1) + SettingsSections.length) % SettingsSections.length
    panel.current?.querySelectorAll<HTMLAnchorElement>('[role="tab"]')[next]?.focus()
    onNavigate(SettingsSections[next].id, undefined, true)
  }

  return <aside ref={panel} role={isMobileOpen ? 'dialog' : undefined} aria-modal={isMobileOpen ? true : undefined} onKeyDown={handleKey} aria-label="Settings navigation" className={`fixed inset-y-0 left-0 z-40 flex w-[min(22rem,calc(100vw-3rem))] shrink-0 flex-col border-r border-line bg-stone text-base text-ink transition-transform lg:relative lg:visible lg:w-[22rem] lg:translate-x-0 lg:text-lg ${isMobileOpen ? 'visible translate-x-0' : 'invisible -translate-x-full'}`}>
    <div className="p-4 sm:p-5">
      <div className="flex items-center justify-between gap-2"><button type="button" onClick={onBack} className="inline-flex min-h-11 items-center gap-2 rounded-lg px-2 font-semibold hover:bg-stone-deep"><ArrowLeft aria-hidden="true" className="size-4" />Back</button><button type="button" aria-label="Close settings navigation" onClick={onClose} className="flex size-11 items-center justify-center rounded-lg hover:bg-stone-deep lg:hidden"><X aria-hidden="true" className="size-5" /></button></div>
      <h2 className="my-5 font-display text-2xl leading-tight">Settings &amp;<br />Personalization</h2>
      <label htmlFor="settings-search" className="sr-only">Search settings</label>
      <div className="relative"><Search aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted" /><input id="settings-search" type="search" value={query} onChange={event => setQuery(event.target.value)} onKeyDown={event => { if (event.key === 'ArrowDown' && query.trim()) { event.preventDefault(); panel.current?.querySelector<HTMLButtonElement>('[data-search-result]')?.focus() } }} placeholder="Search settings…" className="min-h-11 w-full rounded-lg border border-line-strong bg-paper py-2 pl-9 pr-3 text-ink" /></div>
    </div>
    <div className="sidebar-scroll min-h-0 flex-1 overflow-y-auto px-4 pb-6">
      {query.trim() ? <div><p role="status" className="mb-3 px-2 text-sm text-muted">{results.length ? `${results.length} ${results.length === 1 ? 'setting' : 'settings'} found` : 'No settings found. Try a different word.'}</p><ul className="space-y-1">{results.map(result => <li key={result.id}><button type="button" data-search-result aria-label={`${result.label}, ${SettingsSections.find(value => value.id === result.section)?.label}`} onClick={() => onNavigate(result.section, result.id)} className="w-full rounded-lg px-3 py-3 text-left transition hover:bg-stone-deep"><span className="block font-semibold">{result.label}</span><span className="mt-1 block text-sm text-muted">{SettingsSections.find(value => value.id === result.section)?.label}</span></button></li>)}</ul></div>
        : <nav aria-label="Settings sections"><div role="tablist" aria-label="Settings sections" aria-orientation="vertical" className="space-y-1">{SettingsSections.map((item, index) => <a key={item.id} id={`settings-tab-${item.id}`} href={`/settings/${item.id}`} role="tab" aria-selected={section === item.id} aria-controls="settings-panel" tabIndex={section === item.id ? 0 : -1} onKeyDown={event => moveTab(event, index)} onClick={event => { if (!event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) { event.preventDefault(); onNavigate(item.id) } }} className={`block rounded-lg border-l-2 px-4 py-3 font-medium transition hover:bg-stone-deep ${section === item.id ? 'border-pomegranate bg-stone-deep text-ink' : 'border-transparent text-ink-soft'}`}>{item.label}</a>)}</div></nav>}
    </div>
  </aside>
}
