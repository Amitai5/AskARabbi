import type { AuthenticatedUser } from '../auth/authTypes.ts'

export interface PersonalizationProfile {
  fullName: string
  birthDateTime: string
  birthPlace: string
  birthTimeZone: string
  religiousMovement: string
  jewishHeritage: string
  additionalContext: string
}

export function createDefaultPersonalizationProfile(user: AuthenticatedUser): PersonalizationProfile {
  return {
    fullName: user.name,
    birthDateTime: '',
    birthPlace: '',
    birthTimeZone: Intl.DateTimeFormat().resolvedOptions().timeZone ?? '',
    religiousMovement: '',
    jewishHeritage: '',
    additionalContext: '',
  }
}
