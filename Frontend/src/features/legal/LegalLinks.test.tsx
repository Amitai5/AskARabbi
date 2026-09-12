import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { LegalLink, LegalLinks } from './LegalLinks.tsx'
import { PersonalizationPrivacyNotice } from './PersonalizationPrivacyNotice.tsx'
import { searchSettings } from '../settings/settingsRegistry.ts'

describe('Legal policy entry points', () => {
  it('opens public policies in separate tabs to preserve an unfinished form or chat', () => {
    render(<LegalLinks />)
    for (const [name, path] of [['Privacy Policy', '/privacy-policy'], ['Terms of Service', '/terms-of-service']]) {
      const link = screen.getByRole('link', { name: new RegExp(name) })
      expect(link).toHaveAttribute('href', path)
      expect(link).toHaveAttribute('target', '_blank')
      expect(link).toHaveAttribute('rel', 'noopener noreferrer')
      expect(link).toHaveAccessibleName(new RegExp('opens in a new tab'))
    }
  })

  it('links retention disclosures to their specific policy section', () => {
    render(<LegalLink document="privacy-policy" section="retention">Retention details</LegalLink>)
    expect(screen.getByRole('link', { name: /Retention details/ })).toHaveAttribute('href', '/privacy-policy#retention')
  })

  it('explains sensitive profile choices before saving and links the full disclosure', () => {
    render(<PersonalizationPrivacyNotice />)
    expect(screen.getByText(/Religion, heritage, and additional context can be sensitive/)).toBeVisible()
    expect(screen.getByRole('link', { name: /Privacy Policy/ })).toHaveAttribute('href', '/privacy-policy#sensitive-information')
    expect(screen.getByRole('link', { name: /Terms of Service/ })).toHaveAttribute('href', '/terms-of-service')
  })

  it('finds both policies through settings search', () => {
    expect(searchSettings('privacy policy').map(setting => setting.id)).toEqual(['privacy-policy'])
    expect(searchSettings('terms of use').map(setting => setting.id)).toEqual(['terms-of-service'])
  })
})
