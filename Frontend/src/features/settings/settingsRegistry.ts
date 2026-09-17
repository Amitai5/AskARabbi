export const SettingsSections = [
  { id: 'account', label: 'Account', description: 'Manage account access and review your monthly allowance.' },
  { id: 'personalization', label: 'Personalization', description: 'Make AskRabbi yours. Choose the details that shape your conversations.' },
  { id: 'reading', label: 'Reading', description: 'A comfortable space to read, at your own pace.' },
  { id: 'notifications', label: 'Notifications', description: 'Choose what you hear from us.' },
  { id: 'app', label: 'App & offline', description: 'Keep your learning close, with or without a connection.' },
  { id: 'data', label: 'Your data', description: 'You’re in control of your conversations and account.' },
] as const

export type SettingsSectionId = typeof SettingsSections[number]['id']
interface SettingDefinition { id: string; section: SettingsSectionId; label: string; description: string; keywords: string }

// Every setting's search metadata and navigation target live here. SettingAnchor and
// form labels use these IDs, so adding a registered control also adds its search result.
export const SettingsRegistry: readonly SettingDefinition[] = [
  { id: 'account-email', section: 'account', label: 'Account email', description: 'Your sign-in email and verification status.', keywords: 'login address verified' },
  { id: 'password', section: 'account', label: 'Password', description: 'Request a secure link to choose a new password.', keywords: 'reset security login access' },
  { id: 'usage', section: 'account', label: 'Monthly usage limit', description: 'Your chat allowance and next reset.', keywords: 'quota remaining billing subscription tokens' },
  { id: 'full-name', section: 'personalization', label: 'Full name', description: 'Your preferred name.', keywords: 'profile identity' },
  { id: 'birth-date-time', section: 'personalization', label: 'Birth date and time', description: 'Use the local time at your birthplace.', keywords: 'birthday age bar bat mitzvah' },
  { id: 'locations', section: 'personalization', label: 'Location & time zone', description: 'Current location and birthplace, using a U.S. ZIP code or city.', keywords: 'timezone postal country calendar shabbat hebrew dates Israel diaspora sunset' },
  { id: 'conversation-language', section: 'personalization', label: 'Conversation language', description: 'AskRabbi will answer in this language.', keywords: 'English Hebrew translation' },
  { id: 'quotation-language', section: 'personalization', label: 'Torah and source quotations', description: 'Prefer quotations in this language when an approved edition is available.', keywords: 'translation English Hebrew' },
  { id: 'religious-movement', section: 'personalization', label: 'Religious movement or practice', description: 'Customs that may matter to you.', keywords: 'Jewish background orthodox reform conservative' },
  { id: 'jewish-heritage', section: 'personalization', label: 'Heritage or community', description: 'Your Jewish heritage and community.', keywords: 'background ashkenazi sephardi mizrahi' },
  { id: 'additional-context', section: 'personalization', label: 'Additional information', description: 'Optional context for a more useful conversation.', keywords: 'custom instructions occupation family studying accessibility' },
  { id: 'text-size', section: 'reading', label: 'Text size', description: 'Change answer and teaching text, without resizing buttons or navigation.', keywords: 'font small default large extra large accessibility' },
  { id: 'line-spacing', section: 'reading', label: 'Line spacing', description: 'Choose how much breathing room to leave between lines.', keywords: 'compact default relaxed height accessibility' },
  { id: 'theme', section: 'reading', label: 'Theme', description: 'Choose a light or dark appearance, or follow your device.', keywords: 'system night day color appearance' },
  { id: 'focused-reading', section: 'reading', label: 'Focused reading view', description: 'Open long chat answers in a distraction-free column by default.', keywords: 'focus hide sidebar long content distraction accessibility' },
  { id: 'source-context', section: 'reading', label: 'Show source context by default', description: 'Open the supporting quotations and surrounding text with each answer.', keywords: 'references citations sources' },
  { id: 'enter-key', section: 'reading', label: 'Choose how Enter works', description: 'Enter adds a new line by default. Ctrl/Cmd+Enter sends in either mode; Shift+Enter adds a new line.', keywords: 'keyboard shortcut send submit message composer return accidental newline chat conversation' },
  { id: 'product-updates', section: 'notifications', label: 'Email me product updates', description: 'Receive occasional AskRabbi development and feature announcements.', keywords: 'newsletter notifications unsubscribe marketing' },
  { id: 'install-app', section: 'app', label: 'Install AskRabbi', description: 'Open AskRabbi from your home screen or desktop.', keywords: 'pwa mobile application download' },
  { id: 'offline-audio', section: 'app', label: 'Make weekly audio available offline', description: 'Save this week’s recording with word highlighting and tap-to-seek on this device.', keywords: 'dvar torah download timings storage recording listen' },
  { id: 'saved-teaching', section: 'app', label: 'Open saved teaching', description: 'Read this week’s offline Dvar Torah.', keywords: 'offline text references download' },
  { id: 'privacy-policy', section: 'data', label: 'Privacy Policy', description: 'How your information is used, shared, retained, and deleted.', keywords: 'legal data AI training providers cookies consent rights contact' },
  { id: 'terms-of-service', section: 'data', label: 'Terms of Service', description: 'Rules for using AskRabbi and the limits of AI guidance.', keywords: 'legal terms of use agreement liability psak copyright' },
  { id: 'delete-chats', section: 'data', label: 'Delete all chats', description: 'Permanently remove your saved conversations.', keywords: 'history privacy erase data' },
  { id: 'delete-account', section: 'data', label: 'Delete account', description: 'Permanently leave AskRabbi and remove account data.', keywords: 'privacy erase personal data' },
]

export function settingDefinition(id: string) {
  const setting = SettingsRegistry.find(value => value.id === id)
  if (!setting) { throw new Error(`Unknown setting: ${id}`) }
  return setting
}

export function searchSettings(query: string) {
  const terms = query.trim().toLocaleLowerCase().split(/\s+/).filter(Boolean)
  if (terms.length === 0) { return [] }
  return SettingsRegistry.filter(setting => {
    const section = SettingsSections.find(value => value.id === setting.section)?.label
    const text = `${setting.label} ${setting.description} ${setting.keywords} ${section}`.toLocaleLowerCase()
    return terms.every(term => text.includes(term))
  })
}

export function readSettingsRoute(path = window.location.pathname): SettingsSectionId | null {
  const normalized = path.replace(/\/+$/, '')
  if (normalized === '/personalization') { return 'personalization' }
  if (normalized === '/settings') { return 'account' }
  if (!normalized.startsWith('/settings/')) { return null }
  return SettingsSections.find(value => normalized === `/settings/${value.id}`)?.id ?? 'account'
}
