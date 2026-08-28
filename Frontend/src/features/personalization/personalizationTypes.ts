import type { AuthenticatedUser } from '../auth/authTypes.ts'
import { UsTimeZoneValues } from './usTimeZoneOptions.ts'

export interface PersonalizationProfile {
  fullName: string
  birthDateTime: string
  birthTimeZone: string
  conversationLanguage: string
  quotationLanguage: string
  religiousMovement: string
  jewishHeritage: string
  additionalContext: string
}

export function createDefaultPersonalizationProfile(user: AuthenticatedUser): PersonalizationProfile {
  const detectedTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone ?? ''

  return {
    fullName: user.name,
    birthDateTime: '',
    birthTimeZone: UsTimeZoneValues.has(detectedTimeZone) ? detectedTimeZone : '',
    conversationLanguage: 'English',
    quotationLanguage: 'English',
    religiousMovement: '',
    jewishHeritage: '',
    additionalContext: '',
  }
}
