import type { AuthenticatedUser } from '../auth/authTypes.ts'

export interface PersonalizationLocation {
  kind: 'city' | 'zip'
  id: string
  label?: string
  timeZone?: string
}

export interface PersonalizationProfile {
  fullName: string
  birthDateTime: string
  birthTimeZone: string
  birthLocation?: PersonalizationLocation | null
  currentLocation?: PersonalizationLocation | null
  conversationLanguage: string
  quotationLanguage: string
  religiousMovement: string
  jewishHeritage: string
  additionalContext: string
}

export function createDefaultPersonalizationProfile(user: AuthenticatedUser): PersonalizationProfile {
  return {
    fullName: user.name,
    birthDateTime: '',
    birthTimeZone: '',
    birthLocation: null,
    currentLocation: null,
    conversationLanguage: 'English',
    quotationLanguage: 'English',
    religiousMovement: '',
    jewishHeritage: '',
    additionalContext: '',
  }
}
