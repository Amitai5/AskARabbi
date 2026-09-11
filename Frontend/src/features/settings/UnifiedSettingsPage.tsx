import { useEffect, useLayoutEffect, useRef } from 'react'
import { SettingsPage, type SettingsPageProps } from './SettingsPage.tsx'
import { SettingsSections, type SettingsSectionId } from './settingsRegistry.ts'
import { PersonalizationPage } from '../personalization/PersonalizationPage.tsx'
import type { PersonalizationProfile } from '../personalization/personalizationTypes.ts'
import type { ConversationSettingsClient } from '../personalization/conversationSettingsClient.ts'
import { ReadingSettings } from '../reading/ReadingSettings.tsx'

interface Props extends SettingsPageProps {
  section: SettingsSectionId
  targetSetting: string | null
  navigationKey: number
  profile: PersonalizationProfile
  client: ConversationSettingsClient
  onSavePersonalization(profile: PersonalizationProfile): Promise<PersonalizationProfile>
}

export function UnifiedSettingsPage({ section, targetSetting, navigationKey, profile, client, onSavePersonalization, ...settingsProps }: Props) {
  const scroll = useRef<HTMLElement>(null)
  const selected = SettingsSections.find(value => value.id === section) ?? SettingsSections[0]
  useEffect(() => {
    const previous = document.title
    document.title = `${selected.label} · Settings & Personalization · AskRabbi`
    return () => { document.title = previous }
  }, [selected.label])
  useLayoutEffect(() => {
    const container = scroll.current
    if (!container) { return }
    const element = targetSetting ? document.getElementById(`setting-${targetSetting}`) : null
    const target = element && container.contains(element) ? element : null
    if (!target) { container.scrollTo({ top: 0 }); return }
    target.classList.add('settings-highlight')
    const control = target.querySelector<HTMLElement>('input:not(:disabled), select:not(:disabled), textarea:not(:disabled), button:not(:disabled), a[href]')
    ;(control ?? target).focus({ preventScroll: true })
    container.scrollTo({ top: Math.max(0, container.scrollTop + target.getBoundingClientRect().top - container.getBoundingClientRect().top - 32) })
    const timer = window.setTimeout(() => target.classList.remove('settings-highlight'), 2200)
    return () => { window.clearTimeout(timer); target.classList.remove('settings-highlight') }
  }, [section, targetSetting, navigationKey])

  return <section ref={scroll} id="settings-panel" role="tabpanel" aria-labelledby="settings-section-title" tabIndex={0} className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-5 sm:px-8">
    <div className="mx-auto w-full max-w-[58rem] pb-16 pt-7 text-base leading-7 sm:pt-10 sm:text-lg">
      <header className="mb-8 border-b border-line pb-6"><p className="text-sm font-semibold uppercase tracking-[0.13em] text-pomegranate">Settings &amp; Personalization</p><h1 id="settings-section-title" className="mt-3 font-display text-[clamp(2.2rem,4vw,3.1rem)] leading-tight tracking-tight text-ink">{selected.label}</h1><p className="mt-3 text-ink-soft">{selected.description}</p></header>
      {/* Keep existing forms mounted while switching sections, preserving unsaved edits. */}
      <div hidden={section !== 'personalization'}><PersonalizationPage embedded profile={profile} client={client} onBack={settingsProps.onBack} onSave={onSavePersonalization} /></div>
      <div hidden={section !== 'reading'}><ReadingSettings /></div>
      <div hidden={section === 'personalization'}><SettingsPage {...settingsProps} section={section} /></div>
    </div>
  </section>
}
