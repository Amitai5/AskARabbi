import type { PersonalizationProfile } from './personalizationTypes.ts'
import { LanguageValues } from './languageOptions.ts'
import { UsTimeZoneValues } from './usTimeZoneOptions.ts'

export type PersonalizationErrors = Partial<Record<keyof PersonalizationProfile, string>>

const MaximumAge = 130

export function validatePersonalizationProfile(profile: PersonalizationProfile, currentDate = new Date()): PersonalizationErrors {
  const errors: PersonalizationErrors = {}

  validateRequiredText(profile.fullName, 120, 'Enter your full name.', 'Full name cannot exceed 120 characters.', (message) => { errors.fullName = message })
  validateBirthDateTime(profile.birthDateTime, currentDate, (message) => { errors.birthDateTime = message })
  validateTimeZone(profile.birthTimeZone, (message) => { errors.birthTimeZone = message })
  validateLanguage(profile.conversationLanguage, 'Choose a supported conversation language.', (message) => { errors.conversationLanguage = message })
  validateLanguage(profile.quotationLanguage, 'Choose a supported quotation language.', (message) => { errors.quotationLanguage = message })
  validateRequiredText(profile.religiousMovement, 100, 'Choose the background that fits best.', 'Religious background cannot exceed 100 characters.', (message) => { errors.religiousMovement = message })
  validateRequiredText(profile.jewishHeritage, 100, 'Choose the heritage or community that fits best.', 'Heritage or community cannot exceed 100 characters.', (message) => { errors.jewishHeritage = message })

  if (profile.additionalContext.trim().length > 2_000) {
    errors.additionalContext = 'Additional context cannot exceed 2,000 characters.'
  }

  return errors
}

export function normalizePersonalizationProfile(profile: PersonalizationProfile): PersonalizationProfile {
  return {
    ...profile,
    fullName: profile.fullName.trim(),
    birthTimeZone: profile.birthTimeZone.trim(),
    conversationLanguage: profile.conversationLanguage.trim(),
    quotationLanguage: profile.quotationLanguage.trim(),
    religiousMovement: profile.religiousMovement.trim(),
    jewishHeritage: profile.jewishHeritage.trim(),
    additionalContext: profile.additionalContext.trim(),
  }
}

function validateRequiredText(value: string, maximumLength: number, requiredMessage: string, maximumMessage: string, addError: (message: string) => void) {
  const normalized = value.trim()
  if (normalized.length === 0) {
    addError(requiredMessage)
    return
  }

  if (normalized.length > maximumLength) {
    addError(maximumMessage)
  }
}

function validateBirthDateTime(value: string, currentDate: Date, addError: (message: string) => void) {
  if (value.length === 0) {
    addError('Enter your birth date and time.')
    return
  }

  const birthDateTime = new Date(value)
  if (Number.isNaN(birthDateTime.getTime())) {
    addError('Enter a valid birth date and time.')
    return
  }

  if (birthDateTime > currentDate) {
    addError('Birth date and time cannot be in the future.')
    return
  }

  const earliestDate = new Date(currentDate)
  earliestDate.setFullYear(earliestDate.getFullYear() - MaximumAge)
  if (birthDateTime < earliestDate) {
    addError(`Birth date cannot represent an age greater than ${MaximumAge} years.`)
  }
}

function validateTimeZone(value: string, addError: (message: string) => void) {
  const normalized = value.trim()
  if (normalized.length === 0) {
    addError('Choose the U.S. time zone where you were born.')
    return
  }

  if (!UsTimeZoneValues.has(normalized)) {
    addError('Choose a time zone from the U.S. list.')
  }
}

function validateLanguage(value: string, message: string, addError: (message: string) => void) {
  if (!LanguageValues.has(value.trim())) {
    addError(message)
  }
}
