export const TextSizes = { small: 'Small', default: 'Default', large: 'Large', 'extra-large': 'Extra Large' } as const
export const LineSpacings = { compact: 'Compact', default: 'Default', relaxed: 'Relaxed' } as const
export const Themes = { light: 'Light', dark: 'Dark', system: 'System' } as const

export interface ReadingPreferences {
  textSize: keyof typeof TextSizes
  lineSpacing: keyof typeof LineSpacings
  theme: keyof typeof Themes
  focusLongContent: boolean
}

export const DefaultReadingPreferences: ReadingPreferences = { textSize: 'default', lineSpacing: 'default', theme: 'system', focusLongContent: false }
export const ReadingCachePrefix = 'askarabbi.reading.v1:'
export const ActiveReadingUserKey = 'askarabbi.reading.active-user'

interface ReadingCache { preferences: ReadingPreferences; pending: boolean }

export function isReadingPreferences(value: unknown): value is ReadingPreferences {
  if (value === null || typeof value !== 'object') { return false }
  const p = value as ReadingPreferences
  return Object.hasOwn(TextSizes, p.textSize) && Object.hasOwn(LineSpacings, p.lineSpacing) && Object.hasOwn(Themes, p.theme) && typeof p.focusLongContent === 'boolean'
}

export function readReadingCache(userId: string): ReadingCache | null {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(`${ReadingCachePrefix}${userId}`) ?? 'null')
    if (value && typeof value === 'object' && 'preferences' in value && isReadingPreferences(value.preferences)) {
      return { preferences: value.preferences, pending: 'pending' in value && value.pending === true }
    }
  } catch { /* Storage can be unavailable; account persistence still works. */ }
  return null
}

export function cacheReadingPreferences(userId: string, preferences: ReadingPreferences, pending: boolean) {
  try {
    // Only presentation preferences are cached, never profile data or authentication tokens.
    localStorage.setItem(`${ReadingCachePrefix}${userId}`, JSON.stringify({ preferences, pending }))
    localStorage.setItem(ActiveReadingUserKey, userId)
  } catch { /* A full or restricted local store must not block reading. */ }
}

export function applyReadingPreferences(preferences: ReadingPreferences) {
  const root = document.documentElement
  root.dataset.readingSize = preferences.textSize
  root.dataset.readingSpacing = preferences.lineSpacing
  root.dataset.theme = preferences.theme === 'system' ? (window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light') : preferences.theme
  root.style.colorScheme = root.dataset.theme
  root.style.backgroundColor = root.dataset.theme === 'dark' ? '#171b22' : '#fbf8f2'
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', root.dataset.theme === 'dark' ? '#171b22' : '#fbf8f2')
}

export function clearActiveReadingUser() {
  try { localStorage.removeItem(ActiveReadingUserKey) } catch { /* Storage may be restricted. */ }
  applyReadingPreferences(DefaultReadingPreferences)
}

// About a minute of reading. Short replies retain the ordinary chat presentation.
export function isLongReading(text: string) { return text.trim().length >= 1000 || text.trim().split(/\s+/u).length >= 150 }

export function readActiveReadingPreferences() {
  try {
    const userId = localStorage.getItem(ActiveReadingUserKey)
    return userId ? readReadingCache(userId)?.preferences ?? DefaultReadingPreferences : DefaultReadingPreferences
  } catch { return DefaultReadingPreferences }
}
