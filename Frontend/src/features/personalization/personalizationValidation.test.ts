import { describe, expect, it } from 'vitest'
import type { PersonalizationProfile } from './personalizationTypes.ts'
import { normalizePersonalizationProfile, validatePersonalizationProfile } from './personalizationValidation.ts'

const CurrentDate = new Date('2026-08-25T12:00:00')

const ValidProfile: PersonalizationProfile = {
  fullName: 'Amitai Erfanian',
  birthDateTime: '2001-12-17T09:30',
  birthPlace: 'Los Angeles, United States',
  birthTimeZone: 'America/Los_Angeles',
  religiousMovement: 'Conservadox',
  jewishHeritage: 'Mizrahi',
  additionalContext: 'Iranian Jewish family background.',
}

describe('personalization validation', () => {
  it('accepts a complete valid profile', () => {
    const errors = validatePersonalizationProfile(ValidProfile, CurrentDate)

    expect(errors).toEqual({})
  })

  it('rejects unsafe date ranges and malformed time zones', () => {
    const errors = validatePersonalizationProfile({
      ...ValidProfile,
      birthDateTime: '2100-01-01T10:00',
      birthTimeZone: 'Pacific / Somewhere',
    }, CurrentDate)

    expect(errors.birthDateTime).toBe('Birth date and time cannot be in the future.')
    expect(errors.birthTimeZone).toBe('Use a valid IANA time zone, such as America/Los_Angeles.')
  })

  it('rejects implausible ages and context beyond the profile limit', () => {
    const errors = validatePersonalizationProfile({
      ...ValidProfile,
      birthDateTime: '1800-01-01T10:00',
      additionalContext: 'x'.repeat(2_001),
    }, CurrentDate)

    expect(errors.birthDateTime).toBe('Birth date cannot represent an age greater than 130 years.')
    expect(errors.additionalContext).toBe('Additional context cannot exceed 2,000 characters.')
  })

  it('normalizes user-entered text before saving', () => {
    const normalized = normalizePersonalizationProfile({
      ...ValidProfile,
      fullName: '  Amitai Erfanian  ',
      additionalContext: '  Context that should be trimmed.  ',
    })

    expect(normalized.fullName).toBe('Amitai Erfanian')
    expect(normalized.additionalContext).toBe('Context that should be trimmed.')
  })
})
